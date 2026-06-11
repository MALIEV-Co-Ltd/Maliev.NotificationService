using Maliev.MessagingContracts.Contracts.Orders;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;

namespace Maliev.NotificationService.Api.Consumers
{
    public class OrderCompletedEventConsumer : IConsumer<OrderCompletedEvent>
    {
        private readonly ILogger<OrderCompletedEventConsumer> _logger;
        private readonly NotificationDbContext _dbContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public OrderCompletedEventConsumer(
            ILogger<OrderCompletedEventConsumer> logger,
            NotificationDbContext dbContext,
            IPublishEndpoint publishEndpoint)
        {
            _logger = logger;
            _dbContext = dbContext;
            _publishEndpoint = publishEndpoint;
        }

        public async Task Consume(ConsumeContext<OrderCompletedEvent> context)
        {
            var payload = context.Message.Payload;
            _logger.LogInformation(
                "[NotificationService] Received OrderCompletedEvent for Order {OrderNumber}, JobSucceeded: {JobSucceeded}",
                payload.OrderNumber,
                payload.JobSucceeded);

            var notificationEvent = new NotificationEvent(
                MessageId: Guid.NewGuid(),
                MessageName: "OrderCompletedNotification",
                MessageType: MessageType.Event,
                MessageVersion: "1.0",
                PublishedBy: "NotificationService",
                ConsumedBy: new[] { "NotificationService" },
                CorrelationId: context.Message.CorrelationId,
                CausationId: context.Message.MessageId,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: false,
                Payload: new NotificationEventPayload(
                    NotificationType: payload.JobSucceeded ? "OrderCompleted" : "OrderCompletionFailed",
                    Priority: payload.JobSucceeded ? "Normal" : "High",
                    TargetUsers: new[]
                    {
                        new NotificationEventPayloadTargetUsersItem(
                            payload.CustomerId.ToString(),
                            "customer")
                    },
                    TemplateId: payload.JobSucceeded ? "order-completed" : "order-completion-failed",
                    Parameters: new Dictionary<string, object>
                    {
                        ["name"] = "Customer",
                        ["orderId"] = payload.OrderNumber,
                        ["completedAt"] = payload.CompletedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                        ["jobSucceeded"] = payload.JobSucceeded.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    },
                    Metadata: new NotificationEventPayloadMetadata(
                        Language: "en",
                        Source: "OrderService")
                )
            );

            await _publishEndpoint.Publish(notificationEvent, context.CancellationToken);

            // Create delivery log entry to track that we received this event
            var deliveryLog = new DeliveryLog
            {
                EventId = context.Message.MessageId.ToString(),
                UserId = payload.CustomerId.ToString(),
                ChannelType = "rabbitmq-event",
                RecipientIdentifier = $"order-{payload.OrderNumber}",
                Status = "received",
                MessageContent = $"Order completed: {payload.OrderNumber}, JobSucceeded: {payload.JobSucceeded}, CompletedAt: {payload.CompletedAt:O}",
                AttemptNumber = 1,
                DeliveredAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.DeliveryLogs.Add(deliveryLog);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "[NotificationService] Published customer notification and created delivery log {DeliveryLogId} for OrderCompletedEvent {MessageId}",
                deliveryLog.Id,
                context.Message.MessageId);
        }
    }
}
