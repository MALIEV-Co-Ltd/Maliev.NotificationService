using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.NotificationService.Api.Consumers
{
    public class PaymentFailedEventConsumer : IConsumer<PaymentFailedEvent>
    {
        private readonly ILogger<PaymentFailedEventConsumer> _logger;
        private readonly NotificationDbContext _dbContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public PaymentFailedEventConsumer(
            ILogger<PaymentFailedEventConsumer> logger,
            NotificationDbContext dbContext,
            IPublishEndpoint publishEndpoint)
        {
            _logger = logger;
            _dbContext = dbContext;
            _publishEndpoint = publishEndpoint;
        }

        public async Task Consume(ConsumeContext<PaymentFailedEvent> context)
        {
            var payload = context.Message.Payload;
            if (payload is null)
            {
                _logger.LogWarning("[NotificationService] PaymentFailedEvent received without payload; skipping");
                return;
            }

            if (!NotificationConsumerRouting.IsRoutedToNotificationService(context.Message.ConsumedBy))
            {
                _logger.LogDebug(
                    "[NotificationService] Skipping PaymentFailedEvent {MessageId} because it is not routed to NotificationService",
                    context.Message.MessageId);
                return;
            }

            _logger.LogWarning(
                "[NotificationService] Received PaymentFailedEvent for Order ID: {OrderId}, Transaction ID: {TransactionId}. Preparing failure notification.",
                payload.OrderId,
                payload.TransactionId);

            var formattedAmount = PaymentNotificationFormatting.FormatAmount(payload.Amount, payload.Currency);
            var eventId = context.Message.MessageId.ToString();
            var paymentRecipientIdentifier = $"payment-{payload.TransactionId}";
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
                    "[NotificationService] Skipping duplicate PaymentFailedEvent {MessageId} for customer {CustomerId}",
                    context.Message.MessageId,
                    payload.CustomerId);
                return;
            }

            var notificationEvent = new NotificationEvent(
                MessageId: Guid.NewGuid(),
                MessageName: "PaymentFailedNotification",
                MessageType: MessageType.Event,
                MessageVersion: "1.0",
                PublishedBy: "NotificationService",
                ConsumedBy: new[] { "NotificationService" },
                CorrelationId: context.Message.CorrelationId,
                CausationId: context.Message.MessageId,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: false,
                Payload: new NotificationEventPayload(
                    NotificationType: "PaymentFailure",
                    Priority: "Critical",
                    TargetUsers: new[]
                    {
                        new NotificationEventPayloadTargetUsersItem(
                            payload.CustomerId,
                            "customer")
                    },
                    TemplateId: "payment-failed",
                    Parameters: new Dictionary<string, object>
                    {
                        ["name"] = "Customer",
                        ["amount"] = formattedAmount,
                        ["reason"] = payload.ErrorMessage,
                        ["transactionId"] = payload.TransactionId.ToString(),
                        ["providerErrorCode"] = payload.ProviderErrorCode
                    },
                    Metadata: new NotificationEventPayloadMetadata(
                        Language: "en",
                        Source: "PaymentService")
                )
            );

            await _publishEndpoint.Publish(notificationEvent, context.CancellationToken);

            var deliveryLog = new DeliveryLog
            {
                EventId = eventId,
                UserId = payload.CustomerId,
                ChannelType = "rabbitmq-event",
                RecipientIdentifier = paymentRecipientIdentifier,
                Status = "received",
                MessageContent = $"Payment failed: Order {payload.OrderId}, Amount {formattedAmount}, Reason: {payload.ErrorMessage}",
                AttemptNumber = 1,
                DeliveredAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.DeliveryLogs.Add(deliveryLog);
            await _dbContext.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation(
                "[NotificationService] Published customer failure notification and created delivery log {DeliveryLogId} for PaymentFailedEvent {MessageId}",
                deliveryLog.Id,
                context.Message.MessageId);
        }
    }
}
