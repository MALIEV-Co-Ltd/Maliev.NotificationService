using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.NotificationService.Api.Consumers
{
    public class PaymentCompletedEventConsumer : IConsumer<PaymentCompletedEvent>
    {
        private readonly ILogger<PaymentCompletedEventConsumer> _logger;
        private readonly NotificationDbContext _dbContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public PaymentCompletedEventConsumer(
            ILogger<PaymentCompletedEventConsumer> logger,
            NotificationDbContext dbContext,
            IPublishEndpoint publishEndpoint)
        {
            _logger = logger;
            _dbContext = dbContext;
            _publishEndpoint = publishEndpoint;
        }

        public async Task Consume(ConsumeContext<PaymentCompletedEvent> context)
        {
            var payload = context.Message.Payload;
            if (payload is null)
            {
                _logger.LogWarning("[NotificationService] PaymentCompletedEvent received without payload; skipping");
                return;
            }

            if (!NotificationConsumerRouting.IsRoutedToNotificationService(context.Message.ConsumedBy))
            {
                _logger.LogDebug(
                    "[NotificationService] Skipping PaymentCompletedEvent {MessageId} because it is not routed to NotificationService",
                    context.Message.MessageId);
                return;
            }

            _logger.LogInformation(
                "[NotificationService] Received PaymentCompletedEvent for Order ID: {OrderId}, Payment ID: {PaymentId}. Preparing to send notification.",
                payload.OrderId,
                payload.PaymentId);

            var eventId = context.Message.MessageId.ToString();
            var paymentRecipientIdentifier = $"payment-{payload.PaymentId}";
            var alreadyReceived = await _dbContext.DeliveryLogs
                .AsNoTracking()
                .AnyAsync(
                    log => log.UserId == payload.CustomerId
                        && log.Status == "received"
                        && (log.EventId == eventId || log.RecipientIdentifier == paymentRecipientIdentifier),
                    context.CancellationToken);

            if (alreadyReceived)
            {
                _logger.LogInformation(
                    "[NotificationService] Skipping duplicate PaymentCompletedEvent {MessageId} for customer {CustomerId}",
                    context.Message.MessageId,
                    payload.CustomerId);
                return;
            }

            var notificationEvent = new NotificationEvent(
                MessageId: Guid.NewGuid(),
                MessageName: "PaymentCompletedNotification",
                MessageType: MessageType.Event,
                MessageVersion: "1.0",
                PublishedBy: "NotificationService",
                ConsumedBy: new[] { "NotificationService" },
                CorrelationId: context.Message.CorrelationId,
                CausationId: context.Message.MessageId,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: false,
                Payload: new NotificationEventPayload(
                    NotificationType: "PaymentSuccess",
                    Priority: "Critical",
                    TargetUsers: new[]
                    {
                        new NotificationEventPayloadTargetUsersItem(
                            payload.CustomerId,
                            "customer")
                    },
                    TemplateId: "order-confirmed",
                    Parameters: new Dictionary<string, object>
                    {
                        ["name"] = "Customer",
                        ["orderId"] = payload.OrderNumber,
                        ["amount"] = $"{payload.Amount:0.00} {payload.Currency}",
                        ["paymentId"] = payload.PaymentId.ToString()
                    },
                    Metadata: new NotificationEventPayloadMetadata(
                        Language: "en",
                        Source: "PaymentService")
                )
            );

            await _publishEndpoint.Publish(notificationEvent, context.CancellationToken);

            var operationsNotificationEvent = new NotificationEvent(
                MessageId: Guid.NewGuid(),
                MessageName: "PaymentReceivedOperationsNotification",
                MessageType: MessageType.Event,
                MessageVersion: "1.0",
                PublishedBy: "NotificationService",
                ConsumedBy: new[] { "NotificationService" },
                CorrelationId: context.Message.CorrelationId,
                CausationId: context.Message.MessageId,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: false,
                Payload: new NotificationEventPayload(
                    NotificationType: "PaymentReceivedOperations",
                    Priority: "Critical",
                    TargetUsers: new[]
                    {
                        new NotificationEventPayloadTargetUsersItem(
                            NotificationBootstrapData.OperationsInboxUserId,
                            "staff")
                    },
                    TemplateId: "operations-payment-received",
                    Parameters: new Dictionary<string, object>
                    {
                        ["orderId"] = payload.OrderNumber,
                        ["amount"] = $"{payload.Amount:0.00} {payload.Currency}",
                        ["paymentId"] = payload.PaymentId.ToString(),
                        ["customerId"] = payload.CustomerId
                    },
                    Metadata: new NotificationEventPayloadMetadata(
                        Language: "en",
                        Source: "PaymentService")
                )
            );

            await _publishEndpoint.Publish(operationsNotificationEvent, context.CancellationToken);

            // Create delivery log entry to track that we received this event
            var deliveryLog = new DeliveryLog
            {
                EventId = eventId,
                UserId = payload.CustomerId,
                ChannelType = "rabbitmq-event",
                RecipientIdentifier = paymentRecipientIdentifier,
                Status = "received",
                MessageContent = $"Payment completed: Order {payload.OrderNumber}, Amount {payload.Amount} {payload.Currency}",
                AttemptNumber = 1,
                DeliveredAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.DeliveryLogs.Add(deliveryLog);
            await _dbContext.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation(
                "[NotificationService] Published customer notification and created delivery log {DeliveryLogId} for PaymentCompletedEvent {MessageId}",
                deliveryLog.Id,
                context.Message.MessageId);
        }
    }
}
