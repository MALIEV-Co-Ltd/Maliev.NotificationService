using Maliev.MessagingContracts;
using Maliev.MessagingContracts.Contracts.Jobs;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Api.Consumers;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Infrastructure.Persistence;
using Maliev.NotificationService.Tests.Testing;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Integration;

public class JobCreatedEventConsumerTests : IClassFixture<BaseIntegrationTestFactory<Program, NotificationDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, NotificationDbContext> _factory;

    public JobCreatedEventConsumerTests(BaseIntegrationTestFactory<Program, NotificationDbContext> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Consume_JobCreatedEvent_ShouldNotifyOperationsForJobTicket()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<JobCreatedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new JobCreatedEventConsumer(logger, context, publishEndpoint.Object);

        var messageId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var evt = new JobCreatedEvent(
            MessageId: messageId,
            MessageName: nameof(JobCreatedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "job-service",
            ConsumedBy: ["NotificationService", "MaterialService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new JobCreatedEventPayload(
                JobId: jobId,
                OrderId: orderId,
                OrderItemId: orderItemId,
                ProcessType: "SLS",
                JobNumber: "JOB-2026-00042",
                CreatedAt: DateTimeOffset.UtcNow));

        var mockContext = new Mock<ConsumeContext<JobCreatedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(mockContext.Object);

        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal(NotificationBootstrapData.OperationsInboxUserId, logs[0].UserId);
        Assert.Equal($"job-{jobId}", logs[0].RecipientIdentifier);
        Assert.Contains("job ticket", logs[0].MessageContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JOB-2026-00042", logs[0].MessageContent, StringComparison.Ordinal);

        publishEndpoint.Verify(
            p => p.Publish(
                It.Is<NotificationEvent>(notificationEvent =>
                    notificationEvent.CausationId == messageId &&
                    notificationEvent.CorrelationId == evt.CorrelationId &&
                    notificationEvent.Payload.NotificationType == "ProductionJobCreated" &&
                    notificationEvent.Payload.Priority == "High" &&
                    notificationEvent.Payload.TemplateId == "operations-job-created" &&
                    notificationEvent.Payload.TargetUsers.Count == 1 &&
                    notificationEvent.Payload.TargetUsers[0].UserId == NotificationBootstrapData.OperationsInboxUserId &&
                    notificationEvent.Payload.TargetUsers[0].UserType == "staff" &&
                    HasParameter(notificationEvent.Payload.Parameters, "jobId", jobId.ToString()) &&
                    HasParameter(notificationEvent.Payload.Parameters, "jobNumber", "JOB-2026-00042") &&
                    HasParameter(notificationEvent.Payload.Parameters, "orderId", orderId.ToString()) &&
                    HasParameter(notificationEvent.Payload.Parameters, "orderItemId", orderItemId.ToString()) &&
                    HasParameter(notificationEvent.Payload.Parameters, "technology", "SLS")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_JobCreatedEvent_WithoutRoutingList_IgnoresEvent()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<JobCreatedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new JobCreatedEventConsumer(logger, context, publishEndpoint.Object);
        var messageId = Guid.NewGuid();
        var evt = BuildJobCreatedEvent(messageId) with
        {
            ConsumedBy = null!
        };
        var mockContext = BuildConsumeContext(evt);

        await consumer.Consume(mockContext.Object);

        Assert.False(await context.DeliveryLogs.AnyAsync(l => l.EventId == messageId.ToString()));
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_JobCreatedEvent_WithoutNotificationServiceRouting_IgnoresEvent()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<JobCreatedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new JobCreatedEventConsumer(logger, context, publishEndpoint.Object);
        var messageId = Guid.NewGuid();
        var evt = BuildJobCreatedEvent(messageId) with
        {
            ConsumedBy = ["MaterialService", "AuditService"]
        };
        var mockContext = BuildConsumeContext(evt);

        await consumer.Consume(mockContext.Object);

        Assert.False(await context.DeliveryLogs.AnyAsync(l => l.EventId == messageId.ToString()));
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static bool HasParameter(object parameters, string key, string expectedValue)
    {
        if (parameters is IReadOnlyDictionary<string, object> dictionary &&
            dictionary.TryGetValue(key, out var value))
        {
            return string.Equals(value?.ToString(), expectedValue, StringComparison.Ordinal);
        }

        return false;
    }

    private static Mock<ConsumeContext<JobCreatedEvent>> BuildConsumeContext(JobCreatedEvent evt)
    {
        var mockContext = new Mock<ConsumeContext<JobCreatedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);
        return mockContext;
    }

    private static JobCreatedEvent BuildJobCreatedEvent(Guid messageId)
    {
        var jobId = Guid.NewGuid();
        return new JobCreatedEvent(
            MessageId: messageId,
            MessageName: nameof(JobCreatedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "job-service",
            ConsumedBy: ["NotificationService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new JobCreatedEventPayload(
                JobId: jobId,
                OrderId: Guid.NewGuid(),
                OrderItemId: Guid.NewGuid(),
                ProcessType: "SLS",
                JobNumber: "JOB-2026-00042",
                CreatedAt: DateTimeOffset.UtcNow));
    }
}
