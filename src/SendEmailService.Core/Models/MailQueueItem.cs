namespace SendEmailService.Core.Models;

/// <summary>
/// Represents a single email record fetched from the mail queue.
/// </summary>
public class MailQueueItem
{
    /// <summary>Primary key of the queue record.</summary>
    public int Sno { get; set; }

    /// <summary>User identifier used to look up the recipient.</summary>
    public int Puserid { get; set; }

    /// <summary>Date on which the email should be sent.</summary>
    public DateTime ScheduledDate { get; set; }

    /// <summary>Time window (e.g., "10:00") within which the email should be sent.</summary>
    public string Timeslot { get; set; } = string.Empty;

    /// <summary>Identifier of the email template (e.g., "RAO01N").</summary>
    public string EmailTemplateId { get; set; } = string.Empty;

    /// <summary>Batch grouping identifier.</summary>
    public int BatchId { get; set; }

    /// <summary>
    /// Recipient email address — resolved inside <c>usp_GetPendingEmails</c>
    /// via a JOIN on the user table using <see cref="Puserid"/>.
    /// </summary>
    public string RecipientEmail { get; set; } = string.Empty;

    /// <summary>Email subject resolved from the template table.</summary>
    public string EmailSubject { get; set; } = string.Empty;

    /// <summary>Email body (HTML) resolved from the template table.</summary>
    public string EmailBody { get; set; } = string.Empty;

    /// <summary>Number of send attempts made so far.</summary>
    public int RetryCount { get; set; }
}
