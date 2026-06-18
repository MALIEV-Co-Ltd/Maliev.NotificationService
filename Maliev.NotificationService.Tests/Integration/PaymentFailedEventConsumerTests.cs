using Maliev.MessagingContracts;
using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Api.Consumers;
using Maliev.NotificationService.Infrastructure.Persistence;
using Maliev.NotificationService.Tests.Testing;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Globalization;
using System.Text.Json;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Integration;

public class PaymentFailedEventConsumerTests : IClassFixture<BaseIntegrationTestFactory<Program, NotificationDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, NotificationDbContext> _factory;

    public PaymentFailedEventConsumerTests(BaseIntegrationTestFactory<Program, NotificationDbContext> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Consume_PaymentFailedEvent_ShouldPublishCustomerNotificationAndCreateDeliveryLog()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentFailedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();

        var consumer = new PaymentFailedEventConsumer(logger, context, publishEndpoint.Object);

        var messageId = Guid.NewGuid();
        var customerId = Guid.NewGuid().ToString();
        var transactionId = Guid.NewGuid();
        var orderId = Guid.NewGuid().ToString();
        var evt = new PaymentFailedEvent(
            MessageId: messageId,
            MessageName: "PaymentFailedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Payment",
            ConsumedBy: new[] { "Notification" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentFailedEventPayload(
                TransactionId: transactionId,
                IdempotencyKey: "idem-123",
                Amount: 250.5,
                Currency: "THB",
                CustomerId: customerId,
                OrderId: orderId,
                ProviderName: "stripe",
                ErrorMessage: "Card declined",
                ProviderErrorCode: "card_declined",
                FailedAt: DateTimeOffset.UtcNow
            )
        );

        var mockContext = new Mock<ConsumeContext<PaymentFailedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(mockContext.Object);

        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal("received", logs[0].Status);
        Assert.Equal(customerId, logs[0].UserId);
        Assert.Equal($"payment-failed-{transactionId}", logs[0].RecipientIdentifier);
        Assert.Contains("Card declined", logs[0].MessageContent);

        publishEndpoint.Verify(
            p => p.Publish(
                It.Is<NotificationEvent>(notificationEvent =>
                    notificationEvent.CausationId == messageId &&
                    notificationEvent.CorrelationId == evt.CorrelationId &&
                    notificationEvent.Payload.NotificationType == "PaymentFailure" &&
                    notificationEvent.Payload.Priority == "Critical" &&
                    notificationEvent.Payload.TemplateId == "payment-failed" &&
                    notificationEvent.Payload.TargetUsers.Count == 1 &&
                    notificationEvent.Payload.TargetUsers[0].UserId == customerId &&
                    notificationEvent.Payload.TargetUsers[0].UserType == "customer" &&
                    HasParameter(notificationEvent.Payload.Parameters, "amount", "250.50 THB") &&
                    HasParameter(notificationEvent.Payload.Parameters, "reason", "Card declined") &&
                    HasParameter(notificationEvent.Payload.Parameters, "transactionId", transactionId.ToString()) &&
                    HasParameter(notificationEvent.Payload.Parameters, "providerErrorCode", "card_declined")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_PaymentFailedEvent_FormatsAmountsWithInvariantCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

            await _factory.ResetDatabaseAsync();
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentFailedEventConsumer>>();
            var publishEndpoint = new Mock<IPublishEndpoint>();
            var consumer = new PaymentFailedEventConsumer(logger, context, publishEndpoint.Object);

            var messageId = Guid.NewGuid();
            var customerId = Guid.NewGuid().ToString();
            var transactionId = Guid.NewGuid();
            var evt = new PaymentFailedEvent(
                MessageId: messageId,
                MessageName: "PaymentFailedEvent",
                MessageType: MessageType.Event,
                MessageVersion: "1.0",
                PublishedBy: "Payment",
                ConsumedBy: new[] { "Notification" },
                CorrelationId: Guid.NewGuid(),
                CausationId: null,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: true,
                Payload: new PaymentFailedEventPayload(
                    TransactionId: transactionId,
                    IdempotencyKey: "idem-invariant",
                    Amount: 2500.75,
                    Currency: "THB",
                    CustomerId: customerId,
                    OrderId: "ORD-INVARIANT",
                    ProviderName: "stripe",
                    ErrorMessage: "Card declined",
                    ProviderErrorCode: "card_declined",
                    FailedAt: DateTimeOffset.UtcNow));

            var mockContext = new Mock<ConsumeContext<PaymentFailedEvent>>();
            mockContext.Setup(m => m.Message).Returns(evt);
            mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

            await consumer.Consume(mockContext.Object);

            var log = await context.DeliveryLogs.SingleAsync(l => l.EventId == messageId.ToString());
            Assert.Contains("Amount 2500.75 THB", log.MessageContent);
            Assert.DoesNotContain("2500,75", log.MessageContent);

            publishEndpoint.Verify(
                p => p.Publish(
                    It.Is<NotificationEvent>(notificationEvent =>
                        notificationEvent.Payload.NotificationType == "PaymentFailure" &&
                        HasParameter(notificationEvent.Payload.Parameters, "amount", "2500.75 THB")),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public async Task Consume_PaymentFailedEvent_WhenTransactionAlreadyReceived_ShouldNotPublishDuplicateNotification()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentFailedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();

        var consumer = new PaymentFailedEventConsumer(logger, context, publishEndpoint.Object);

        var customerId = Guid.NewGuid().ToString();
        var transactionId = Guid.NewGuid();
        context.DeliveryLogs.Add(new Domain.Entities.DeliveryLog
        {
            EventId = Guid.NewGuid().ToString(),
            UserId = customerId,
            ChannelType = "rabbitmq-event",
            RecipientIdentifier = $"payment-failed-{transactionId}",
            Status = "received",
            MessageContent = "Payment failed: Order ORD-FAILED, Amount 250.5 THB, Reason: Card declined",
            AttemptNumber = 1,
            DeliveredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var messageId = Guid.NewGuid();
        var evt = new PaymentFailedEvent(
            MessageId: messageId,
            MessageName: "PaymentFailedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Payment",
            ConsumedBy: new[] { "Notification" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentFailedEventPayload(
                TransactionId: transactionId,
                IdempotencyKey: "idem-123",
                Amount: 250.5,
                Currency: "THB",
                CustomerId: customerId,
                OrderId: "ORD-FAILED",
                ProviderName: "stripe",
                ErrorMessage: "Card declined",
                ProviderErrorCode: "card_declined",
                FailedAt: DateTimeOffset.UtcNow));

        var mockContext = new Mock<ConsumeContext<PaymentFailedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(mockContext.Object);

        var logs = await context.DeliveryLogs
            .Where(l => l.RecipientIdentifier == $"payment-failed-{transactionId}")
            .ToListAsync();
        Assert.Single(logs);
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_PaymentFailedEvent_WhenNotRoutedToNotificationService_ShouldSkipNotificationAndDeliveryLog()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentFailedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new PaymentFailedEventConsumer(logger, context, publishEndpoint.Object);

        var messageId = Guid.NewGuid();
        var evt = new PaymentFailedEvent(
            MessageId: messageId,
            MessageName: "PaymentFailedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Payment",
            ConsumedBy: new[] { "QuoteEngine" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentFailedEventPayload(
                TransactionId: Guid.NewGuid(),
                IdempotencyKey: "idem-not-routed",
                Amount: 250.5,
                Currency: "THB",
                CustomerId: Guid.NewGuid().ToString(),
                OrderId: "ORD-NOTIFICATION-SKIP",
                ProviderName: "stripe",
                ErrorMessage: "Card declined",
                ProviderErrorCode: "card_declined",
                FailedAt: DateTimeOffset.UtcNow));

        var mockContext = new Mock<ConsumeContext<PaymentFailedEvent>>();
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
