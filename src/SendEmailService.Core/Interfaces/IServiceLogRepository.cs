using SendEmailService.Core.Models;

namespace SendEmailService.Core.Interfaces;

/// <summary>
/// Repository abstraction for writing operational log entries to <c>tbl_EmailServiceLog</c>.
/// </summary>
public interface IServiceLogRepository
{
    /// <summary>
    /// Persists a log entry to the database.
    /// </summary>
    /// <param name="entry">The log entry to persist.</param>
    /// <param name="cancellationToken">Propagates a cancellation signal.</param>
    Task LogAsync(ServiceLogEntry entry, CancellationToken cancellationToken = default);
}
