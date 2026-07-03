-- =============================================================================
-- DatabaseSetup.sql
-- Creates all tables required by the SendEmailService.
-- Run this script on your SQL Server database before starting the service.
-- =============================================================================

USE [YourDatabase];
GO

-- ---------------------------------------------------------------------------
-- Table: tbl_sendMailQueue
-- The live mail queue. Records are inserted here by the application and
-- consumed by the service.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'tbl_sendMailQueue')
BEGIN
    CREATE TABLE [dbo].[tbl_sendMailQueue]
    (
        [Sno]                           INT             NOT NULL IDENTITY(1,1),
        [puserid]                       INT             NOT NULL,
        [Scheduled_Date]                DATE            NOT NULL,
        [Timeslot]                      VARCHAR(10)     NOT NULL,
        [MailpushedDate]                DATETIME        NULL,
        [EmailTemplateId]               VARCHAR(20)     NOT NULL,
        [TransmissionID]                VARCHAR(100)    NULL,
        [IsMailPushedToSendingQueue]    CHAR(1)         NOT NULL CONSTRAINT [DF_sendMailQueue_Status] DEFAULT ('N'),
        [Date_Added]                    DATETIME        NOT NULL CONSTRAINT [DF_sendMailQueue_DateAdded] DEFAULT (GETDATE()),
        [Added_By]                      VARCHAR(50)     NOT NULL,
        [Batch_ID]                      INT             NOT NULL,
        [RetryCount]                    INT             NOT NULL CONSTRAINT [DF_sendMailQueue_RetryCount] DEFAULT (0),
        [LastRetryDate]                 DATETIME        NULL,
        [PickedBy]                      VARCHAR(100)    NULL,
        [PickedAt]                      DATETIME        NULL,

        CONSTRAINT [PK_tbl_sendMailQueue] PRIMARY KEY CLUSTERED ([Sno] ASC)
    );

    -- Index to speed up polling query
    CREATE NONCLUSTERED INDEX [IX_sendMailQueue_Status_Scheduled]
        ON [dbo].[tbl_sendMailQueue] ([IsMailPushedToSendingQueue], [Scheduled_Date], [Timeslot])
        INCLUDE ([RetryCount], [PickedAt]);

    PRINT 'Created table: tbl_sendMailQueue';
END
ELSE
BEGIN
    -- Add new columns to an existing table (idempotent ALTER TABLE statements)
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('tbl_sendMailQueue') AND name = 'RetryCount')
        ALTER TABLE [dbo].[tbl_sendMailQueue] ADD [RetryCount] INT NOT NULL CONSTRAINT [DF_sendMailQueue_RetryCount] DEFAULT (0);

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('tbl_sendMailQueue') AND name = 'LastRetryDate')
        ALTER TABLE [dbo].[tbl_sendMailQueue] ADD [LastRetryDate] DATETIME NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('tbl_sendMailQueue') AND name = 'PickedBy')
        ALTER TABLE [dbo].[tbl_sendMailQueue] ADD [PickedBy] VARCHAR(100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('tbl_sendMailQueue') AND name = 'PickedAt')
        ALTER TABLE [dbo].[tbl_sendMailQueue] ADD [PickedAt] DATETIME NULL;

    PRINT 'Updated table: tbl_sendMailQueue (added missing columns)';
END
GO

-- ---------------------------------------------------------------------------
-- Table: tbl_sendMailQueue_Archive
-- Dead-letter and success archive. Records are moved here from the live queue
-- after a successful send or after exhausting all retries.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'tbl_sendMailQueue_Archive')
BEGIN
    CREATE TABLE [dbo].[tbl_sendMailQueue_Archive]
    (
        [Sno]                           INT             NOT NULL,
        [puserid]                       INT             NOT NULL,
        [Scheduled_Date]                DATE            NOT NULL,
        [Timeslot]                      VARCHAR(10)     NOT NULL,
        [MailpushedDate]                DATETIME        NULL,
        [EmailTemplateId]               VARCHAR(20)     NOT NULL,
        [TransmissionID]                VARCHAR(100)    NULL,
        [IsMailPushedToSendingQueue]    CHAR(1)         NOT NULL,
        [Date_Added]                    DATETIME        NOT NULL,
        [Added_By]                      VARCHAR(50)     NOT NULL,
        [Batch_ID]                      INT             NOT NULL,
        [RetryCount]                    INT             NOT NULL,
        [LastRetryDate]                 DATETIME        NULL,
        [PickedBy]                      VARCHAR(100)    NULL,
        [PickedAt]                      DATETIME        NULL,

        -- Archive-specific columns
        [ArchiveDate]                   DATETIME        NOT NULL CONSTRAINT [DF_Archive_ArchiveDate] DEFAULT (GETDATE()),
        [FinalStatus]                   CHAR(1)         NOT NULL,  -- Y = Success, F = Failed (dead-letter)
        [ExceptionMsg]                  NVARCHAR(MAX)   NULL,

        CONSTRAINT [PK_tbl_sendMailQueue_Archive] PRIMARY KEY CLUSTERED ([Sno] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_Archive_FinalStatus_ArchiveDate]
        ON [dbo].[tbl_sendMailQueue_Archive] ([FinalStatus], [ArchiveDate] DESC);

    PRINT 'Created table: tbl_sendMailQueue_Archive';
END
GO

-- ---------------------------------------------------------------------------
-- Table: tbl_EmailServiceLog
-- Operational log table. Written to by the custom DatabaseLogger.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'tbl_EmailServiceLog')
BEGIN
    CREATE TABLE [dbo].[tbl_EmailServiceLog]
    (
        [LogId]             BIGINT          NOT NULL IDENTITY(1,1),
        [LogLevel]          VARCHAR(20)     NOT NULL,
        [Message]           NVARCHAR(MAX)   NOT NULL,
        [ExceptionDetail]   NVARCHAR(MAX)   NULL,
        [Sno]               INT             NULL,
        [MachineName]       VARCHAR(100)    NOT NULL,
        [CreatedAt]         DATETIME        NOT NULL CONSTRAINT [DF_EmailServiceLog_CreatedAt] DEFAULT (GETDATE()),

        CONSTRAINT [PK_tbl_EmailServiceLog] PRIMARY KEY CLUSTERED ([LogId] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_EmailServiceLog_CreatedAt]
        ON [dbo].[tbl_EmailServiceLog] ([CreatedAt] DESC)
        INCLUDE ([LogLevel], [MachineName]);

    PRINT 'Created table: tbl_EmailServiceLog';
END
GO

PRINT 'DatabaseSetup.sql completed successfully.';
GO
