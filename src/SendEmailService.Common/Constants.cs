namespace SendEmailService.Common;

/// <summary>
/// Application-wide constants.
/// </summary>
public static class Constants
{
    /// <summary>Mail queue status values.</summary>
    public static class MailStatus
    {
        /// <summary>Email is pending to be sent.</summary>
        public const string Pending = "N";

        /// <summary>Email has been picked up and is being processed.</summary>
        public const string Processing = "P";

        /// <summary>Email was successfully sent.</summary>
        public const string Sent = "Y";

        /// <summary>Email sending failed.</summary>
        public const string Failed = "F";
    }

    /// <summary>Stored procedure names.</summary>
    public static class StoredProcedures
    {
        /// <summary>Fetches a batch of pending emails and marks them as processing.</summary>
        public const string GetPendingEmails = "usp_GetPendingEmails";

        /// <summary>Updates the status of a mail queue record after a send attempt.</summary>
        public const string UpdateEmailStatus = "usp_UpdateEmailStatus";

        /// <summary>Archives a mail queue record (success or dead-letter).</summary>
        public const string ArchiveEmail = "usp_ArchiveEmail";

        /// <summary>Resets stale 'P' records back to 'N' for crash recovery.</summary>
        public const string ResetStaleProcessing = "usp_ResetStaleProcessing";

        /// <summary>Inserts an operational log entry.</summary>
        public const string InsertServiceLog = "usp_InsertServiceLog";
    }

    /// <summary>Log level names stored in the database.</summary>
    public static class LogLevels
    {
        public const string Information = "Info";
        public const string Warning = "Warning";
        public const string Error = "Error";
        public const string Critical = "Critical";
        public const string Debug = "Debug";
    }
}
