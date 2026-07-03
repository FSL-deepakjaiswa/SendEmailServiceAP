using SendEmailService.Core.Models;

namespace SendEmailService.Core.Interfaces;

/// <summary>
/// Abstraction over the underlying email delivery provider (e.g., SparkPost).
/// Implement this interface to swap providers without touching business logic.
/// </summary>
public interface IEmailProvider
{
    /// <summary>
    /// Sends a single email using the configured provider.
    /// </summary>
    /// <param name="item">The mail queue item containing recipient and content details.</param>
    /// <param name="cancellationToken">Propagates a cancellation signal.</param>
    /// <returns>An <see cref="EmailResult"/> indicating success or failure.</returns>
    Task<EmailResult> SendAsync(MailQueueItem item, CancellationToken cancellationToken = default);
}
