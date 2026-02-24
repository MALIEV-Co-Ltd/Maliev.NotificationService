using Moq;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.MessagingContracts.Generated;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Api.Consumers;
using Maliev.NotificationService.Data;
using Maliev.NotificationService.Data.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Maliev.NotificationService.Tests.Testing;
using Microsoft.Extensions.Logging;

namespace Maliev.NotificationService.Api.Tests.Integration;

public class NotificationEventConsumerTests : IClassFixture<BaseIntegrationTestFactory<Program, NotificationDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, NotificationDbContext> _factory;

    public NotificationEventConsumerTests(BaseIntegrationTestFactory<Program, NotificationDbContext> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Consume_DuplicateEvent_ShouldSkipProcessing()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        var messageId = Guid.NewGuid();
        var deduplicationService = scope.ServiceProvider.GetRequiredService<IDeduplicationService>();

        // Mark as processed
        await deduplicationService.IsDuplicateAsync(messageId.ToString(), DateTimeOffset.UtcNow);

        var consumer = scope.ServiceProvider.GetRequiredService<NotificationEventConsumer>();
        var mockContext = new Mock<ConsumeContext<NotificationEvent>>();
        var evt = CreateEvent(messageId);
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.Headers).Returns(new Mock<Headers>().Object);

        // Act
        await consumer.Consume(mockContext.Object);

        // Assert
        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Empty(logs);
    }

    [Fact]
    public async Task Consume_RetryableFailure_ShouldScheduleRetry()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var mockRouter = new Mock<INotificationRouter>();
        mockRouter.Setup(r => r.RouteAsync(It.IsAny<NotificationEvent>(), It.IsAny<NotificationEventPayloadTargetUsersItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RoutingResult.Failed("Transient Error", isRetryable: true, selectedChannel: "email"));

        var mockRetryService = new Mock<IRetryService>();
        mockRetryService.Setup(s => s.ScheduleRetryAsync(It.IsAny<NotificationEvent>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RetryResult.Success(DateTimeOffset.UtcNow.AddSeconds(10)));

        using var scope = _factory.Services.CreateScope();
        var deduplicationService = scope.ServiceProvider.GetRequiredService<IDeduplicationService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var alertingService = scope.ServiceProvider.GetRequiredService<IAlertingService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<NotificationEventConsumer>>();

        var consumer = new NotificationEventConsumer(
            deduplicationService,
            mockRouter.Object,
            mockRetryService.Object,
            alertingService,
            dbContext,
            logger
        );

        var messageId = Guid.NewGuid();
        var evt = CreateEvent(messageId);
        var mockContext = new Mock<ConsumeContext<NotificationEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.Headers).Returns(new Mock<Headers>().Object);

        // Act
        await consumer.Consume(mockContext.Object);

        // Assert
        mockRetryService.Verify(s => s.ScheduleRetryAsync(
            It.Is<NotificationEvent>(e => e.MessageId == messageId),
            It.IsAny<int>(),
            "Transient Error",
            It.IsAny<CancellationToken>()), Times.Once);

        var logs = await dbContext.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal("failed", logs[0].Status);
    }

    [Fact]
    public async Task Consume_NonRetryableFailure_ShouldMoveToDeadLetter()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var mockRouter = new Mock<INotificationRouter>();
        mockRouter.Setup(r => r.RouteAsync(It.IsAny<NotificationEvent>(), It.IsAny<NotificationEventPayloadTargetUsersItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RoutingResult.Failed("Permanent Error", isRetryable: false, selectedChannel: "email"));

        using var scope = _factory.Services.CreateScope();
        var deduplicationService = scope.ServiceProvider.GetRequiredService<IDeduplicationService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var retryService = scope.ServiceProvider.GetRequiredService<IRetryService>();
        var alertingService = scope.ServiceProvider.GetRequiredService<IAlertingService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<NotificationEventConsumer>>();

        var consumer = new NotificationEventConsumer(
            deduplicationService,
            mockRouter.Object,
            retryService,
            alertingService,
            dbContext,
            logger
        );

        var messageId = Guid.NewGuid();
        var evt = CreateEvent(messageId);
        var mockContext = new Mock<ConsumeContext<NotificationEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.Headers).Returns(new Mock<Headers>().Object);

        // Act
        await consumer.Consume(mockContext.Object);

        // Assert
        var dlqRecords = await dbContext.DeadLetterRecords.Where(r => r.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(dlqRecords);
        Assert.Equal("pending", dlqRecords[0].EscalationStatus);

        var logs = await dbContext.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal("failed", logs[0].Status);
        Assert.Contains("dead letter", logs[0].ProviderResponse);
    }
    private NotificationEvent CreateEvent(Guid? messageId = null)
    {
        return new NotificationEvent(
            MessageId: messageId ?? Guid.NewGuid(),
            MessageName: "NotificationEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Test",
            ConsumedBy: new[] { "NotificationService" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new NotificationEventPayload(
                NotificationType: "Test",
                Priority: "critical",
                TargetUsers: new[] { new NotificationEventPayloadTargetUsersItem("user1", "customer") },
                TemplateId: string.Empty,
                Parameters: new Dictionary<string, string>(),
                Metadata: new NotificationEventPayloadMetadata("en", "test")
            )
        );
    }
}
