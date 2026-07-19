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

public class DeliveryStatusChangedEventConsumerTests : IClassFixture<BaseIntegrationTestFactory<Program, NotificationDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, NotificationDbContext> _factory;

    public DeliveryStatusChangedEventConsumerTests(BaseIntegrationTestFactory<Program, NotificationDbContext> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Consume_DeliveryStatusChangedEvent_ShouldNotifyCustomerAndCreateDeliveryLog()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeliveryStatusChangedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new DeliveryStatusChangedEventConsumer(logger, context, publishEndpoint.Object);
        var messageId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var changedAt = DateTimeOffset.UtcNow;
        var evt = CreateEvent(messageId, customerId, changedAt);
        var mockContext = CreateConsumeContext(evt);

        await consumer.Consume(mockContext.Object);

        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal(customerId.ToString(), logs[0].UserId);
        Assert.Equal("delivery-note-DN-2026-0002", logs[0].RecipientIdentifier);
        Assert.Equal("received", logs[0].Status);
        Assert.Contains("InTransit", logs[0].MessageContent, StringComparison.Ordinal);

        publishEndpoint.Verify(
            p => p.Publish(
                It.Is<NotificationEvent>(notificationEvent =>
                    notificationEvent.CausationId == messageId &&
                    notificationEvent.CorrelationId == evt.CorrelationId &&
                    notificationEvent.Payload.NotificationType == "DeliveryStatusChanged" &&
                    notificationEvent.Payload.Priority == "Normal" &&
                    notificationEvent.Payload.TemplateId == "delivery-status-changed" &&
                    notificationEvent.Payload.TargetUsers.Count == 1 &&
                    notificationEvent.Payload.TargetUsers[0].UserId == customerId.ToString() &&
                    notificationEvent.Payload.TargetUsers[0].UserType == "customer" &&
                    HasParameter(notificationEvent.Payload.Parameters, "orderId", "ORD-2026-0002") &&
                    HasParameter(notificationEvent.Payload.Parameters, "deliveryNoteId", "DN-2026-0002") &&
                    HasParameter(notificationEvent.Payload.Parameters, "previousStatus", "Pending") &&
                    HasParameter(notificationEvent.Payload.Parameters, "newStatus", "InTransit") &&
                    HasParameter(notificationEvent.Payload.Parameters, "changedAt", changedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture))),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_DeliveryStatusChangedEvent_WhenAlreadyReceived_ShouldNotPublishDuplicateNotification()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeliveryStatusChangedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new DeliveryStatusChangedEventConsumer(logger, context, publishEndpoint.Object);
        var messageId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        context.DeliveryLogs.Add(new DeliveryLog
        {
            EventId = messageId.ToString(),
            UserId = customerId.ToString(),
            ChannelType = "rabbitmq-event",
            RecipientIdentifier = "delivery-note-DN-2026-0002",
            Status = "received",
            MessageContent = "Delivery status changed: DN-2026-0002",
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
    public async Task Consume_DeliveryStatusChangedEvent_WhenNotRoutedToNotificationService_ShouldIgnore()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeliveryStatusChangedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new DeliveryStatusChangedEventConsumer(logger, context, publishEndpoint.Object);
        var evt = CreateEvent(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, consumedBy: ["BillingService"]);
        var mockContext = CreateConsumeContext(evt);

        await consumer.Consume(mockContext.Object);

        Assert.Empty(await context.DeliveryLogs.ToListAsync());
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_DeliveryStatusChangedEvent_WithoutRoutingList_ShouldIgnore()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeliveryStatusChangedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new DeliveryStatusChangedEventConsumer(logger, context, publishEndpoint.Object);
        var evt = CreateEvent(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow) with
        {
            ConsumedBy = null!
        };
        var mockContext = CreateConsumeContext(evt);

        await consumer.Consume(mockContext.Object);

        Assert.Empty(await context.DeliveryLogs.ToListAsync());
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_DeliveryStatusChangedEvent_WhenDelivered_ShouldNotDuplicateDeliveryCompletedNotification()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeliveryStatusChangedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new DeliveryStatusChangedEventConsumer(logger, context, publishEndpoint.Object);
        var evt = CreateEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            previousStatus: "InTransit",
            newStatus: "Delivered");
        var mockContext = CreateConsumeContext(evt);

        await consumer.Consume(mockContext.Object);

        Assert.Empty(await context.DeliveryLogs.ToListAsync());
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static DeliveryStatusChangedEvent CreateEvent(
        Guid messageId,
        Guid customerId,
        DateTimeOffset changedAt,
        IReadOnlyList<string>? consumedBy = null,
        string previousStatus = "Pending",
        string newStatus = "InTransit")
    {
        return new DeliveryStatusChangedEvent(
            MessageId: messageId,
            MessageName: nameof(DeliveryStatusChangedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "DeliveryService",
            ConsumedBy: consumedBy ?? ["NotificationService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new DeliveryStatusChangedEventPayload(
                DeliveryNoteId: "DN-2026-0002",
                OrderId: "ORD-2026-0002",
                CustomerId: customerId,
                PreviousStatus: previousStatus,
                NewStatus: newStatus,
                ActualDeliveryTime: null,
                ReceivedByName: null,
                ChangedAt: changedAt,
                ChangedBy: "employee-1"));
    }

    private static Mock<ConsumeContext<DeliveryStatusChangedEvent>> CreateConsumeContext(DeliveryStatusChangedEvent evt)
    {
        var mockContext = new Mock<ConsumeContext<DeliveryStatusChangedEvent>>();
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
