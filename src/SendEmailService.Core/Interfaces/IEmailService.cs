namespace SendEmailService.Core.Interfaces;

/// <summary>
/// Orchestrates the end-to-end email processing pipeline:
/// fetch → send → update status → archive.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Processes one batch of pending emails from the queue.
    /// Each email is sent in parallel (bounded by <c>MaxParallelism</c>).
    /// Results are logged and records are archived or retried as appropriate.
    /// </summary>
    /// <param name="cancellationToken">Propagates a cancellation signal.</param>
    Task ProcessBatchAsync(CancellationToken cancellationToken = default);
}
