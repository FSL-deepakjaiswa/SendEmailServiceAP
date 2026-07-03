# SendEmailServiceAP

A production-ready **Windows Service** that processes emails from a `tbl_sendMailQueue` SQL Server table using:

- **.NET 8 Worker Service** — runs as a Windows Service with graceful shutdown
- **SparkPost** — email delivery via the `SparkPost` NuGet package
- **Dapper + Stored Procedures** — fast, lightweight SQL Server data access
- **Multi-instance safe** — `WITH (UPDLOCK, READPAST)` prevents duplicate processing
- **Retry & Dead-letter** — 3 attempts, then archived to `tbl_sendMailQueue_Archive`
- **Database Logging** — operational logs written to `tbl_EmailServiceLog`

---

## Architecture

```
┌────────────────────────────────────────────────────────────────────┐
│                  SendEmailService (Windows Service)                  │
├────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌──────────────────────┐    ┌─────────────────┐    ┌───────────┐  │
│  │ EmailProcessorWorker │───▶│ EmailService    │───▶│ SparkPost │  │
│  │ (polls every 30 s)   │    │ (orchestrator)  │    │ Provider  │  │
│  └──────────────────────┘    └─────────────────┘    └───────────┘  │
│  ┌──────────────────────┐             │                             │
│  │ StaleRecordCleanup   │             │                             │
│  │ Worker (every 5 min) │             ▼                             │
│  └──────────────────────┘   ┌──────────────────┐                   │
│                              │ MailQueue        │                   │
│                              │ Repository       │                   │
│                              │ (Dapper + SPs)   │                   │
│                              └──────────────────┘                   │
│                                       │                             │
│                                       ▼                             │
│            ┌──────────────────────────────────────────────┐        │
│            │              SQL Server                        │        │
│            │  tbl_sendMailQueue        (live queue)         │        │
│            │  tbl_sendMailQueue_Archive (dead-letter)       │        │
│            │  tbl_EmailServiceLog      (operational logs)   │        │
│            └──────────────────────────────────────────────┘        │
└────────────────────────────────────────────────────────────────────┘
```

### Project Structure

```
SendEmailService/
├── SendEmailService.sln
├── src/
│   ├── SendEmailService.Worker/          # Windows Service host (Program.cs, workers)
│   ├── SendEmailService.Core/            # Interfaces, models, options, business logic
│   ├── SendEmailService.Infrastructure/  # Dapper repositories, SparkPost provider, DB logger
│   └── SendEmailService.Common/          # Shared constants
├── tests/
│   └── SendEmailService.Tests/           # xUnit unit tests (Moq)
└── scripts/
    ├── DatabaseSetup.sql                 # Table creation
    ├── StoredProcedures.sql              # All stored procedures
    └── install-service.ps1               # Windows Service installer
```

---

## Prerequisites

| Requirement | Version |
|-------------|---------|
| .NET SDK | 8.0 LTS |
| SQL Server | 2016 or later |
| Windows | Windows Server 2016+ / Windows 10+ (for service install) |

---

## Database Setup

1. Open `scripts/DatabaseSetup.sql` and replace `YourDatabase` with your database name.
2. Run the script against your SQL Server instance:

   ```sql
   sqlcmd -S <server> -d <database> -i scripts/DatabaseSetup.sql
   ```

3. Open `scripts/StoredProcedures.sql` and replace the `YourDatabase` reference at the top.
4. **Important:** Locate the `usp_GetPendingEmails` stored procedure and replace the placeholder
   `RecipientEmail`, `EmailSubject`, and `EmailBody` expressions with actual JOINs to your user
   and template tables:

   ```sql
   -- Replace these placeholders:
   ''  AS RecipientEmail    -- ← JOIN with your user table on puserid
   ''  AS EmailSubject      -- ← JOIN with your template table on EmailTemplateId
   ''  AS EmailBody         -- ← JOIN with your template table on EmailTemplateId
   ```

5. Run the stored procedures script:

   ```sql
   sqlcmd -S <server> -d <database> -i scripts/StoredProcedures.sql
   ```

---

## Configuration

