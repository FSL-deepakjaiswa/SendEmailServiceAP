using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendEmailService.Common;
using SendEmailService.Core.Interfaces;
using SendEmailService.Core.Models;
using SendEmailService.Core.Options;

namespace SendEmailService.Core.Services;

/// <summary>
/// Orchestrates the email processing pipeline:
/// fetch → parallel send → update status → archive.
/// </summary>
public sealed class EmailService : IEmailService
{
    private readonly IMailQueueRepository _mailQueueRepository;
    private readonly IEmailProvider _emailProvider;
    private readonly IServiceLogRepository _serviceLogRepository;
    private readonly EmailServiceOptions _options;
    private readonly ILogger<EmailService> _logger;

    /// <summary>Initialises a new instance of <see cref="EmailService"/>.</summary>
    public EmailService(
        IMailQueueRepository mailQueueRepository,
        IEmailProvider emailProvider,
        IServiceLogRepository serviceLogRepository,
        IOptions<EmailServiceOptions> options,
        ILogger<EmailService> logger)
    {
        _mailQueueRepository = mailQueueRepository;
        _emailProvider = emailProvider;
        _serviceLogRepository = serviceLogRepository;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting email batch processing. InstanceId={InstanceId}", _options.InstanceId);

        IEnumerable<MailQueueItem> batch;

        try
        {
            batch = await _mailQueueRepository.GetPendingEmailsAsync(
                _options.BatchSize,
                _options.MaxRetryCount,
                _options.InstanceId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch pending emails from queue.");
            return;
        }

        var items = batch.ToList();

        if (items.Count == 0)
        {
            _logger.LogInformation("No pending emails found in this cycle.");
            return;
        }

        _logger.LogInformation("Fetched {Count} email(s) for processing.", items.Count);

        int successCount = 0;
        int failureCount = 0;

        using var semaphore = new SemaphoreSlim(_options.MaxParallelism, _options.MaxParallelism);

        var tasks = items.Select(item => ProcessSingleEmailAsync(item, semaphore, cancellationToken));
        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            if (result) successCount++;
            else failureCount++;
        }

        _logger.LogInformation(
            "Batch complete. Total={Total}, Success={Success}, Failed={Failed}",
            items.Count, successCount, failureCount);
    }

    private async Task<bool> ProcessSingleEmailAsync(
        MailQueueItem item,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            _logger.LogDebug("Sending email for Sno={Sno}, Recipient={Recipient}", item.Sno, item.RecipientEmail);

            var result = await _emailProvider.SendAsync(item, cancellationToken);

            if (result.IsSuccess)
            {
                await HandleSuccessAsync(item, result.TransmissionId!, cancellationToken);
                return true;
            }
            else
            {
                await HandleFailureAsync(item, result.ErrorMessage!, cancellationToken);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing Sno={Sno}.", item.Sno);
            await HandleFailureAsync(item, ex.ToString(), cancellationToken);
            return false;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task HandleSuccessAsync(
        MailQueueItem item,
        string transmissionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _mailQueueRepository.UpdateEmailStatusAsync(
                item.Sno, Constants.MailStatus.Sent, transmissionId, null, cancellationToken);

            await _mailQueueRepository.ArchiveEmailAsync(
                item.Sno, Constants.MailStatus.Sent, null, cancellationToken);

            _logger.LogInformation(
                "Email sent successfully. Sno={Sno}, TransmissionId={TransmissionId}",
                item.Sno, transmissionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating success status for Sno={Sno}.", item.Sno);
        }
    }

    private async Task HandleFailureAsync(
        MailQueueItem item,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await _mailQueueRepository.UpdateEmailStatusAsync(
                item.Sno, Constants.MailStatus.Failed, null, errorMessage, cancellationToken);

            // After incrementing, if we've exhausted retries, archive as dead-letter.
            int updatedRetryCount = item.RetryCount + 1;
            if (updatedRetryCount >= _options.MaxRetryCount)
            {
                _logger.LogWarning(
                    "Max retries reached for Sno={Sno}. Archiving as dead-letter.", item.Sno);

                await _mailQueueRepository.ArchiveEmailAsync(
                    item.Sno, Constants.MailStatus.Failed, errorMessage, cancellationToken);
            }
            else
            {
                _logger.LogWarning(
                    "Email send failed for Sno={Sno}. RetryCount={RetryCount}/{MaxRetry}. Will retry.",
                    item.Sno, updatedRetryCount, _options.MaxRetryCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating failure status for Sno={Sno}.", item.Sno);
        }
    }
}
