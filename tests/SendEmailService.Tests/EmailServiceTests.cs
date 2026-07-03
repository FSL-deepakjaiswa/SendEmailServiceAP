using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SendEmailService.Common;
using SendEmailService.Core.Interfaces;
using SendEmailService.Core.Models;
using SendEmailService.Core.Options;
using SendEmailService.Core.Services;
using Xunit;

namespace SendEmailService.Tests;

/// <summary>
/// Unit tests for <see cref="EmailService"/>.
/// </summary>
public class EmailServiceTests
{
    private readonly Mock<IMailQueueRepository> _mockRepository;
    private readonly Mock<IEmailProvider> _mockProvider;
    private readonly Mock<IServiceLogRepository> _mockLogRepository;
    private readonly EmailServiceOptions _options;

    public EmailServiceTests()
    {
        _mockRepository = new Mock<IMailQueueRepository>();
        _mockProvider = new Mock<IEmailProvider>();
        _mockLogRepository = new Mock<IServiceLogRepository>();
        _options = new EmailServiceOptions
        {
            BatchSize = 10,
            MaxRetryCount = 3,
            MaxParallelism = 5,
            PollingIntervalSeconds = 30,
            StaleRecordTimeoutMinutes = 5,
            InstanceId = "TEST-INSTANCE"
        };
    }

    private EmailService CreateSut()
    {
        var logger = Mock.Of<ILogger<EmailService>>();
        var options = Options.Create(_options);
        return new EmailService(
            _mockRepository.Object,
            _mockProvider.Object,
            _mockLogRepository.Object,
            options,
            logger);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenNoPendingEmails_DoesNotCallProvider()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetPendingEmailsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<MailQueueItem>());

        var sut = CreateSut();

        // Act
        await sut.ProcessBatchAsync(CancellationToken.None);

        // Assert
        _mockProvider.Verify(p => p.SendAsync(It.IsAny<MailQueueItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessBatchAsync_OnSuccess_UpdatesStatusAndArchives()
    {
        // Arrange
        var item = new MailQueueItem { Sno = 1, RecipientEmail = "test@example.com", RetryCount = 0 };
        _mockRepository
            .Setup(r => r.GetPendingEmailsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { item });

        _mockProvider
            .Setup(p => p.SendAsync(item, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailResult.Success("txn-123"));

        _mockRepository.Setup(r => r.UpdateEmailStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.ArchiveEmailAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        await sut.ProcessBatchAsync(CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.UpdateEmailStatusAsync(1, Constants.MailStatus.Sent, "txn-123", null, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.ArchiveEmailAsync(1, Constants.MailStatus.Sent, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessBatchAsync_OnFailure_WithRemainingRetries_UpdatesStatusButDoesNotArchive()
    {
        // Arrange
        var item = new MailQueueItem { Sno = 2, RecipientEmail = "test@example.com", RetryCount = 0 };
        _mockRepository
            .Setup(r => r.GetPendingEmailsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { item });

        _mockProvider
            .Setup(p => p.SendAsync(item, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailResult.Failure("SMTP error"));

        _mockRepository.Setup(r => r.UpdateEmailStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        await sut.ProcessBatchAsync(CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.UpdateEmailStatusAsync(2, Constants.MailStatus.Failed, null, "SMTP error", It.IsAny<CancellationToken>()), Times.Once);
        // RetryCount was 0, updated to 1, which is < MaxRetryCount(3), so no archive.
        _mockRepository.Verify(r => r.ArchiveEmailAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessBatchAsync_OnFailure_WhenMaxRetriesReached_ArchivesAsFailed()
    {
        // Arrange — RetryCount = 2, max = 3, so after this attempt (count becomes 3) archive.
        var item = new MailQueueItem { Sno = 3, RecipientEmail = "test@example.com", RetryCount = 2 };
        _mockRepository
            .Setup(r => r.GetPendingEmailsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { item });

        _mockProvider
            .Setup(p => p.SendAsync(item, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailResult.Failure("Connection timeout"));

        _mockRepository.Setup(r => r.UpdateEmailStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.ArchiveEmailAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        await sut.ProcessBatchAsync(CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.ArchiveEmailAsync(3, Constants.MailStatus.Failed, "Connection timeout", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenRepositoryThrows_DoesNotPropagateException()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetPendingEmailsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection failed"));

        var sut = CreateSut();

        // Act & Assert — should not throw
        await sut.ProcessBatchAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProcessBatchAsync_ProcessesMultipleEmailsInParallel()
    {
        // Arrange
        var items = Enumerable.Range(1, 5)
            .Select(i => new MailQueueItem { Sno = i, RecipientEmail = $"user{i}@example.com", RetryCount = 0 })
            .ToList();

        _mockRepository
            .Setup(r => r.GetPendingEmailsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        _mockProvider
            .Setup(p => p.SendAsync(It.IsAny<MailQueueItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailResult.Success("txn-id"));

        _mockRepository.Setup(r => r.UpdateEmailStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.ArchiveEmailAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        await sut.ProcessBatchAsync(CancellationToken.None);

        // Assert all 5 emails were sent and archived
        _mockProvider.Verify(p => p.SendAsync(It.IsAny<MailQueueItem>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
        _mockRepository.Verify(r => r.ArchiveEmailAsync(It.IsAny<int>(), Constants.MailStatus.Sent, null, It.IsAny<CancellationToken>()), Times.Exactly(5));
    }
}
