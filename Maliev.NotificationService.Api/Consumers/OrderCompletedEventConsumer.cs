using Maliev.MessagingContracts.Contracts.Orders;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;

namespace Maliev.NotificationService.Api.Consumers
{
    public class OrderCompletedEventConsumer : IConsumer<OrderCompletedEvent>
    {
        private readonly ILogger<OrderCompletedEventConsumer> _logger;
        private readonly NotificationDbContext _dbContext;

        public OrderCompletedEventConsumer(
            ILogger<OrderCompletedEventConsumer> logger,
            NotificationDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task Consume(ConsumeContext<OrderCompletedEvent> context)
        {
            var payload = context.Message.Payload;
            _logger.LogInformation(
                "[NotificationService] Received OrderCompletedEvent for Order {OrderNumber}, JobSucceeded: {JobSucceeded}",
                payload.OrderNumber,
                payload.JobSucceeded);

            // Create delivery log entry to track that we received this event
            var deliveryLog = new DeliveryLog
            {
                EventId = context.Message.MessageId.ToString(),
                UserId = payload.OrderId.ToString(), // OrderId as user identifier
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
                "[NotificationService] Created delivery log {DeliveryLogId} for OrderCompletedEvent {MessageId}",
                deliveryLog.Id,
                context.Message.MessageId);

            // Actual notification logic (e.g., email/SMS confirming job closure) would go here.
        }
    }
}
