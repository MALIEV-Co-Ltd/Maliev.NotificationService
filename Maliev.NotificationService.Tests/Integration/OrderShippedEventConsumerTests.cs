using Moq;
using Maliev.NotificationService.Infrastructure.Persistence;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Maliev.MessagingContracts;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.MessagingContracts.Contracts.Orders;
using Maliev.NotificationService.Api.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Maliev.NotificationService.Tests.Testing;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Maliev.NotificationService.Api.Tests.Integration;

public class OrderShippedEventConsumerTests : IClassFixture<BaseIntegrationTestFactory<Program, NotificationDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, NotificationDbContext> _factory;

    public OrderShippedEventConsumerTests(BaseIntegrationTestFactory<Program, NotificationDbContext> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Consume_OrderShippedEvent_WhenAlreadyReceived_ShouldNotPublishDuplicateNotification()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OrderShippedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();

        var consumer = new OrderShippedEventConsumer(logger, context, publishEndpoint.Object);

        var messageId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        context.DeliveryLogs.Add(new Domain.Entities.DeliveryLog
        {
            EventId = messageId.ToString(),
            UserId = customerId.ToString(),
            ChannelType = "rabbitmq-event",
            RecipientIdentifier = "order-ORD-SHIP-DUP",
            Status = "received",
            MessageContent = "Order shipped: ORD-SHIP-DUP",
            AttemptNumber = 1,
            DeliveredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var evt = CreateEvent(messageId, customerId, "ORD-SHIP-DUP");
        var mockContext = CreateConsumeContext(evt);

        // Act
        await consumer.Consume(mockContext.Object);

        // Assert
        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_OrderShippedEvent_ShouldCreateDeliveryLog()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OrderShippedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();

        var consumer = new OrderShippedEventConsumer(logger, context, publishEndpoint.Object);

        var messageId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var evt = CreateEvent(messageId, customerId, "ORD-SHIP-001");
        var mockContext = CreateConsumeContext(evt);

        // Act
        await consumer.Consume(mockContext.Object);

        // Assert
        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal("received", logs[0].Status);
        Assert.Equal("rabbitmq-event", logs[0].ChannelType);
        Assert.Equal(customerId.ToString(), logs[0].UserId);
        Assert.Contains("ORD-SHIP-001", logs[0].RecipientIdentifier);

        publishEndpoint.Verify(
            p => p.Publish(
                It.Is<NotificationEvent>(notificationEvent =>
                    notificationEvent.CausationId == messageId &&
                    notificationEvent.CorrelationId == evt.CorrelationId &&
                    notificationEvent.Payload.NotificationType == "OrderShipped" &&
                    notificationEvent.Payload.Priority == "High" &&
                    notificationEvent.Payload.TemplateId == "order-shipped" &&
                    notificationEvent.Payload.TargetUsers.Count == 1 &&
                    notificationEvent.Payload.TargetUsers[0].UserId == customerId.ToString() &&
                    notificationEvent.Payload.TargetUsers[0].UserType == "customer" &&
                    HasParameter(notificationEvent.Payload.Parameters, "orderId", "ORD-SHIP-001") &&
                    HasParameter(notificationEvent.Payload.Parameters, "carrier", "Thailand Post") &&
                    HasParameter(notificationEvent.Payload.Parameters, "trackingNumber", "TH123456789")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_OrderShippedEvent_WithoutTrackingNumber_ShouldCreateDeliveryLog()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OrderShippedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();

        var consumer = new OrderShippedEventConsumer(logger, context, publishEndpoint.Object);

        var messageId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var evt = CreateEvent(
            messageId,
            customerId,
            "ORD-SHIP-002",
            trackingNumber: null,
            carrier: null,
            estimatedDeliveryDate: null);
        var mockContext = CreateConsumeContext(evt);

        // Act
        await consumer.Consume(mockContext.Object);

        // Assert
        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal("received", logs[0].Status);

        publishEndpoint.Verify(
            p => p.Publish(
                It.Is<NotificationEvent>(notificationEvent =>
                    notificationEvent.Payload.NotificationType == "OrderShipped" &&
                    notificationEvent.Payload.TargetUsers[0].UserId == customerId.ToString() &&
                    HasParameter(notificationEvent.Payload.Parameters, "trackingNumber", "Not available")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_OrderShippedEvent_WhenNotRoutedToNotificationService_ShouldIgnore()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OrderShippedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new OrderShippedEventConsumer(logger, context, publishEndpoint.Object);
        var evt = CreateEvent(Guid.NewGuid(), Guid.NewGuid(), "ORD-SHIP-UNTARGETED", consumedBy: ["BillingService"]);
        var mockContext = CreateConsumeContext(evt);

        await consumer.Consume(mockContext.Object);

        Assert.Empty(await context.DeliveryLogs.ToListAsync());
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_OrderShippedEvent_WithoutRoutingList_ShouldIgnore()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OrderShippedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new OrderShippedEventConsumer(logger, context, publishEndpoint.Object);
        var evt = CreateEvent(Guid.NewGuid(), Guid.NewGuid(), "ORD-SHIP-NOROUTE") with
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

    private static Mock<ConsumeContext<OrderShippedEvent>> CreateConsumeContext(OrderShippedEvent evt)
    {
        var mockContext = new Mock<ConsumeContext<OrderShippedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);
        return mockContext;
    }

    private static OrderShippedEvent CreateEvent(
        Guid messageId,
        Guid customerId,
        string orderNumber,
        IReadOnlyList<string>? consumedBy = null,
        string? trackingNumber = "TH123456789",
        string? carrier = "Thailand Post",
        DateTimeOffset? estimatedDeliveryDate = null)
    {
        return new OrderShippedEvent(
            MessageId: messageId,
            MessageName: "OrderShippedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Order",
            ConsumedBy: consumedBy ?? ["Notification"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new OrderShippedEventPayload(
                OrderId: Guid.NewGuid(),
                OrderNumber: orderNumber,
                CustomerId: customerId,
                ShippedAt: DateTimeOffset.UtcNow,
                TrackingNumber: trackingNumber,
                Carrier: carrier,
                EstimatedDeliveryDate: estimatedDeliveryDate ?? DateTimeOffset.UtcNow.AddDays(3)));
    }
}
