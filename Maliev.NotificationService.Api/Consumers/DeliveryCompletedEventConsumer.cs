using System.Globalization;
using Maliev.MessagingContracts.Contracts.Delivery;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.NotificationService.Api.Consumers
{
    public class DeliveryCompletedEventConsumer : IConsumer<DeliveryCompletedEvent>
    {
        private const string ServiceName = "NotificationService";
        private readonly ILogger<DeliveryCompletedEventConsumer> _logger;
        private readonly NotificationDbContext _dbContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public DeliveryCompletedEventConsumer(
            ILogger<DeliveryCompletedEventConsumer> logger,
            NotificationDbContext dbContext,
            IPublishEndpoint publishEndpoint)
        {
            _logger = logger;
            _dbContext = dbContext;
            _publishEndpoint = publishEndpoint;
        }

        public async Task Consume(ConsumeContext<DeliveryCompletedEvent> context)
        {
            if (!IsRoutedToNotificationService(context.Message.ConsumedBy))
            {
                _logger.LogDebug(
                    "[NotificationService] Ignoring DeliveryCompletedEvent {MessageId} for consumers {ConsumedBy}",
                    context.Message.MessageId,
                    string.Join(",", context.Message.ConsumedBy));
                return;
            }

            var payload = context.Message.Payload;
            _logger.LogInformation(
                "[NotificationService] Received DeliveryCompletedEvent for DeliveryNote {DeliveryNoteId}, Order {OrderId}",
                payload.DeliveryNoteId,
                payload.OrderId);

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
                    "[NotificationService] Skipping duplicate DeliveryCompletedEvent {MessageId} for customer {CustomerId}",
                    context.Message.MessageId,
                    payload.CustomerId);
                return;
            }

            var orderReference = string.IsNullOrWhiteSpace(payload.OrderId)
                ? payload.DeliveryNoteId
                : payload.OrderId;
            var completedAt = payload.CompletedAt.ToString("O", CultureInfo.InvariantCulture);
            var notificationEvent = new NotificationEvent(
                MessageId: Guid.NewGuid(),
                MessageName: "DeliveryCompletedNotification",
                MessageType: MessageType.Event,
                MessageVersion: "1.0",
                PublishedBy: ServiceName,
                ConsumedBy: [ServiceName],
                CorrelationId: context.Message.CorrelationId,
                CausationId: context.Message.MessageId,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: false,
                Payload: new NotificationEventPayload(
                    NotificationType: "DeliveryCompleted",
                    Priority: "High",
                    TargetUsers:
                    [
                        new NotificationEventPayloadTargetUsersItem(
                            customerId,
                            "customer")
                    ],
                    TemplateId: "delivery-completed",
                    Parameters: new Dictionary<string, object>
                    {
                        ["name"] = "Customer",
                        ["orderId"] = orderReference,
                        ["deliveryNoteId"] = payload.DeliveryNoteId,
                        ["completedAt"] = completedAt,
                        ["receivedByName"] = payload.ReceivedByName
                    },
                    Metadata: new NotificationEventPayloadMetadata(
                        Language: "en",
                        Source: "DeliveryService")
                )
            );

            await _publishEndpoint.Publish(notificationEvent, context.CancellationToken);

            var deliveryLog = new DeliveryLog
            {
                EventId = eventId,
                UserId = customerId,
                ChannelType = "rabbitmq-event",
                RecipientIdentifier = $"delivery-note-{payload.DeliveryNoteId}",
                Status = "received",
                MessageContent = $"Delivery completed: {payload.DeliveryNoteId}, Order: {orderReference}, ReceivedBy: {payload.ReceivedByName}, CompletedAt: {completedAt}",
                AttemptNumber = 1,
                DeliveredAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.DeliveryLogs.Add(deliveryLog);
            await _dbContext.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation(
                "[NotificationService] Published customer notification and created delivery log {DeliveryLogId} for DeliveryCompletedEvent {MessageId}",
                deliveryLog.Id,
                context.Message.MessageId);
        }

        private static bool IsRoutedToNotificationService(IReadOnlyList<string> consumedBy)
        {
            return consumedBy.Any(consumer =>
                string.Equals(consumer, ServiceName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
