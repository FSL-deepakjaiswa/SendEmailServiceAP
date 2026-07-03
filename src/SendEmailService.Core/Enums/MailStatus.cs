namespace SendEmailService.Core.Enums;

/// <summary>
/// Represents the processing status of a mail queue record.
/// </summary>
public enum MailStatus
{
    /// <summary>Email is pending, waiting to be picked up.</summary>
    Pending,

    /// <summary>Email has been picked up and is currently being processed.</summary>
    Processing,

    /// <summary>Email was delivered successfully.</summary>
    Sent,

    /// <summary>Email delivery failed (may be retried).</summary>
    Failed
}
