using Microsoft.Extensions.Hosting;
using SendEmailService.Worker.Extensions;
using SendEmailService.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);

// Support running as a Windows Service (no console interaction required).
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "SendEmailService";
});

// Register application dependencies (repositories, providers, services, options).
builder.Services.AddEmailServiceDependencies(builder.Configuration);

// Register background workers.
builder.Services.AddHostedService<EmailProcessorWorker>();
builder.Services.AddHostedService<StaleRecordCleanupWorker>();

// Add database logging in addition to the default console logger.
builder.Logging.AddEmailServiceDatabaseLogging();

var host = builder.Build();
host.Run();
