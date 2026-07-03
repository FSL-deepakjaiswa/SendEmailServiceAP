-- =============================================================================
-- StoredProcedures.sql
-- All stored procedures used by the SendEmailService.
-- Run this script after DatabaseSetup.sql.
-- =============================================================================

USE [YourDatabase];
GO

-- ---------------------------------------------------------------------------
-- usp_GetPendingEmails
-- Fetches a batch of pending emails and atomically marks them as Processing.
-- Uses UPDLOCK + READPAST for multi-instance concurrency safety.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_GetPendingEmails', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetPendingEmails;
GO

CREATE PROCEDURE dbo.usp_GetPendingEmails
    @BatchSize      INT         = 50,
    @MaxRetry       INT         = 3,
    @InstanceId     VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    -- -----------------------------------------------------------------------
    -- NOTE: RecipientEmail, EmailSubject and EmailBody are resolved here via
    -- JOINs with your user and template tables. Replace the placeholder
    -- SELECT expressions below with your actual table/column names.
    -- -----------------------------------------------------------------------

    BEGIN TRANSACTION;

    DECLARE @PickedIds TABLE (Sno INT);

    -- Lock and pick the top N eligible records.
    INSERT INTO @PickedIds (Sno)
    SELECT TOP (@BatchSize)
        q.Sno
    FROM dbo.tbl_sendMailQueue q WITH (UPDLOCK, READPAST)
    WHERE q.IsMailPushedToSendingQueue = 'N'
      AND q.RetryCount < @MaxRetry
      AND q.Scheduled_Date <= CAST(GETDATE() AS DATE)
      AND q.Timeslot       <= CONVERT(VARCHAR(5), GETDATE(), 108)   -- HH:MM
    ORDER BY q.Scheduled_Date ASC, q.Timeslot ASC, q.Sno ASC;

    -- Mark picked records as Processing.
    UPDATE q
    SET
        q.IsMailPushedToSendingQueue = 'P',
        q.PickedBy                  = @InstanceId,
        q.PickedAt                  = GETDATE()
    FROM dbo.tbl_sendMailQueue q
    INNER JOIN @PickedIds p ON q.Sno = p.Sno;

    COMMIT TRANSACTION;

    -- Return the rows with resolved recipient / template data.
    -- TODO: Replace the placeholder expressions with your actual JOINs.
    SELECT
        q.Sno,
        q.puserid,
        q.Scheduled_Date        AS ScheduledDate,
        q.Timeslot,
        q.EmailTemplateId,
        q.Batch_ID              AS BatchId,
        q.RetryCount,

        -- ----------------------------------------------------------------
        -- REPLACE BELOW: Join with your user table to get the email address.
        -- Example:
        --   u.EmailID           AS RecipientEmail,
        --   u.FirstName + ' ' + u.LastName AS RecipientName,
        -- ----------------------------------------------------------------
        ''                      AS RecipientEmail,   -- TODO: replace with actual JOIN

        -- ----------------------------------------------------------------
        -- REPLACE BELOW: Join with your template table to get subject/body.
        -- Example:
        --   t.EmailSubject      AS EmailSubject,
        --   t.EmailBodyHTML     AS EmailBody,
        -- ----------------------------------------------------------------
        ''                      AS EmailSubject,     -- TODO: replace with actual JOIN
        ''                      AS EmailBody         -- TODO: replace with actual JOIN

    FROM dbo.tbl_sendMailQueue q
    INNER JOIN @PickedIds p ON q.Sno = p.Sno;
END
GO

-- ---------------------------------------------------------------------------
-- usp_UpdateEmailStatus
-- Updates the status of a record after a send attempt.
-- On failure: increments RetryCount; resets to 'N' if retries remain.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_UpdateEmailStatus', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_UpdateEmailStatus;
GO

CREATE PROCEDURE dbo.usp_UpdateEmailStatus
    @Sno            INT,
    @Status         CHAR(1),        -- 'Y' = Success, 'F' = Failure
    @TransmissionID VARCHAR(100)    = NULL,
    @ExceptionMsg   NVARCHAR(MAX)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Status = 'Y'
    BEGIN
        -- Success path
        UPDATE dbo.tbl_sendMailQueue
        SET
            IsMailPushedToSendingQueue  = 'Y',
            MailpushedDate              = GETDATE(),
            TransmissionID              = @TransmissionID
        WHERE Sno = @Sno;
    END
    ELSE IF @Status = 'F'
    BEGIN
        -- Failure path: increment retry counter
        UPDATE dbo.tbl_sendMailQueue
        SET
            RetryCount      = RetryCount + 1,
            LastRetryDate   = GETDATE(),
            PickedBy        = NULL,
            PickedAt        = NULL,
            -- If retries remain, reset back to 'N'; otherwise keep 'F' for archiving.
            IsMailPushedToSendingQueue =
                CASE
                    WHEN (RetryCount + 1) < (SELECT TOP 1 CAST(value AS INT)
                                              FROM sys.extended_properties
                                              WHERE 1 = 0)   -- placeholder; use @MaxRetry if needed
                    THEN 'N'
                    ELSE 'F'
                END
        WHERE Sno = @Sno;

        -- Simpler version without MaxRetry parameter (the .NET service decides archiving):
        UPDATE dbo.tbl_sendMailQueue
        SET
            RetryCount      = RetryCount + 1,
            LastRetryDate   = GETDATE(),
            PickedBy        = NULL,
            PickedAt        = NULL,
            IsMailPushedToSendingQueue  = 'N'   -- reset to N so the service can re-evaluate
        WHERE Sno = @Sno
          AND IsMailPushedToSendingQueue <> 'Y'; -- guard against double-update
    END
