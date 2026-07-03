using Microsoft.Extensions.Logging;
using SendEmailService.Core.Interfaces;

namespace SendEmailService.Infrastructure.Logging;

/// <summary>
/// <see cref="ILoggerProvider"/> that creates <see cref="DatabaseLogger"/> instances
/// backed by <see cref="IServiceLogRepository"/>.
/// </summary>
public sealed class DatabaseLoggerProvider : ILoggerProvider
{
    private readonly IServiceLogRepository _serviceLogRepository;

    /// <summary>Initialises a new instance of <see cref="DatabaseLoggerProvider"/>.</summary>
    /// <param name="serviceLogRepository">Repository used to write log entries.</param>
    public DatabaseLoggerProvider(IServiceLogRepository serviceLogRepository)
    {
        _serviceLogRepository = serviceLogRepository;
    }

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) =>
        new DatabaseLogger(categoryName, _serviceLogRepository);

    /// <inheritdoc/>
    public void Dispose() { /* No unmanaged resources to release. */ }
}
