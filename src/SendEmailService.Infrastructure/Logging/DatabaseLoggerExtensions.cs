using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SendEmailService.Core.Interfaces;

namespace SendEmailService.Infrastructure.Logging;

/// <summary>
/// Extension methods for configuring the database logger.
/// </summary>
public static class DatabaseLoggerExtensions
{
    /// <summary>
    /// Adds the <see cref="DatabaseLoggerProvider"/> to the logging builder so that
    /// log entries at or above <see cref="LogLevel.Information"/> are written to
    /// <c>tbl_EmailServiceLog</c>.
    /// </summary>
    /// <param name="builder">The logging builder to configure.</param>
    /// <returns>The same builder for chaining.</returns>
    public static ILoggingBuilder AddDatabaseLogger(this ILoggingBuilder builder)
    {
        builder.Services.AddSingleton<ILoggerProvider>(sp =>
        {
            var repo = sp.GetRequiredService<IServiceLogRepository>();
            return new DatabaseLoggerProvider(repo);
        });

        return builder;
    }
}
