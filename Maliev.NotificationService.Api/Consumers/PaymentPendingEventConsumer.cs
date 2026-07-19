using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.NotificationService.Api.Consumers;

/// <summary>
/// Consumes pending payment events for audit without sending customer notifications.
/// </summary>
public sealed class PaymentPendingEventConsumer : IConsumer<PaymentPendingEvent>
{
    private readonly ILogger<PaymentPendingEventConsumer> _logger;
    private readonly NotificationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentPendingEventConsumer"/> class.
    /// </summary>
    /// <param name="logger">The structured logger.</param>
    /// <param name="dbContext">The notification database context.</param>
    public PaymentPendingEventConsumer(
        ILogger<PaymentPendingEventConsumer> logger,
        NotificationDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<PaymentPendingEvent> context)
    {
        var payload = context.Message.Payload;
        if (payload is null)
        {
            _logger.LogWarning("[NotificationService] PaymentPendingEvent received without payload; skipping");
            return;
        }

        if (!NotificationConsumerRouting.IsRoutedToNotificationService(context.Message.ConsumedBy))
        {
            _logger.LogDebug(
                "[NotificationService] Skipping PaymentPendingEvent {MessageId} because it is not routed to NotificationService",
                context.Message.MessageId);
            return;
        }

        var eventId = context.Message.MessageId.ToString();
        var formattedAmount = PaymentNotificationFormatting.FormatAmount(payload.Amount, payload.Currency);
        var legacyPaymentRecipientIdentifier = $"payment-{payload.TransactionId}";
        var paymentRecipientIdentifier = $"payment-pending-{payload.TransactionId}";

        var alreadyReceived = await _dbContext.DeliveryLogs
            .AsNoTracking()
            .AnyAsync(
                log => log.UserId == payload.CustomerId
                    && log.Status == "received"
                    && (log.EventId == eventId
                        || log.RecipientIdentifier == paymentRecipientIdentifier
                        || (log.RecipientIdentifier == legacyPaymentRecipientIdentifier
                            && EF.Functions.Like(log.MessageContent, "Payment pending:%"))),
                context.CancellationToken);

        if (alreadyReceived)
        {
            _logger.LogInformation(
                "[NotificationService] Skipping duplicate PaymentPendingEvent {MessageId} for customer {CustomerId}",
                context.Message.MessageId,
                payload.CustomerId);
            return;
        }

        _dbContext.DeliveryLogs.Add(new DeliveryLog
        {
            EventId = eventId,
            UserId = payload.CustomerId,
            ChannelType = "rabbitmq-event",
            RecipientIdentifier = paymentRecipientIdentifier,
            Status = "received",
            MessageContent = $"Payment pending: Order {payload.OrderId}, Amount {formattedAmount}, Provider event: {payload.ProviderEventCode}",
            AttemptNumber = 1,
            DeliveredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
