using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;

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
            _logger.LogInformation(
                "[NotificationService] Received PaymentCompletedEvent for Order ID: {OrderId}, Payment ID: {PaymentId}. Preparing to send notification.",
                payload.OrderId,
                payload.PaymentId);

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

            // Create delivery log entry to track that we received this event
            var deliveryLog = new DeliveryLog
            {
                EventId = context.Message.MessageId.ToString(),
                UserId = payload.CustomerId,
                ChannelType = "rabbitmq-event",
                RecipientIdentifier = $"payment-{payload.PaymentId}",
                Status = "received",
                MessageContent = $"Payment completed: Order {payload.OrderNumber}, Amount {payload.Amount} {payload.Currency}",
                AttemptNumber = 1,
                DeliveredAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.DeliveryLogs.Add(deliveryLog);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "[NotificationService] Published customer notification and created delivery log {DeliveryLogId} for PaymentCompletedEvent {MessageId}",
                deliveryLog.Id,
                context.Message.MessageId);
        }
    }
}
