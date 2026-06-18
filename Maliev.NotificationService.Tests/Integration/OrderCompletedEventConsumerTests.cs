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
    public async Task Consume_OrderCompletedEvent_WhenAlreadyReceived_ShouldNotPublishDuplicateNotification()
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
        context.DeliveryLogs.Add(new Domain.Entities.DeliveryLog
        {
            EventId = messageId.ToString(),
            UserId = customerId.ToString(),
            ChannelType = "rabbitmq-event",
            RecipientIdentifier = "order-ORD-COMP-DUP",
            Status = "received",
            MessageContent = "Order completed: ORD-COMP-DUP",
            AttemptNumber = 1,
            DeliveredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var evt = CreateEvent(messageId, customerId, "ORD-COMP-DUP", jobSucceeded: true);
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
        var evt = CreateEvent(messageId, customerId, "ORD-COMP-001", jobSucceeded: true);
        var mockContext = CreateConsumeContext(evt);

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
        var evt = CreateEvent(messageId, customerId, "ORD-COMP-002", jobSucceeded: false);
        var mockContext = CreateConsumeContext(evt);

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
                    notificationEvent.Payload.TemplateId == "order-completion-failed" &&
                    notificationEvent.Payload.TargetUsers[0].UserId == customerId.ToString() &&
                    HasParameter(notificationEvent.Payload.Parameters, "jobSucceeded", "False")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_OrderCompletedEvent_WhenNotRoutedToNotificationService_ShouldIgnore()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OrderCompletedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new OrderCompletedEventConsumer(logger, context, publishEndpoint.Object);
        var evt = CreateEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "ORD-COMP-UNTARGETED",
            jobSucceeded: true,
            consumedBy: ["BillingService"]);
        var mockContext = CreateConsumeContext(evt);

        await consumer.Consume(mockContext.Object);

        Assert.Empty(await context.DeliveryLogs.ToListAsync());
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_OrderCompletedEvent_WithoutRoutingList_ShouldIgnore()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OrderCompletedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new OrderCompletedEventConsumer(logger, context, publishEndpoint.Object);
        var evt = CreateEvent(Guid.NewGuid(), Guid.NewGuid(), "ORD-COMP-NOROUTE", jobSucceeded: true) with
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

    private static Mock<ConsumeContext<OrderCompletedEvent>> CreateConsumeContext(OrderCompletedEvent evt)
    {
        var mockContext = new Mock<ConsumeContext<OrderCompletedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);
        return mockContext;
    }

    private static OrderCompletedEvent CreateEvent(
        Guid messageId,
        Guid customerId,
        string orderNumber,
        bool jobSucceeded,
        IReadOnlyList<string>? consumedBy = null)
    {
        return new OrderCompletedEvent(
            MessageId: messageId,
            MessageName: "OrderCompletedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Order",
            ConsumedBy: consumedBy ?? ["Notification"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new OrderCompletedEventPayload(
                OrderId: Guid.NewGuid(),
                OrderNumber: orderNumber,
                CustomerId: customerId,
                QuotationId: Guid.NewGuid(),
                OrderCreatedAt: DateTimeOffset.UtcNow.AddDays(-7),
                CompletedAt: DateTimeOffset.UtcNow,
                CompletedBy: Guid.NewGuid(),
                JobSucceeded: jobSucceeded,
                ActualMaterialUsedCm3: jobSucceeded ? 125.5 : null,
                ActualPrintTimeHours: jobSucceeded ? 4.2 : null,
                ActualLaborHours: jobSucceeded ? 1.5 : null,
                ActualTotalCost: jobSucceeded ? 850.00 : null,
                Items: Array.Empty<OrderCompletedEventPayloadItemsItem>()));
    }
}
