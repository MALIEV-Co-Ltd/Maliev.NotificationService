using Moq;
using Maliev.NotificationService.Infrastructure.Persistence;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Maliev.MessagingContracts;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.NotificationService.Api.Consumers;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Maliev.NotificationService.Tests.Testing;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Maliev.NotificationService.Api.Tests.Integration;

public class PaymentCompletedEventConsumerTests : IClassFixture<BaseIntegrationTestFactory<Program, NotificationDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, NotificationDbContext> _factory;

    public PaymentCompletedEventConsumerTests(BaseIntegrationTestFactory<Program, NotificationDbContext> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Consume_PaymentCompletedEvent_WhenAlreadyReceived_ShouldNotPublishDuplicateNotifications()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentCompletedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();

        var consumer = new PaymentCompletedEventConsumer(logger, context, publishEndpoint.Object);

        var messageId = Guid.NewGuid();
        var customerId = Guid.NewGuid().ToString();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        context.DeliveryLogs.Add(new DeliveryLog
        {
            EventId = messageId.ToString(),
            UserId = customerId,
            ChannelType = "rabbitmq-event",
            RecipientIdentifier = $"payment-{paymentId}",
            Status = "received",
            MessageContent = "Payment completed: Order ORD-123, Amount 100 USD",
            AttemptNumber = 1,
            DeliveredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var evt = new PaymentCompletedEvent(
            MessageId: messageId,
            MessageName: "PaymentCompletedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Payment",
            ConsumedBy: new[] { "Notification" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentCompletedEventPayload(
                OrderId: orderId,
                OrderNumber: "ORD-123",
                CustomerId: customerId,
                PaymentId: paymentId,
                Amount: 100,
                Currency: "USD"
            )
        );

        var mockContext = new Mock<ConsumeContext<PaymentCompletedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

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
    public async Task Consume_PaymentCompletedEvent_ShouldPublishCustomerNotificationAndCreateDeliveryLog()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentCompletedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();

        var consumer = new PaymentCompletedEventConsumer(logger, context, publishEndpoint.Object);

        var messageId = Guid.NewGuid();
        var customerId = Guid.NewGuid().ToString();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var evt = new PaymentCompletedEvent(
            MessageId: messageId,
            MessageName: "PaymentCompletedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Payment",
            ConsumedBy: new[] { "Notification" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentCompletedEventPayload(
                OrderId: orderId,
                OrderNumber: "ORD-123",
                CustomerId: customerId,
                PaymentId: paymentId,
                Amount: 100,
                Currency: "USD"
            )
        );

        var mockContext = new Mock<ConsumeContext<PaymentCompletedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(mockContext.Object);

        // Assert
        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal("received", logs[0].Status);
        Assert.Equal(customerId, logs[0].UserId);
        Assert.Equal($"payment-completed-{paymentId}", logs[0].RecipientIdentifier);
        Assert.Contains("ORD-123", logs[0].MessageContent);

        publishEndpoint.Verify(
            p => p.Publish(
                It.Is<NotificationEvent>(notificationEvent =>
                    notificationEvent.CausationId == messageId &&
                    notificationEvent.CorrelationId == evt.CorrelationId &&
                    notificationEvent.Payload.NotificationType == "PaymentSuccess" &&
                    notificationEvent.Payload.Priority == "Critical" &&
                    notificationEvent.Payload.TemplateId == "order-confirmed" &&
                    notificationEvent.Payload.TargetUsers.Count == 1 &&
                    notificationEvent.Payload.TargetUsers[0].UserId == customerId &&
                    notificationEvent.Payload.TargetUsers[0].UserType == "customer" &&
                    HasParameter(notificationEvent.Payload.Parameters, "orderId", "ORD-123") &&
                    HasParameter(notificationEvent.Payload.Parameters, "amount", "100.00 USD") &&
                    HasParameter(notificationEvent.Payload.Parameters, "paymentId", paymentId.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);

        publishEndpoint.Verify(
            p => p.Publish(
                It.Is<NotificationEvent>(notificationEvent =>
                    notificationEvent.CausationId == messageId &&
                    notificationEvent.CorrelationId == evt.CorrelationId &&
                    notificationEvent.Payload.NotificationType == "PaymentReceivedOperations" &&
                    notificationEvent.Payload.Priority == "Critical" &&
                    notificationEvent.Payload.TemplateId == "operations-payment-received" &&
                    notificationEvent.Payload.TargetUsers.Count == 1 &&
                    notificationEvent.Payload.TargetUsers[0].UserId == NotificationBootstrapData.OperationsInboxUserId &&
                    notificationEvent.Payload.TargetUsers[0].UserType == "staff" &&
                    HasParameter(notificationEvent.Payload.Parameters, "orderId", "ORD-123") &&
                    HasParameter(notificationEvent.Payload.Parameters, "amount", "100.00 USD") &&
                    HasParameter(notificationEvent.Payload.Parameters, "paymentId", paymentId.ToString()) &&
                    HasParameter(notificationEvent.Payload.Parameters, "customerId", customerId)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_PaymentCompletedEvent_WhenPendingAuditExistsForSameTransaction_ShouldStillPublishConfirmation()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentCompletedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new PaymentCompletedEventConsumer(logger, context, publishEndpoint.Object);

        var customerId = Guid.NewGuid().ToString();
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        context.DeliveryLogs.Add(new DeliveryLog
        {
            EventId = Guid.NewGuid().ToString(),
            UserId = customerId,
            ChannelType = "rabbitmq-event",
            RecipientIdentifier = $"payment-{paymentId}",
            Status = "received",
            MessageContent = "Payment pending: Order ORD-123, Amount 100 USD",
            AttemptNumber = 1,
            DeliveredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var messageId = Guid.NewGuid();
        var evt = new PaymentCompletedEvent(
            MessageId: messageId,
            MessageName: "PaymentCompletedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Payment",
            ConsumedBy: new[] { "NotificationService" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentCompletedEventPayload(
                OrderId: orderId,
                OrderNumber: "ORD-123",
                CustomerId: customerId,
                PaymentId: paymentId,
                Amount: 100,
                Currency: "USD"));

        var mockContext = new Mock<ConsumeContext<PaymentCompletedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(mockContext.Object);

        Assert.True(await context.DeliveryLogs.AnyAsync(l => l.EventId == messageId.ToString()));
        publishEndpoint.Verify(
            p => p.Publish(
                It.Is<NotificationEvent>(notificationEvent =>
                    notificationEvent.Payload.NotificationType == "PaymentSuccess" &&
                    notificationEvent.Payload.TemplateId == "order-confirmed"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_PaymentCompletedEvent_WhenNotRoutedToNotificationService_ShouldSkipNotificationAndDeliveryLog()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentCompletedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new PaymentCompletedEventConsumer(logger, context, publishEndpoint.Object);

        var messageId = Guid.NewGuid();
        var evt = new PaymentCompletedEvent(
            MessageId: messageId,
            MessageName: "PaymentCompletedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Payment",
            ConsumedBy: new[] { "InvoiceService", "OrderService" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentCompletedEventPayload(
                OrderId: Guid.NewGuid(),
                OrderNumber: "ORD-NOTIFICATION-SKIP",
                CustomerId: Guid.NewGuid().ToString(),
                PaymentId: Guid.NewGuid(),
                Amount: 100,
                Currency: "USD"));

        var mockContext = new Mock<ConsumeContext<PaymentCompletedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

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

        var json = JsonSerializer.Serialize(parameters);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(key, out var property) &&
            string.Equals(property.ToString(), expectedValue, StringComparison.Ordinal);
    }
}
