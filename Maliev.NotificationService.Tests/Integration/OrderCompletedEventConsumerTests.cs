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

public class OrderCompletedEventConsumerTests : IClassFixture<BaseIntegrationTestFactory<Program, NotificationDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, NotificationDbContext> _factory;

    public OrderCompletedEventConsumerTests(BaseIntegrationTestFactory<Program, NotificationDbContext> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Consume_OrderCompletedEvent_JobSucceeded_ShouldCreateDeliveryLog()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OrderCompletedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();

        var consumer = new OrderCompletedEventConsumer(logger, context, publishEndpoint.Object);

        var messageId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var evt = new OrderCompletedEvent(
            MessageId: messageId,
            MessageName: "OrderCompletedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Order",
            ConsumedBy: new[] { "Notification" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new OrderCompletedEventPayload(
                OrderId: Guid.NewGuid(),
                OrderNumber: "ORD-COMP-001",
                CustomerId: customerId,
                QuotationId: Guid.NewGuid(),
                OrderCreatedAt: DateTimeOffset.UtcNow.AddDays(-7),
                CompletedAt: DateTimeOffset.UtcNow,
                CompletedBy: Guid.NewGuid(),
                JobSucceeded: true,
                ActualMaterialUsedCm3: 125.5,
                ActualPrintTimeHours: 4.2,
                ActualLaborHours: 1.5,
                ActualTotalCost: 850.00,
                Items: Array.Empty<OrderCompletedEventPayloadItemsItem>()
            )
        );

        var mockContext = new Mock<ConsumeContext<OrderCompletedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(mockContext.Object);

        // Assert
        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal("received", logs[0].Status);
        Assert.Equal("rabbitmq-event", logs[0].ChannelType);
        Assert.Equal(customerId.ToString(), logs[0].UserId);
        Assert.Contains("ORD-COMP-001", logs[0].RecipientIdentifier);
        Assert.Contains("True", logs[0].MessageContent);

        publishEndpoint.Verify(
            p => p.Publish(
                It.Is<NotificationEvent>(notificationEvent =>
                    notificationEvent.CausationId == messageId &&
                    notificationEvent.CorrelationId == evt.CorrelationId &&
                    notificationEvent.Payload.NotificationType == "OrderCompleted" &&
                    notificationEvent.Payload.Priority == "Normal" &&
                    notificationEvent.Payload.TemplateId == "order-completed" &&
                    notificationEvent.Payload.TargetUsers.Count == 1 &&
                    notificationEvent.Payload.TargetUsers[0].UserId == customerId.ToString() &&
                    notificationEvent.Payload.TargetUsers[0].UserType == "customer" &&
                    HasParameter(notificationEvent.Payload.Parameters, "orderId", "ORD-COMP-001") &&
                    HasParameter(notificationEvent.Payload.Parameters, "jobSucceeded", "True")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_OrderCompletedEvent_JobFailed_ShouldCreateDeliveryLog()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OrderCompletedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();

        var consumer = new OrderCompletedEventConsumer(logger, context, publishEndpoint.Object);

        var messageId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var evt = new OrderCompletedEvent(
            MessageId: messageId,
            MessageName: "OrderCompletedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Order",
            ConsumedBy: new[] { "Notification" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new OrderCompletedEventPayload(
                OrderId: Guid.NewGuid(),
                OrderNumber: "ORD-COMP-002",
                CustomerId: customerId,
                QuotationId: Guid.NewGuid(),
                OrderCreatedAt: DateTimeOffset.UtcNow.AddDays(-14),
                CompletedAt: DateTimeOffset.UtcNow,
                CompletedBy: Guid.NewGuid(),
                JobSucceeded: false,
                ActualMaterialUsedCm3: null,
                ActualPrintTimeHours: null,
                ActualLaborHours: null,
                ActualTotalCost: null,
                Items: Array.Empty<OrderCompletedEventPayloadItemsItem>()
            )
        );

        var mockContext = new Mock<ConsumeContext<OrderCompletedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(mockContext.Object);

        // Assert
        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal("received", logs[0].Status);
        Assert.Contains("False", logs[0].MessageContent);

        publishEndpoint.Verify(
            p => p.Publish(
                It.Is<NotificationEvent>(notificationEvent =>
                    notificationEvent.Payload.NotificationType == "OrderCompletionFailed" &&
                    notificationEvent.Payload.Priority == "High" &&
                    notificationEvent.Payload.TargetUsers[0].UserId == customerId.ToString() &&
                    HasParameter(notificationEvent.Payload.Parameters, "jobSucceeded", "False")),
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

        var json = JsonSerializer.Serialize(parameters);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(key, out var property) &&
            string.Equals(property.ToString(), expectedValue, StringComparison.Ordinal);
    }
}
