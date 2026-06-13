using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.NotificationService.Api.Consumers;

/// <summary>
/// Consumes cancelled payment events and emits a customer action notification.
/// </summary>
public sealed class PaymentCancelledEventConsumer : IConsumer<PaymentCancelledEvent>
{
    private readonly ILogger<PaymentCancelledEventConsumer> _logger;
    private readonly NotificationDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentCancelledEventConsumer"/> class.
    /// </summary>
    /// <param name="logger">The structured logger.</param>
    /// <param name="dbContext">The notification database context.</param>
    /// <param name="publishEndpoint">The MassTransit publish endpoint.</param>
    public PaymentCancelledEventConsumer(
        ILogger<PaymentCancelledEventConsumer> logger,
        NotificationDbContext dbContext,
        IPublishEndpoint publishEndpoint)
    {
        _logger = logger;
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<PaymentCancelledEvent> context)
    {
        var payload = context.Message.Payload;
        if (payload is null)
        {
            _logger.LogWarning("[NotificationService] PaymentCancelledEvent received without payload; skipping");
            return;
        }

        var eventId = context.Message.MessageId.ToString();

        if (await HasReceivedEventAsync(eventId, payload.CustomerId, context.CancellationToken))
        {
            _logger.LogInformation(
                "[NotificationService] Skipping duplicate PaymentCancelledEvent {MessageId} for customer {CustomerId}",
                context.Message.MessageId,
                payload.CustomerId);
            return;
        }

        await _publishEndpoint.Publish(new NotificationEvent(
            MessageId: Guid.NewGuid(),
            MessageName: "PaymentCancelledNotification",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "NotificationService",
            ConsumedBy: new[] { "NotificationService" },
            CorrelationId: context.Message.CorrelationId,
            CausationId: context.Message.MessageId,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new NotificationEventPayload(
                NotificationType: "PaymentCancelled",
                Priority: "Critical",
                TargetUsers: new[]
                {
                    new NotificationEventPayloadTargetUsersItem(payload.CustomerId, "customer")
                },
                TemplateId: "payment-cancelled",
                Parameters: new Dictionary<string, object>
                {
                    ["name"] = "Customer",
                    ["amount"] = $"{payload.Amount:0.00} {payload.Currency}",
                    ["reason"] = payload.Reason,
                    ["transactionId"] = payload.TransactionId.ToString(),
                    ["providerEventCode"] = payload.ProviderEventCode
                },
                Metadata: new NotificationEventPayloadMetadata("en", "PaymentService"))
        ), context.CancellationToken);

        _dbContext.DeliveryLogs.Add(new DeliveryLog
        {
            EventId = eventId,
            UserId = payload.CustomerId,
            ChannelType = "rabbitmq-event",
            RecipientIdentifier = $"payment-{payload.TransactionId}",
            Status = "received",
            MessageContent = $"Payment cancelled: Order {payload.OrderId}, Amount {payload.Amount} {payload.Currency}, Reason: {payload.Reason}",
            AttemptNumber = 1,
            DeliveredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(context.CancellationToken);
    }

    private Task<bool> HasReceivedEventAsync(string eventId, string customerId, CancellationToken cancellationToken)
    {
        return _dbContext.DeliveryLogs
            .AsNoTracking()
            .AnyAsync(log => log.EventId == eventId && log.UserId == customerId, cancellationToken);
    }
}
