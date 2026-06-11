using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;

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
            _logger.LogWarning(
                "[NotificationService] Received PaymentFailedEvent for Order ID: {OrderId}, Transaction ID: {TransactionId}. Preparing failure notification.",
                payload.OrderId,
                payload.TransactionId);

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
                        ["amount"] = $"{payload.Amount:0.00} {payload.Currency}",
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
                EventId = context.Message.MessageId.ToString(),
                UserId = payload.CustomerId,
                ChannelType = "rabbitmq-event",
                RecipientIdentifier = $"payment-{payload.TransactionId}",
                Status = "received",
                MessageContent = $"Payment failed: Order {payload.OrderId}, Amount {payload.Amount} {payload.Currency}, Reason: {payload.ErrorMessage}",
                AttemptNumber = 1,
                DeliveredAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.DeliveryLogs.Add(deliveryLog);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "[NotificationService] Published customer failure notification and created delivery log {DeliveryLogId} for PaymentFailedEvent {MessageId}",
                deliveryLog.Id,
                context.Message.MessageId);
        }
    }
}
