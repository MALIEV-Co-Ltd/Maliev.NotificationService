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

public class JobStatusChangedEventConsumerTests : IClassFixture<BaseIntegrationTestFactory<Program, NotificationDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, NotificationDbContext> _factory;

    public JobStatusChangedEventConsumerTests(BaseIntegrationTestFactory<Program, NotificationDbContext> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Consume_CompletedJobStatusChange_ShouldNotifyOperationsForQcIntake()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<JobStatusChangedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new JobStatusChangedEventConsumer(logger, context, publishEndpoint.Object);

        var messageId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var evt = new JobStatusChangedEvent(
            MessageId: messageId,
            MessageName: nameof(JobStatusChangedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "job-service",
            ConsumedBy: ["NotificationService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new JobStatusChangedEventPayload(
                JobId: jobId,
                OrderId: orderId,
                PreviousStatus: "Finishing",
                NewStatus: "Completed",
                Technology: "FDM",
                AssignedMachineId: "FDM-001",
                ChangedAt: DateTimeOffset.UtcNow,
                ChangedBy: "scanner-operator"));

        var mockContext = new Mock<ConsumeContext<JobStatusChangedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(mockContext.Object);

        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal(NotificationBootstrapData.OperationsInboxUserId, logs[0].UserId);
        Assert.Equal($"job-{jobId}", logs[0].RecipientIdentifier);
        Assert.Contains("QC intake", logs[0].MessageContent, StringComparison.OrdinalIgnoreCase);

        publishEndpoint.Verify(
            p => p.Publish(
                It.Is<NotificationEvent>(notificationEvent =>
                    notificationEvent.CausationId == messageId &&
                    notificationEvent.CorrelationId == evt.CorrelationId &&
                    notificationEvent.Payload.NotificationType == "JobCompletedQcReady" &&
                    notificationEvent.Payload.Priority == "High" &&
                    notificationEvent.Payload.TemplateId == "operations-job-completed-qc-ready" &&
                    notificationEvent.Payload.TargetUsers.Count == 1 &&
                    notificationEvent.Payload.TargetUsers[0].UserId == NotificationBootstrapData.OperationsInboxUserId &&
                    notificationEvent.Payload.TargetUsers[0].UserType == "staff" &&
                    HasParameter(notificationEvent.Payload.Parameters, "jobId", jobId.ToString()) &&
                    HasParameter(notificationEvent.Payload.Parameters, "orderId", orderId.ToString()) &&
                    HasParameter(notificationEvent.Payload.Parameters, "technology", "FDM") &&
                    HasParameter(notificationEvent.Payload.Parameters, "assignedMachineId", "FDM-001") &&
                    HasParameter(notificationEvent.Payload.Parameters, "changedBy", "scanner-operator")),
                It.IsAny<CancellationToken>()),
            Times.Once);
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
}