END
GO

-- ---------------------------------------------------------------------------
-- Revised (simpler) usp_UpdateEmailStatus using @MaxRetry parameter
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_UpdateEmailStatus', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_UpdateEmailStatus;
GO

CREATE PROCEDURE dbo.usp_UpdateEmailStatus
    @Sno            INT,
    @Status         CHAR(1),            -- 'Y' = Success, 'F' = Failure
    @TransmissionID VARCHAR(100)    = NULL,
    @ExceptionMsg   NVARCHAR(MAX)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Status = 'Y'
    BEGIN
        UPDATE dbo.tbl_sendMailQueue
        SET
            IsMailPushedToSendingQueue  = 'Y',
            MailpushedDate              = GETDATE(),
            TransmissionID              = @TransmissionID,
            PickedBy                    = NULL,
            PickedAt                    = NULL
        WHERE Sno = @Sno;
    END
    ELSE
    BEGIN
        -- Increment retry count and reset to 'N' for re-processing.
        -- The .NET EmailService layer decides whether to archive based on RetryCount.
        UPDATE dbo.tbl_sendMailQueue
        SET
            RetryCount                  = RetryCount + 1,
            LastRetryDate               = GETDATE(),
            IsMailPushedToSendingQueue  = 'N',
            PickedBy                    = NULL,
            PickedAt                    = NULL
        WHERE Sno = @Sno;
    END
END
GO

-- ---------------------------------------------------------------------------
-- usp_ArchiveEmail
-- Moves a record from the live queue to the archive table, then deletes it.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_ArchiveEmail', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_ArchiveEmail;
GO

CREATE PROCEDURE dbo.usp_ArchiveEmail
    @Sno            INT,
    @FinalStatus    CHAR(1),            -- 'Y' = Success, 'F' = Dead-letter
    @ExceptionMsg   NVARCHAR(MAX)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRANSACTION;

    INSERT INTO dbo.tbl_sendMailQueue_Archive
    (
        Sno, puserid, Scheduled_Date, Timeslot, MailpushedDate,
        EmailTemplateId, TransmissionID, IsMailPushedToSendingQueue,
        Date_Added, Added_By, Batch_ID, RetryCount, LastRetryDate,
        PickedBy, PickedAt,
        ArchiveDate, FinalStatus, ExceptionMsg
    )
    SELECT
        q.Sno, q.puserid, q.Scheduled_Date, q.Timeslot, q.MailpushedDate,
        q.EmailTemplateId, q.TransmissionID, q.IsMailPushedToSendingQueue,
        q.Date_Added, q.Added_By, q.Batch_ID, q.RetryCount, q.LastRetryDate,
        q.PickedBy, q.PickedAt,
        GETDATE(), @FinalStatus, @ExceptionMsg
    FROM dbo.tbl_sendMailQueue q
    WHERE q.Sno = @Sno;

    DELETE FROM dbo.tbl_sendMailQueue WHERE Sno = @Sno;

    COMMIT TRANSACTION;
END
GO

-- ---------------------------------------------------------------------------
-- usp_ResetStaleProcessing
-- Resets records stuck in 'P' for longer than @StaleMinutes back to 'N'.
-- This is the crash-recovery mechanism called by StaleRecordCleanupWorker.
-- Returns the number of records reset.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_ResetStaleProcessing', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_ResetStaleProcessing;
GO

CREATE PROCEDURE dbo.usp_ResetStaleProcessing
    @StaleMinutes   INT         = 5,
    @InstanceId     VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ResetCount INT = 0;

    UPDATE dbo.tbl_sendMailQueue
    SET
        IsMailPushedToSendingQueue  = 'N',
        PickedBy                    = NULL,
        PickedAt                    = NULL
    WHERE
        IsMailPushedToSendingQueue  = 'P'
        AND PickedAt < DATEADD(MINUTE, -@StaleMinutes, GETDATE());

    SET @ResetCount = @@ROWCOUNT;

    SELECT @ResetCount AS ResetCount;
END
GO

-- ---------------------------------------------------------------------------
-- usp_InsertServiceLog
-- Writes an operational log entry to tbl_EmailServiceLog.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_InsertServiceLog', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_InsertServiceLog;
GO

CREATE PROCEDURE dbo.usp_InsertServiceLog
    @LogLevel       VARCHAR(20),
    @Message        NVARCHAR(MAX),
    @ExceptionDetail NVARCHAR(MAX)  = NULL,
    @Sno            INT             = NULL,
    @MachineName    VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.tbl_EmailServiceLog
        (LogLevel, Message, ExceptionDetail, Sno, MachineName, CreatedAt)
    VALUES
        (@LogLevel, @Message, @ExceptionDetail, @Sno, @MachineName, GETDATE());
END
GO

PRINT 'StoredProcedures.sql completed successfully.';
GO
