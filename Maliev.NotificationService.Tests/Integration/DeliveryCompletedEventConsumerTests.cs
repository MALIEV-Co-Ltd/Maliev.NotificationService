using System.Text.Json;
using Maliev.MessagingContracts;
using Maliev.MessagingContracts.Contracts.Delivery;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Api.Consumers;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using Maliev.NotificationService.Tests.Testing;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Integration;

public class DeliveryCompletedEventConsumerTests : IClassFixture<BaseIntegrationTestFactory<Program, NotificationDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, NotificationDbContext> _factory;

    public DeliveryCompletedEventConsumerTests(BaseIntegrationTestFactory<Program, NotificationDbContext> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Consume_DeliveryCompletedEvent_ShouldNotifyCustomerAndCreateDeliveryLog()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeliveryCompletedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new DeliveryCompletedEventConsumer(logger, context, publishEndpoint.Object);
        var messageId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var completedAt = DateTimeOffset.UtcNow;
        var evt = CreateEvent(messageId, customerId, completedAt);
        var mockContext = CreateConsumeContext(evt);

        await consumer.Consume(mockContext.Object);

        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal(customerId.ToString(), logs[0].UserId);
        Assert.Equal("delivery-note-DN-2026-0001", logs[0].RecipientIdentifier);
        Assert.Equal("received", logs[0].Status);
        Assert.Contains("DN-2026-0001", logs[0].MessageContent, StringComparison.Ordinal);
        Assert.Contains("ORD-2026-0001", logs[0].MessageContent, StringComparison.Ordinal);

        publishEndpoint.Verify(
            p => p.Publish(
                It.Is<NotificationEvent>(notificationEvent =>
                    notificationEvent.CausationId == messageId &&
                    notificationEvent.CorrelationId == evt.CorrelationId &&
                    notificationEvent.Payload.NotificationType == "DeliveryCompleted" &&
                    notificationEvent.Payload.Priority == "High" &&
                    notificationEvent.Payload.TemplateId == "delivery-completed" &&
                    notificationEvent.Payload.TargetUsers.Count == 1 &&
                    notificationEvent.Payload.TargetUsers[0].UserId == customerId.ToString() &&
                    notificationEvent.Payload.TargetUsers[0].UserType == "customer" &&
                    HasParameter(notificationEvent.Payload.Parameters, "orderId", "ORD-2026-0001") &&
                    HasParameter(notificationEvent.Payload.Parameters, "deliveryNoteId", "DN-2026-0001") &&
                    HasParameter(notificationEvent.Payload.Parameters, "receivedByName", "John Doe") &&
                    HasParameter(notificationEvent.Payload.Parameters, "completedAt", completedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture))),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_DeliveryCompletedEvent_WhenAlreadyReceived_ShouldNotPublishDuplicateNotification()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeliveryCompletedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new DeliveryCompletedEventConsumer(logger, context, publishEndpoint.Object);
        var messageId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        context.DeliveryLogs.Add(new DeliveryLog
        {
            EventId = messageId.ToString(),
            UserId = customerId.ToString(),
            ChannelType = "rabbitmq-event",
            RecipientIdentifier = "delivery-note-DN-2026-0001",
            Status = "received",
            MessageContent = "Delivery completed: DN-2026-0001",
            AttemptNumber = 1,
            DeliveredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        var evt = CreateEvent(messageId, customerId, DateTimeOffset.UtcNow);
        var mockContext = CreateConsumeContext(evt);

        await consumer.Consume(mockContext.Object);

        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_DeliveryCompletedEvent_WhenNotRoutedToNotificationService_ShouldIgnore()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeliveryCompletedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new DeliveryCompletedEventConsumer(logger, context, publishEndpoint.Object);
        var evt = CreateEvent(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, ["BillingService"]);
        var mockContext = CreateConsumeContext(evt);

        await consumer.Consume(mockContext.Object);

        Assert.Empty(await context.DeliveryLogs.ToListAsync());
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static DeliveryCompletedEvent CreateEvent(
        Guid messageId,
        Guid customerId,
        DateTimeOffset completedAt,
        IReadOnlyList<string>? consumedBy = null)
    {
        return new DeliveryCompletedEvent(
            MessageId: messageId,
            MessageName: nameof(DeliveryCompletedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "DeliveryService",
            ConsumedBy: consumedBy ?? ["NotificationService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new DeliveryCompletedEventPayload(
                DeliveryNoteId: "DN-2026-0001",
                OrderId: "ORD-2026-0001",
                PurchaseOrderId: 20260001,
                CustomerId: customerId,
                CompletedAt: completedAt,
                ReceivedByName: "John Doe"));
    }

    private static Mock<ConsumeContext<DeliveryCompletedEvent>> CreateConsumeContext(DeliveryCompletedEvent evt)
    {
        var mockContext = new Mock<ConsumeContext<DeliveryCompletedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);
        return mockContext;
    }

    private static bool HasParameter(object parameters, string key, string expectedValue)
    {
        if (parameters is IReadOnlyDictionary<string, object> dictionary &&
            dictionary.TryGetValue(key, out var value))
        {
            return string.Equals(value?.ToString(), expectedValue, StringComparison.Ordinal);
        }

        var json = JsonSerializer.Serialize(parameters);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(key, out var property) &&
            string.Equals(property.ToString(), expectedValue, StringComparison.Ordinal);
    }
}
