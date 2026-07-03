using Microsoft.Extensions.Logging;
using SendEmailService.Common;
using SendEmailService.Core.Interfaces;
using SendEmailService.Core.Models;

namespace SendEmailService.Infrastructure.Logging;

/// <summary>
/// Custom <see cref="ILogger"/> implementation that writes log entries to
/// <c>tbl_EmailServiceLog</c> via <see cref="IServiceLogRepository"/>.
/// </summary>
internal sealed class DatabaseLogger : ILogger
{
    private readonly string _categoryName;
    private readonly IServiceLogRepository _serviceLogRepository;

    public DatabaseLogger(string categoryName, IServiceLogRepository serviceLogRepository)
    {
        _categoryName = categoryName;
        _serviceLogRepository = serviceLogRepository;
    }

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) =>
        logLevel is not LogLevel.None and not LogLevel.Trace and not LogLevel.Debug;

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message)) return;

        var entry = new ServiceLogEntry
        {
            LogLevel = MapLogLevel(logLevel),
            Message = $"[{_categoryName}] {message}",
            ExceptionDetail = exception?.ToString(),
            MachineName = Environment.MachineName
        };

        // Fire-and-forget — database logging must not block or throw on the calling thread.
        _ = _serviceLogRepository.LogAsync(entry).ContinueWith(t =>
        {
            // Silently swallow logging errors to avoid log-storm recursion.
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private static string MapLogLevel(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Information => Constants.LogLevels.Information,
        LogLevel.Warning => Constants.LogLevels.Warning,
        LogLevel.Error => Constants.LogLevels.Error,
        LogLevel.Critical => Constants.LogLevels.Critical,
        _ => Constants.LogLevels.Debug
    };
}
