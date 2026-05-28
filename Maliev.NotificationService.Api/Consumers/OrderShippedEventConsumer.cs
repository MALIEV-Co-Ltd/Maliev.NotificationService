using Maliev.MessagingContracts.Contracts.Orders;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;

namespace Maliev.NotificationService.Api.Consumers
{
    public class OrderShippedEventConsumer : IConsumer<OrderShippedEvent>
    {
        private readonly ILogger<OrderShippedEventConsumer> _logger;
        private readonly NotificationDbContext _dbContext;

        public OrderShippedEventConsumer(
            ILogger<OrderShippedEventConsumer> logger,
            NotificationDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task Consume(ConsumeContext<OrderShippedEvent> context)
        {
            var payload = context.Message.Payload;
            _logger.LogInformation(
                "[NotificationService] Received OrderShippedEvent for Order {OrderNumber}, TrackingNumber: {TrackingNumber}",
                payload.OrderNumber,
                payload.TrackingNumber);

            // Create delivery log entry to track that we received this event
            var deliveryLog = new DeliveryLog
            {
                EventId = context.Message.MessageId.ToString(),
                UserId = payload.OrderId.ToString(), // OrderId as user identifier (CustomerId not present in payload)
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
                "[NotificationService] Created delivery log {DeliveryLogId} for OrderShippedEvent {MessageId}",
                deliveryLog.Id,
                context.Message.MessageId);

            // Actual notification logic (e.g., email/SMS with tracking info) would go here.
        }
    }
}
