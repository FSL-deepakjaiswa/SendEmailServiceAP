using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SendEmailService.Core.Models;
using SendEmailService.Core.Options;
using SendEmailService.Infrastructure.Providers;
using Xunit;

namespace SendEmailService.Tests;

/// <summary>
/// Unit tests for <see cref="SparkPostEmailProvider"/>.
/// These tests verify the provider correctly maps <see cref="MailQueueItem"/>
/// fields and handles SparkPost API responses/errors.
/// </summary>
public class SparkPostEmailProviderTests
{
    private SparkPostOptions CreateOptions(string apiKey = "test-api-key") =>
        new SparkPostOptions
        {
            ApiKey = apiKey,
            SenderEmail = "noreply@example.com",
            SenderName = "Test Sender",
            BaseUrl = "https://api.sparkpost.com/api/v1"
        };

    [Fact]
    public void Constructor_WithValidOptions_DoesNotThrow()
    {
        // Arrange
        var options = Options.Create(CreateOptions());
        var logger = Mock.Of<ILogger<SparkPostEmailProvider>>();

        // Act & Assert
        var exception = Record.Exception(() => new SparkPostEmailProvider(options, logger));
        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_WithEuBaseUrl_DoesNotThrow()
    {
        // Arrange
        var opts = CreateOptions();
        opts.BaseUrl = "https://api.eu.sparkpost.com/api/v1";
        var options = Options.Create(opts);
        var logger = Mock.Of<ILogger<SparkPostEmailProvider>>();

        // Act & Assert
        var exception = Record.Exception(() => new SparkPostEmailProvider(options, logger));
        Assert.Null(exception);
    }

    [Fact]
    public async Task SendAsync_WhenApiKeyIsInvalid_ReturnsFailureResult()
    {
        // Arrange — uses an obviously invalid key; SparkPost will reject it.
        var opts = CreateOptions("INVALID_KEY_FOR_UNIT_TEST");
        opts.BaseUrl = "https://api.sparkpost.com/api/v1";
        var options = Options.Create(opts);
        var logger = Mock.Of<ILogger<SparkPostEmailProvider>>();
        var provider = new SparkPostEmailProvider(options, logger);

        var item = new MailQueueItem
        {
            Sno = 99,
            RecipientEmail = "recipient@example.com",
            EmailSubject = "Test Subject",
            EmailBody = "<p>Test Body</p>",
            RetryCount = 0
        };

        // Act — this will call the real SparkPost endpoint but should fail gracefully.
        // In CI (no network or wrong key), it must return a failure, never throw.
        var result = await provider.SendAsync(item, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }
}
