using Maliev.MessagingContracts;
using Maliev.MessagingContracts.Contracts.Payments;
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
using System.Globalization;
using System.Text.Json;

namespace Maliev.NotificationService.Api.Tests.Integration;

public class PaymentTerminalEventConsumerTests : IClassFixture<BaseIntegrationTestFactory<Program, NotificationDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, NotificationDbContext> _factory;

    public PaymentTerminalEventConsumerTests(BaseIntegrationTestFactory<Program, NotificationDbContext> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Consume_PaymentCancelledEvent_ShouldPublishCustomerNotificationAndCreateDeliveryLog()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentCancelledEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new PaymentCancelledEventConsumer(logger, context, publishEndpoint.Object);

        var messageId = Guid.NewGuid();
        var customerId = Guid.NewGuid().ToString();
        var transactionId = Guid.NewGuid();
        var evt = new PaymentCancelledEvent(
            MessageId: messageId,
            MessageName: "PaymentCancelledEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Payment",
            ConsumedBy: new[] { "Notification" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentCancelledEventPayload(
                TransactionId: transactionId,
                IdempotencyKey: "idem-cancelled",
                Amount: 1200,
                Currency: "THB",
                CustomerId: customerId,
                OrderId: "ORD-CANCELLED",
                ProviderName: "omise",
                Reason: "Customer cancelled checkout",
                ProviderEventCode: "charge.cancelled",
                CancelledAt: DateTimeOffset.UtcNow));

        var mockContext = new Mock<ConsumeContext<PaymentCancelledEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(mockContext.Object);

        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal("received", logs[0].Status);
        Assert.Equal(customerId, logs[0].UserId);
        Assert.Equal($"payment-{transactionId}", logs[0].RecipientIdentifier);
        Assert.Contains("Customer cancelled checkout", logs[0].MessageContent);

        publishEndpoint.Verify(
            p => p.Publish(
                It.Is<NotificationEvent>(notificationEvent =>
                    notificationEvent.CausationId == messageId &&
                    notificationEvent.CorrelationId == evt.CorrelationId &&
                    notificationEvent.Payload.NotificationType == "PaymentCancelled" &&
                    notificationEvent.Payload.Priority == "Critical" &&
                    notificationEvent.Payload.TemplateId == "payment-cancelled" &&
                    notificationEvent.Payload.TargetUsers.Count == 1 &&
                    notificationEvent.Payload.TargetUsers[0].UserId == customerId &&
                    notificationEvent.Payload.TargetUsers[0].UserType == "customer" &&
                    HasParameter(notificationEvent.Payload.Parameters, "amount", "1200.00 THB") &&
                    HasParameter(notificationEvent.Payload.Parameters, "reason", "Customer cancelled checkout") &&
                    HasParameter(notificationEvent.Payload.Parameters, "transactionId", transactionId.ToString()) &&
                    HasParameter(notificationEvent.Payload.Parameters, "providerEventCode", "charge.cancelled")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_PaymentCancelledEvent_FormatsAmountsWithInvariantCulture()
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
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentCancelledEventConsumer>>();
            var publishEndpoint = new Mock<IPublishEndpoint>();
            var consumer = new PaymentCancelledEventConsumer(logger, context, publishEndpoint.Object);

            var messageId = Guid.NewGuid();
            var customerId = Guid.NewGuid().ToString();
            var transactionId = Guid.NewGuid();
            var evt = new PaymentCancelledEvent(
                MessageId: messageId,
                MessageName: "PaymentCancelledEvent",
                MessageType: MessageType.Event,
                MessageVersion: "1.0",
                PublishedBy: "Payment",
                ConsumedBy: new[] { "Notification" },
                CorrelationId: Guid.NewGuid(),
                CausationId: null,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: true,
                Payload: new PaymentCancelledEventPayload(
                    TransactionId: transactionId,
                    IdempotencyKey: "idem-cancelled-invariant",
                    Amount: 1200.25,
                    Currency: "THB",
                    CustomerId: customerId,
                    OrderId: "ORD-CANCELLED-INVARIANT",
                    ProviderName: "omise",
                    Reason: "Customer cancelled checkout",
                    ProviderEventCode: "charge.cancelled",
                    CancelledAt: DateTimeOffset.UtcNow));

            var mockContext = new Mock<ConsumeContext<PaymentCancelledEvent>>();
            mockContext.Setup(m => m.Message).Returns(evt);
            mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

            await consumer.Consume(mockContext.Object);

            var log = await context.DeliveryLogs.SingleAsync(l => l.EventId == messageId.ToString());
            Assert.Contains("Amount 1200.25 THB", log.MessageContent);
            Assert.DoesNotContain("1200,25", log.MessageContent);

            publishEndpoint.Verify(
                p => p.Publish(
                    It.Is<NotificationEvent>(notificationEvent =>
                        notificationEvent.Payload.NotificationType == "PaymentCancelled" &&
                        HasParameter(notificationEvent.Payload.Parameters, "amount", "1200.25 THB")),
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
    public async Task Consume_PaymentCancelledEvent_WhenTransactionAlreadyReceived_ShouldNotPublishDuplicateNotification()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentCancelledEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new PaymentCancelledEventConsumer(logger, context, publishEndpoint.Object);

        var customerId = Guid.NewGuid().ToString();
        var transactionId = Guid.NewGuid();
        context.DeliveryLogs.Add(new DeliveryLog
        {
            EventId = Guid.NewGuid().ToString(),
            UserId = customerId,
            ChannelType = "rabbitmq-event",
            RecipientIdentifier = $"payment-{transactionId}",
            Status = "received",
            MessageContent = "Payment cancelled: Order ORD-CANCELLED, Amount 1200 THB, Reason: Customer cancelled checkout",
            AttemptNumber = 1,
            DeliveredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var messageId = Guid.NewGuid();
        var evt = new PaymentCancelledEvent(
            MessageId: messageId,
            MessageName: "PaymentCancelledEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Payment",
            ConsumedBy: new[] { "Notification" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentCancelledEventPayload(
                TransactionId: transactionId,
                IdempotencyKey: "idem-cancelled",
                Amount: 1200,
                Currency: "THB",
                CustomerId: customerId,
                OrderId: "ORD-CANCELLED",
                ProviderName: "omise",
                Reason: "Customer cancelled checkout",
                ProviderEventCode: "charge.cancelled",
                CancelledAt: DateTimeOffset.UtcNow));

        var mockContext = new Mock<ConsumeContext<PaymentCancelledEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(mockContext.Object);

        var logs = await context.DeliveryLogs
            .Where(l => l.RecipientIdentifier == $"payment-{transactionId}")
            .ToListAsync();
        Assert.Single(logs);
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_PaymentCancelledEvent_WhenNotRoutedToNotificationService_ShouldSkipNotificationAndDeliveryLog()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentCancelledEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new PaymentCancelledEventConsumer(logger, context, publishEndpoint.Object);

        var messageId = Guid.NewGuid();
        var evt = new PaymentCancelledEvent(
            MessageId: messageId,
            MessageName: "PaymentCancelledEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Payment",
            ConsumedBy: new[] { "QuoteEngine" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentCancelledEventPayload(
                TransactionId: Guid.NewGuid(),
                IdempotencyKey: "idem-cancelled-skip",
                Amount: 1200,
                Currency: "THB",
                CustomerId: Guid.NewGuid().ToString(),
                OrderId: "ORD-CANCELLED",
                ProviderName: "omise",
                Reason: "Customer cancelled checkout",
                ProviderEventCode: "charge.cancelled",
                CancelledAt: DateTimeOffset.UtcNow));

        var mockContext = new Mock<ConsumeContext<PaymentCancelledEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(mockContext.Object);

        Assert.False(await context.DeliveryLogs.AnyAsync(l => l.EventId == messageId.ToString()));
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_PaymentExpiredEvent_ShouldPublishCustomerNotificationAndCreateDeliveryLog()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentExpiredEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new PaymentExpiredEventConsumer(logger, context, publishEndpoint.Object);

        var messageId = Guid.NewGuid();
        var customerId = Guid.NewGuid().ToString();
        var transactionId = Guid.NewGuid();
        var evt = new PaymentExpiredEvent(
            MessageId: messageId,
            MessageName: "PaymentExpiredEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Payment",
            ConsumedBy: new[] { "Notification" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentExpiredEventPayload(
                TransactionId: transactionId,
                IdempotencyKey: "idem-expired",
                Amount: 990,
                Currency: "THB",
                CustomerId: customerId,
                OrderId: "ORD-EXPIRED",
                ProviderName: "stripe",
                Reason: "Checkout session expired",
                ProviderEventCode: "checkout.session.expired",
                ExpiredAt: DateTimeOffset.UtcNow));

        var mockContext = new Mock<ConsumeContext<PaymentExpiredEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(mockContext.Object);

        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal("received", logs[0].Status);
        Assert.Equal(customerId, logs[0].UserId);
        Assert.Equal($"payment-{transactionId}", logs[0].RecipientIdentifier);
        Assert.Contains("Checkout session expired", logs[0].MessageContent);

        publishEndpoint.Verify(
            p => p.Publish(
                It.Is<NotificationEvent>(notificationEvent =>
                    notificationEvent.CausationId == messageId &&
                    notificationEvent.CorrelationId == evt.CorrelationId &&
                    notificationEvent.Payload.NotificationType == "PaymentExpired" &&
                    notificationEvent.Payload.Priority == "Critical" &&
                    notificationEvent.Payload.TemplateId == "payment-expired" &&
                    notificationEvent.Payload.TargetUsers.Count == 1 &&
                    notificationEvent.Payload.TargetUsers[0].UserId == customerId &&
                    notificationEvent.Payload.TargetUsers[0].UserType == "customer" &&
                    HasParameter(notificationEvent.Payload.Parameters, "amount", "990.00 THB") &&
                    HasParameter(notificationEvent.Payload.Parameters, "reason", "Checkout session expired") &&
                    HasParameter(notificationEvent.Payload.Parameters, "transactionId", transactionId.ToString()) &&
                    HasParameter(notificationEvent.Payload.Parameters, "providerEventCode", "checkout.session.expired")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_PaymentExpiredEvent_FormatsAmountsWithInvariantCulture()
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
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentExpiredEventConsumer>>();
            var publishEndpoint = new Mock<IPublishEndpoint>();
            var consumer = new PaymentExpiredEventConsumer(logger, context, publishEndpoint.Object);

            var messageId = Guid.NewGuid();
            var customerId = Guid.NewGuid().ToString();
            var transactionId = Guid.NewGuid();
            var evt = new PaymentExpiredEvent(
                MessageId: messageId,
                MessageName: "PaymentExpiredEvent",
                MessageType: MessageType.Event,
                MessageVersion: "1.0",
                PublishedBy: "Payment",
                ConsumedBy: new[] { "Notification" },
                CorrelationId: Guid.NewGuid(),
                CausationId: null,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: true,
                Payload: new PaymentExpiredEventPayload(
                    TransactionId: transactionId,
                    IdempotencyKey: "idem-expired-invariant",
                    Amount: 990.50,
                    Currency: "THB",
                    CustomerId: customerId,
                    OrderId: "ORD-EXPIRED-INVARIANT",
                    ProviderName: "stripe",
                    Reason: "Checkout session expired",
                    ProviderEventCode: "checkout.session.expired",
                    ExpiredAt: DateTimeOffset.UtcNow));

            var mockContext = new Mock<ConsumeContext<PaymentExpiredEvent>>();
            mockContext.Setup(m => m.Message).Returns(evt);
            mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

            await consumer.Consume(mockContext.Object);

            var log = await context.DeliveryLogs.SingleAsync(l => l.EventId == messageId.ToString());
            Assert.Contains("Amount 990.50 THB", log.MessageContent);
            Assert.DoesNotContain("990,50", log.MessageContent);

            publishEndpoint.Verify(
                p => p.Publish(
                    It.Is<NotificationEvent>(notificationEvent =>
                        notificationEvent.Payload.NotificationType == "PaymentExpired" &&
                        HasParameter(notificationEvent.Payload.Parameters, "amount", "990.50 THB")),
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
    public async Task Consume_PaymentExpiredEvent_WhenTransactionAlreadyReceived_ShouldNotPublishDuplicateNotification()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentExpiredEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new PaymentExpiredEventConsumer(logger, context, publishEndpoint.Object);

        var customerId = Guid.NewGuid().ToString();
        var transactionId = Guid.NewGuid();
        context.DeliveryLogs.Add(new DeliveryLog
        {
            EventId = Guid.NewGuid().ToString(),
            UserId = customerId,
            ChannelType = "rabbitmq-event",
            RecipientIdentifier = $"payment-{transactionId}",
            Status = "received",
            MessageContent = "Payment expired: Order ORD-EXPIRED, Amount 990 THB, Reason: Checkout session expired",
            AttemptNumber = 1,
            DeliveredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var messageId = Guid.NewGuid();
        var evt = new PaymentExpiredEvent(
            MessageId: messageId,
            MessageName: "PaymentExpiredEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Payment",
            ConsumedBy: new[] { "Notification" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentExpiredEventPayload(
                TransactionId: transactionId,
                IdempotencyKey: "idem-expired",
                Amount: 990,
                Currency: "THB",
                CustomerId: customerId,
                OrderId: "ORD-EXPIRED",
                ProviderName: "stripe",
                Reason: "Checkout session expired",
                ProviderEventCode: "checkout.session.expired",
                ExpiredAt: DateTimeOffset.UtcNow));

        var mockContext = new Mock<ConsumeContext<PaymentExpiredEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(mockContext.Object);

        var logs = await context.DeliveryLogs
            .Where(l => l.RecipientIdentifier == $"payment-{transactionId}")
            .ToListAsync();
        Assert.Single(logs);
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_PaymentExpiredEvent_WhenNotRoutedToNotificationService_ShouldSkipNotificationAndDeliveryLog()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentExpiredEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new PaymentExpiredEventConsumer(logger, context, publishEndpoint.Object);

        var messageId = Guid.NewGuid();
        var evt = new PaymentExpiredEvent(
            MessageId: messageId,
            MessageName: "PaymentExpiredEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Payment",
            ConsumedBy: new[] { "QuoteEngine" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentExpiredEventPayload(
                TransactionId: Guid.NewGuid(),
                IdempotencyKey: "idem-expired-skip",
                Amount: 990,
                Currency: "THB",
                CustomerId: Guid.NewGuid().ToString(),
                OrderId: "ORD-EXPIRED",
                ProviderName: "stripe",
                Reason: "Checkout session expired",
                ProviderEventCode: "checkout.session.expired",
                ExpiredAt: DateTimeOffset.UtcNow));

        var mockContext = new Mock<ConsumeContext<PaymentExpiredEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(mockContext.Object);

        Assert.False(await context.DeliveryLogs.AnyAsync(l => l.EventId == messageId.ToString()));
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_PaymentPendingEvent_ShouldCreateDeliveryLogWithoutCustomerNotification()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentPendingEventConsumer>>();
        var consumer = new PaymentPendingEventConsumer(logger, context);

        var messageId = Guid.NewGuid();
        var customerId = Guid.NewGuid().ToString();
        var transactionId = Guid.NewGuid();
        var evt = new PaymentPendingEvent(
            MessageId: messageId,
            MessageName: "PaymentPendingEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Payment",
            ConsumedBy: new[] { "Notification" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentPendingEventPayload(
                TransactionId: transactionId,
                IdempotencyKey: "idem-pending",
                Amount: 450,
                Currency: "THB",
                CustomerId: customerId,
                OrderId: "ORD-PENDING",
                ProviderName: "omise",
                ProviderEventCode: "charge.pending",
                PendingAt: DateTimeOffset.UtcNow));

        var mockContext = new Mock<ConsumeContext<PaymentPendingEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(mockContext.Object);

        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal("received", logs[0].Status);
        Assert.Equal(customerId, logs[0].UserId);
        Assert.Equal($"payment-{transactionId}", logs[0].RecipientIdentifier);
        Assert.Contains("Payment pending", logs[0].MessageContent);
    }

    [Fact]
    public async Task Consume_PaymentPendingEvent_FormatsAuditAmountWithInvariantCulture()
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
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentPendingEventConsumer>>();
            var consumer = new PaymentPendingEventConsumer(logger, context);

            var messageId = Guid.NewGuid();
            var evt = new PaymentPendingEvent(
                MessageId: messageId,
                MessageName: "PaymentPendingEvent",
                MessageType: MessageType.Event,
                MessageVersion: "1.0",
                PublishedBy: "Payment",
                ConsumedBy: new[] { "Notification" },
                CorrelationId: Guid.NewGuid(),
                CausationId: null,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: true,
                Payload: new PaymentPendingEventPayload(
                    TransactionId: Guid.NewGuid(),
                    IdempotencyKey: "idem-pending-invariant",
                    Amount: 450.25,
                    Currency: "THB",
                    CustomerId: Guid.NewGuid().ToString(),
                    OrderId: "ORD-PENDING-INVARIANT",
                    ProviderName: "omise",
                    ProviderEventCode: "charge.pending",
                    PendingAt: DateTimeOffset.UtcNow));

            var mockContext = new Mock<ConsumeContext<PaymentPendingEvent>>();
            mockContext.Setup(m => m.Message).Returns(evt);
            mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

            await consumer.Consume(mockContext.Object);

            var log = await context.DeliveryLogs.SingleAsync(l => l.EventId == messageId.ToString());
            Assert.Contains("Amount 450.25 THB", log.MessageContent);
            Assert.DoesNotContain("450,25", log.MessageContent);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public async Task Consume_PaymentPendingEvent_WhenNotRoutedToNotificationService_ShouldSkipDeliveryLog()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentPendingEventConsumer>>();
        var consumer = new PaymentPendingEventConsumer(logger, context);

        var messageId = Guid.NewGuid();
        var evt = new PaymentPendingEvent(
            MessageId: messageId,
            MessageName: "PaymentPendingEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Payment",
            ConsumedBy: new[] { "QuoteEngine" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentPendingEventPayload(
                TransactionId: Guid.NewGuid(),
                IdempotencyKey: "idem-pending-skip",
                Amount: 450,
                Currency: "THB",
                CustomerId: Guid.NewGuid().ToString(),
                OrderId: "ORD-PENDING",
                ProviderName: "omise",
                ProviderEventCode: "charge.pending",
                PendingAt: DateTimeOffset.UtcNow));

        var mockContext = new Mock<ConsumeContext<PaymentPendingEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(mockContext.Object);

        Assert.False(await context.DeliveryLogs.AnyAsync(l => l.EventId == messageId.ToString()));
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