Edit `src/SendEmailService.Worker/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "EmailDB": "Server=.;Database=YourDatabase;Trusted_Connection=true;TrustServerCertificate=true;"
  },
  "EmailServiceOptions": {
    "PollingIntervalSeconds": 30,
    "BatchSize": 50,
    "MaxRetryCount": 3,
    "MaxParallelism": 10,
    "StaleRecordTimeoutMinutes": 5,
    "InstanceId": "EMAIL-SVC-01"
  },
  "SparkPostOptions": {
    "ApiKey": "your-sparkpost-api-key",
    "SenderEmail": "noreply@yourdomain.com",
    "SenderName": "Your Company",
    "BaseUrl": "https://api.sparkpost.com/api/v1"
  }
}
```

| Setting | Description |
|---------|-------------|
| `PollingIntervalSeconds` | How often (in seconds) the service polls for new emails |
| `BatchSize` | Max emails fetched per polling cycle |
| `MaxRetryCount` | Attempts before archiving as dead-letter |
| `MaxParallelism` | Max concurrent SparkPost API calls per cycle |
| `StaleRecordTimeoutMinutes` | Minutes before a "Processing" record is reset |
| `InstanceId` | Unique name for this service instance (multi-server deployments) |

> **EU customers:** Set `BaseUrl` to `https://api.eu.sparkpost.com/api/v1`

---

## Running in Development (Console Mode)

```bash
cd src/SendEmailService.Worker
dotnet run
```

The service runs as a console application in development, making it easy to debug.

---

## Installing as Windows Service

### Option 1 — PowerShell Script (recommended)

```powershell
# 1. Publish the project
dotnet publish src/SendEmailService.Worker -c Release -r win-x64 --self-contained -o ./publish

# 2. Run the installer script as Administrator
.\scripts\install-service.ps1 -Action Install -BinPath "C:\Services\SendEmailService\SendEmailService.Worker.exe"

# Check status
.\scripts\install-service.ps1 -Action Status

# Uninstall
.\scripts\install-service.ps1 -Action Uninstall
```

### Option 2 — sc.exe

```cmd
sc create SendEmailService ^
    binPath= "C:\Services\SendEmailService\SendEmailService.Worker.exe" ^
    DisplayName= "Send Email Service" ^
    start= auto

sc start SendEmailService
sc query SendEmailService
sc stop  SendEmailService
sc delete SendEmailService
```

---

## Processing Flow

```
Every 30 seconds:
│
├─► 1. Call usp_GetPendingEmails (batch = 50)
│        • SELECT TOP N WITH (UPDLOCK, READPAST)
│        • WHERE status='N' AND RetryCount<3 AND Scheduled_Date<=TODAY AND Timeslot<=NOW
│        • UPDATE status='P', PickedBy=InstanceId, PickedAt=NOW
│
├─► 2. Process each email in parallel (max 10 concurrent)
│        │
│        ├─ SUCCESS → usp_UpdateEmailStatus (Y) → usp_ArchiveEmail (Y)
│        │
│        └─ FAILURE → usp_UpdateEmailStatus (F, RetryCount++)
│                     └─ If RetryCount >= 3 → usp_ArchiveEmail (F, ExceptionMsg)
│
└─► 3. Log batch summary
```

### Crash Recovery

Every 5 minutes, `StaleRecordCleanupWorker` calls `usp_ResetStaleProcessing` which:
- Finds records where `status='P'` AND `PickedAt < NOW - 5 min`
- Resets them to `status='N'` so they are re-processed

This handles cases where the service crashed while emails were in-flight.

---

## Running Tests

```bash
dotnet test
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Service won't start | Check Windows Event Log → Application for errors |
| No emails picked up | Verify `Scheduled_Date <= TODAY` and `Timeslot <= current time` in the queue |
| SparkPost 401 errors | Verify `ApiKey` in appsettings.json |
| Connection string errors | Ensure SQL Server is accessible and the DB user has EXECUTE on all SPs |
| Emails stuck in 'P' | Wait 5 min for `StaleRecordCleanupWorker` to reset them, or run `usp_ResetStaleProcessing` manually |
| Logs not appearing in DB | Verify `usp_InsertServiceLog` is created and DB user has INSERT on `tbl_EmailServiceLog` |

### Viewing Operational Logs

```sql
SELECT TOP 100 * 
FROM tbl_EmailServiceLog 
ORDER BY CreatedAt DESC;
```

### Checking Archive / Dead-letter

```sql
-- Successful sends
SELECT * FROM tbl_sendMailQueue_Archive WHERE FinalStatus = 'Y' ORDER BY ArchiveDate DESC;

-- Failed emails (dead-letter)  
SELECT * FROM tbl_sendMailQueue_Archive WHERE FinalStatus = 'F' ORDER BY ArchiveDate DESC;
```
