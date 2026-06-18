using Maliev.MessagingContracts.Contracts.Orders;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.NotificationService.Api.Consumers
{
    public class OrderShippedEventConsumer : IConsumer<OrderShippedEvent>
    {
        private readonly ILogger<OrderShippedEventConsumer> _logger;
        private readonly NotificationDbContext _dbContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public OrderShippedEventConsumer(
            ILogger<OrderShippedEventConsumer> logger,
            NotificationDbContext dbContext,
            IPublishEndpoint publishEndpoint)
        {
            _logger = logger;
            _dbContext = dbContext;
            _publishEndpoint = publishEndpoint;
        }

        public async Task Consume(ConsumeContext<OrderShippedEvent> context)
        {
            var payload = context.Message.Payload;
            if (!NotificationConsumerRouting.IsRoutedToNotificationService(context.Message.ConsumedBy))
            {
                _logger.LogDebug(
                    "[NotificationService] Ignoring OrderShippedEvent {MessageId} for consumers {ConsumedBy}",
                    context.Message.MessageId,
                    FormatConsumedBy(context.Message.ConsumedBy));
                return;
            }

            _logger.LogInformation(
                "[NotificationService] Received OrderShippedEvent for Order {OrderNumber}, TrackingNumber: {TrackingNumber}",
                payload.OrderNumber,
                payload.TrackingNumber);

            var eventId = context.Message.MessageId.ToString();
            var customerId = payload.CustomerId.ToString();
            var alreadyReceived = await _dbContext.DeliveryLogs
                .AsNoTracking()
                .AnyAsync(
                    log => log.EventId == eventId && log.UserId == customerId,
                    context.CancellationToken);

            if (alreadyReceived)
            {
                _logger.LogInformation(
                    "[NotificationService] Skipping duplicate OrderShippedEvent {MessageId} for customer {CustomerId}",
                    context.Message.MessageId,
                    payload.CustomerId);
                return;
            }

            var notificationEvent = new NotificationEvent(
                MessageId: Guid.NewGuid(),
                MessageName: "OrderShippedNotification",
                MessageType: MessageType.Event,
                MessageVersion: "1.0",
                PublishedBy: "NotificationService",
                ConsumedBy: new[] { "NotificationService" },
                CorrelationId: context.Message.CorrelationId,
                CausationId: context.Message.MessageId,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: false,
                Payload: new NotificationEventPayload(
                    NotificationType: "OrderShipped",
                    Priority: "High",
                    TargetUsers: new[]
                    {
                        new NotificationEventPayloadTargetUsersItem(
                            payload.CustomerId.ToString(),
                            "customer")
                    },
                    TemplateId: "order-shipped",
                    Parameters: new Dictionary<string, object>
                    {
                        ["name"] = "Customer",
                        ["orderId"] = payload.OrderNumber,
                        ["carrier"] = payload.Carrier ?? "Not available",
                        ["trackingNumber"] = payload.TrackingNumber ?? "Not available",
                        ["estimatedDeliveryDate"] = payload.EstimatedDeliveryDate?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? "Not available"
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
                EventId = eventId,
                UserId = customerId,
                ChannelType = "rabbitmq-event",
                RecipientIdentifier = $"order-{payload.OrderNumber}",
                Status = "received",
                MessageContent = $"Order shipped: {payload.OrderNumber}, Carrier: {payload.Carrier}, Tracking: {payload.TrackingNumber}",
                AttemptNumber = 1,
                DeliveredAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.DeliveryLogs.Add(deliveryLog);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "[NotificationService] Published customer notification and created delivery log {DeliveryLogId} for OrderShippedEvent {MessageId}",
                deliveryLog.Id,
                context.Message.MessageId);
        }

        private static string FormatConsumedBy(IReadOnlyList<string>? consumedBy)
        {
            return consumedBy is null ? "<none>" : string.Join(",", consumedBy);
        }
    }
}
