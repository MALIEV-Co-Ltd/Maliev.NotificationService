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

namespace Maliev.NotificationService.Api.Tests.Integration;

public class OrderShippedEventConsumerTests : IClassFixture<BaseIntegrationTestFactory<Program, NotificationDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, NotificationDbContext> _factory;

    public OrderShippedEventConsumerTests(BaseIntegrationTestFactory<Program, NotificationDbContext> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Consume_OrderShippedEvent_ShouldCreateDeliveryLog()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OrderShippedEventConsumer>>();

        var consumer = new OrderShippedEventConsumer(logger, context);

        var messageId = Guid.NewGuid();
        var evt = new OrderShippedEvent(
            MessageId: messageId,
            MessageName: "OrderShippedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Order",
            ConsumedBy: new[] { "Notification" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new OrderShippedEventPayload(
                OrderId: Guid.NewGuid(),
                OrderNumber: "ORD-SHIP-001",
                ShippedAt: DateTimeOffset.UtcNow,
                TrackingNumber: "TH123456789",
                Carrier: "Thailand Post",
                EstimatedDeliveryDate: DateTimeOffset.UtcNow.AddDays(3)
            )
        );

        var mockContext = new Mock<ConsumeContext<OrderShippedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);

        // Act
        await consumer.Consume(mockContext.Object);

        // Assert
        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal("received", logs[0].Status);
        Assert.Equal("rabbitmq-event", logs[0].ChannelType);
        Assert.Contains("ORD-SHIP-001", logs[0].RecipientIdentifier);
    }

    [Fact]
    public async Task Consume_OrderShippedEvent_WithoutTrackingNumber_ShouldCreateDeliveryLog()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OrderShippedEventConsumer>>();

        var consumer = new OrderShippedEventConsumer(logger, context);

        var messageId = Guid.NewGuid();
        var evt = new OrderShippedEvent(
            MessageId: messageId,
            MessageName: "OrderShippedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Order",
            ConsumedBy: new[] { "Notification" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new OrderShippedEventPayload(
                OrderId: Guid.NewGuid(),
                OrderNumber: "ORD-SHIP-002",
                ShippedAt: DateTimeOffset.UtcNow,
                TrackingNumber: null,
                Carrier: null,
                EstimatedDeliveryDate: null
            )
        );

        var mockContext = new Mock<ConsumeContext<OrderShippedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);

        // Act
        await consumer.Consume(mockContext.Object);

        // Assert
        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal("received", logs[0].Status);
    }
}
