using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SendEmailService.Core.Interfaces;
using SendEmailService.Core.Options;
using SendEmailService.Core.Services;
using SendEmailService.Infrastructure.Logging;
using SendEmailService.Infrastructure.Providers;
using SendEmailService.Infrastructure.Repositories;

namespace SendEmailService.Worker.Extensions;

/// <summary>
/// Extension methods for registering application services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all application services required by the email processing service.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddEmailServiceDependencies(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        // Options
        services.AddOptions<EmailServiceOptions>()
            .Bind(configuration.GetSection("EmailServiceOptions"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SparkPostOptions>()
            .Bind(configuration.GetSection("SparkPostOptions"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Repositories
        services.AddSingleton<IMailQueueRepository, MailQueueRepository>();
        services.AddSingleton<IServiceLogRepository, ServiceLogRepository>();

        // Email provider
        services.AddSingleton<IEmailProvider, SparkPostEmailProvider>();

        // Business logic
        services.AddScoped<IEmailService, EmailService>();

        return services;
    }

    /// <summary>
    /// Registers the database logger provider so that application logs are written
    /// to <c>tbl_EmailServiceLog</c> in addition to the console.
    /// </summary>
    /// <param name="logging">The logging builder to configure.</param>
    /// <returns>The same builder for chaining.</returns>
    public static Microsoft.Extensions.Logging.ILoggingBuilder AddEmailServiceDatabaseLogging(
        this Microsoft.Extensions.Logging.ILoggingBuilder logging)
    {
        logging.AddDatabaseLogger();
        return logging;
    }
}
