using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendEmailService.Core.Interfaces;
using SendEmailService.Core.Options;

namespace SendEmailService.Worker.Workers;

/// <summary>
/// Main background service that polls the mail queue at a configurable interval
/// and dispatches email batches for processing.
/// </summary>
public sealed class EmailProcessorWorker : BackgroundService
{
    private readonly IEmailService _emailService;
    private readonly EmailServiceOptions _options;
    private readonly ILogger<EmailProcessorWorker> _logger;

    /// <summary>Initialises a new instance of <see cref="EmailProcessorWorker"/>.</summary>
    public EmailProcessorWorker(
        IEmailService emailService,
        IOptions<EmailServiceOptions> options,
        ILogger<EmailProcessorWorker> logger)
    {
        _emailService = emailService;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "EmailProcessorWorker started. PollingInterval={Interval}s, BatchSize={BatchSize}, InstanceId={InstanceId}",
            _options.PollingIntervalSeconds, _options.BatchSize, _options.InstanceId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _emailService.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown — exit the loop cleanly.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in EmailProcessorWorker processing cycle.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_options.PollingIntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested during the delay — exit cleanly.
                break;
            }
        }

        _logger.LogInformation("EmailProcessorWorker stopped.");
    }
}
