using SendEmailService.Core.Models;

namespace SendEmailService.Core.Interfaces;

/// <summary>
/// Repository abstraction for all <c>tbl_sendMailQueue</c> database operations.
/// </summary>
public interface IMailQueueRepository
{
    /// <summary>
    /// Fetches a batch of pending emails from the queue, atomically marking them
    /// as <c>P</c> (Processing) to prevent duplicate picks across instances.
    /// Uses <c>WITH (UPDLOCK, READPAST)</c> for concurrency safety.
    /// </summary>
    /// <param name="batchSize">Maximum number of records to retrieve.</param>
    /// <param name="maxRetry">Records with <c>RetryCount &gt;= maxRetry</c> are skipped.</param>
    /// <param name="instanceId">Identifier of this service instance (stored in <c>PickedBy</c>).</param>
    /// <param name="cancellationToken">Propagates a cancellation signal.</param>
    /// <returns>A collection of mail queue items ready for processing.</returns>
    Task<IEnumerable<MailQueueItem>> GetPendingEmailsAsync(
        int batchSize,
        int maxRetry,
        string instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of a mail queue record after a send attempt.
    /// On failure, increments <c>RetryCount</c> and conditionally resets the
    /// record back to <c>N</c> if retries remain.
    /// </summary>
    /// <param name="sno">Primary key of the record to update.</param>
    /// <param name="status"><c>Y</c> for success, <c>F</c> for failure.</param>
    /// <param name="transmissionId">SparkPost transmission ID (success only).</param>
    /// <param name="exceptionMsg">Error message (failure only).</param>
    /// <param name="cancellationToken">Propagates a cancellation signal.</param>
    Task UpdateEmailStatusAsync(
        int sno,
        string status,
        string? transmissionId,
        string? exceptionMsg,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a record from the live queue to <c>tbl_sendMailQueue_Archive</c>
    /// and deletes it from the source table.
    /// </summary>
    /// <param name="sno">Primary key of the record to archive.</param>
    /// <param name="finalStatus"><c>Y</c> for success archive, <c>F</c> for dead-letter.</param>
    /// <param name="exceptionMsg">Error details for failed records (optional).</param>
    /// <param name="cancellationToken">Propagates a cancellation signal.</param>
    Task ArchiveEmailAsync(
        int sno,
        string finalStatus,
        string? exceptionMsg,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets records that have been stuck in <c>P</c> (Processing) status
    /// for longer than <paramref name="staleMinutes"/> back to <c>N</c> (Pending).
    /// Provides crash-recovery for records orphaned by a crashed instance.
    /// </summary>
    /// <param name="staleMinutes">Age threshold in minutes.</param>
    /// <param name="instanceId">Identifier of this service instance.</param>
    /// <param name="cancellationToken">Propagates a cancellation signal.</param>
    /// <returns>Number of records reset.</returns>
    Task<int> ResetStaleProcessingAsync(
        int staleMinutes,
        string instanceId,
        CancellationToken cancellationToken = default);
}
