using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendEmailService.Core.Interfaces;
using SendEmailService.Core.Models;
using SendEmailService.Core.Options;
using SparkPost;

namespace SendEmailService.Infrastructure.Providers;

/// <summary>
/// SparkPost implementation of <see cref="IEmailProvider"/>.
/// Uses the <c>SparkPost.dll</c> NuGet package to send transactional emails.
/// </summary>
public sealed class SparkPostEmailProvider : IEmailProvider
{
    private readonly Client _sparkPostClient;
    private readonly SparkPostOptions _options;
    private readonly ILogger<SparkPostEmailProvider> _logger;

    /// <summary>Initialises a new instance of <see cref="SparkPostEmailProvider"/>.</summary>
    /// <param name="options">SparkPost configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public SparkPostEmailProvider(
        IOptions<SparkPostOptions> options,
        ILogger<SparkPostEmailProvider> logger)
    {
        _options = options.Value;
        _logger = logger;

        _sparkPostClient = string.IsNullOrWhiteSpace(_options.BaseUrl) ||
                           _options.BaseUrl.Equals("https://api.sparkpost.com/api/v1", StringComparison.OrdinalIgnoreCase)
            ? new Client(_options.ApiKey)
            : new Client(_options.ApiKey, _options.BaseUrl);
    }

    /// <inheritdoc/>
    public async Task<EmailResult> SendAsync(MailQueueItem item, CancellationToken cancellationToken = default)
    {
        try
        {
            var transmission = BuildTransmission(item);
            var response = await _sparkPostClient.Transmissions.Send(transmission);

            if (response.TotalRejectedRecipients > 0)
            {
                var errorMsg = $"SparkPost rejected {response.TotalRejectedRecipients} recipient(s). " +
                               $"StatusCode={response.StatusCode}, Reason={response.ReasonPhrase}";
                _logger.LogWarning("Partial rejection for Sno={Sno}: {Error}", item.Sno, errorMsg);
                return EmailResult.Failure(errorMsg);
            }

            _logger.LogDebug(
                "SparkPost accepted transmission for Sno={Sno}. TransmissionId={Id}, Accepted={Count}",
                item.Sno, response.Id, response.TotalAcceptedRecipients);

            return EmailResult.Success(response.Id ?? string.Empty);
        }
        catch (ResponseException ex)
        {
            var errorMsg = $"SparkPost API error: {ex.Message}. " +
                           $"StatusCode={ex.Response?.StatusCode}, Content={ex.Response?.Content}";
            _logger.LogError(ex, "SparkPost ResponseException for Sno={Sno}: {Error}", item.Sno, errorMsg);
            return EmailResult.Failure(errorMsg);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Unexpected error sending email: {ex.Message}";
            _logger.LogError(ex, "Unexpected error for Sno={Sno}: {Error}", item.Sno, errorMsg);
            return EmailResult.Failure(errorMsg);
        }
    }

    private Transmission BuildTransmission(MailQueueItem item)
    {
        var transmission = new Transmission
        {
            Content = new Content
            {
                From = new Address
                {
                    Email = _options.SenderEmail,
                    Name = _options.SenderName
                },
                Subject = item.EmailSubject,
                Html = item.EmailBody
            },
            Recipients = new List<Recipient>
            {
                new Recipient
                {
                    Address = new Address
                    {
                        Email = item.RecipientEmail
                    }
                }
            }
        };

        return transmission;
    }
}
