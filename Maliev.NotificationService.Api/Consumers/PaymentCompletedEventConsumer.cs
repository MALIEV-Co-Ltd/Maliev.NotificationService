using MassTransit;
using Maliev.MessagingContracts;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Maliev.NotificationService.Data;
using Maliev.NotificationService.Data.Entities;

namespace Maliev.NotificationService.Api.Consumers
{
    public class PaymentCompletedEventConsumer : IConsumer<PaymentCompletedEvent>
    {
        private readonly ILogger<PaymentCompletedEventConsumer> _logger;
        private readonly NotificationDbContext _dbContext;

        public PaymentCompletedEventConsumer(
            ILogger<PaymentCompletedEventConsumer> logger,
            NotificationDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task Consume(ConsumeContext<PaymentCompletedEvent> context)
        {
            var payload = context.Message.Payload;
            _logger.LogInformation(
                "[NotificationService] Received PaymentCompletedEvent for Order ID: {OrderId}, Payment ID: {PaymentId}. Preparing to send notification.",
                payload.OrderId,
                payload.PaymentId);

            // Create delivery log entry to track that we received this event
            var deliveryLog = new DeliveryLog
            {
                EventId = context.Message.MessageId.ToString(),
                UserId = payload.OrderId.ToString(), // Using OrderId as user identifier for this event
                ChannelType = "rabbitmq-event",
                RecipientIdentifier = $"payment-{payload.PaymentId}",
                Status = "received",
                MessageContent = $"Payment completed: Order {payload.OrderId}, Amount {payload.Amount} {payload.Currency}",
                AttemptNumber = 1,
                DeliveredAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.DeliveryLogs.Add(deliveryLog);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "[NotificationService] Created delivery log {DeliveryLogId} for PaymentCompletedEvent {MessageId}",
                deliveryLog.Id,
                context.Message.MessageId);

            // Actual notification logic (e.g., email) would go here.
        }
    }
}
