using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendEmailService.Core.Interfaces;
using SendEmailService.Core.Options;

namespace SendEmailService.Worker.Workers;

/// <summary>
/// Background service that periodically resets mail queue records stuck in
/// <c>P</c> (Processing) status — a crash-recovery mechanism.
/// Runs every 5 minutes (configurable via <see cref="EmailServiceOptions.StaleRecordTimeoutMinutes"/>).
/// </summary>
public sealed class StaleRecordCleanupWorker : BackgroundService
{
    private readonly IMailQueueRepository _mailQueueRepository;
    private readonly EmailServiceOptions _options;
    private readonly ILogger<StaleRecordCleanupWorker> _logger;

    /// <summary>Initialises a new instance of <see cref="StaleRecordCleanupWorker"/>.</summary>
    public StaleRecordCleanupWorker(
        IMailQueueRepository mailQueueRepository,
        IOptions<EmailServiceOptions> options,
        ILogger<StaleRecordCleanupWorker> logger)
    {
        _mailQueueRepository = mailQueueRepository;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "StaleRecordCleanupWorker started. StaleTimeout={Timeout} min, RunInterval={Interval} min.",
            _options.StaleRecordTimeoutMinutes, _options.StaleRecordTimeoutMinutes);

        // Wait briefly on startup to avoid a race against the main worker.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int resetCount = await _mailQueueRepository.ResetStaleProcessingAsync(
                    _options.StaleRecordTimeoutMinutes,
                    _options.InstanceId,
                    stoppingToken);

                if (resetCount > 0)
                {
                    _logger.LogWarning(
                        "StaleRecordCleanup: Reset {Count} stale record(s) from 'P' to 'N'.",
                        resetCount);
                }
                else
                {
                    _logger.LogDebug("StaleRecordCleanup: No stale records found.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in StaleRecordCleanupWorker.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(_options.StaleRecordTimeoutMinutes),
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("StaleRecordCleanupWorker stopped.");
    }
}
