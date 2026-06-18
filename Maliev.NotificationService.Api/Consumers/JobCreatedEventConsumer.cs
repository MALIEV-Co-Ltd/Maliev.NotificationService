using Maliev.MessagingContracts.Contracts.Jobs;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.NotificationService.Api.Consumers
{
    /// <summary>
    /// Consumes production job creation events and notifies the operations inbox.
    /// </summary>
    public class JobCreatedEventConsumer : IConsumer<JobCreatedEvent>
    {
        private readonly ILogger<JobCreatedEventConsumer> _logger;
        private readonly NotificationDbContext _dbContext;
        private readonly IPublishEndpoint _publishEndpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="JobCreatedEventConsumer"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="dbContext">The notification database context.</param>
        /// <param name="publishEndpoint">The publish endpoint used to emit notification events.</param>
        public JobCreatedEventConsumer(
            ILogger<JobCreatedEventConsumer> logger,
            NotificationDbContext dbContext,
            IPublishEndpoint publishEndpoint)
        {
            _logger = logger;
            _dbContext = dbContext;
            _publishEndpoint = publishEndpoint;
        }

        /// <inheritdoc />
        public async Task Consume(ConsumeContext<JobCreatedEvent> context)
        {
            var payload = context.Message.Payload;
            if (payload is null)
            {
                _logger.LogWarning("[NotificationService] JobCreatedEvent received without payload; skipping");
                return;
            }

            if (!IsRoutedToNotificationService(context.Message))
            {
                _logger.LogDebug(
                    "[NotificationService] Ignoring untargeted JobCreatedEvent for job {JobId}",
                    payload.JobId);
                return;
            }

            var eventId = context.Message.MessageId.ToString();
            var recipientIdentifier = $"job-{payload.JobId}";
            var alreadyReceived = await _dbContext.DeliveryLogs
                .AsNoTracking()
                .AnyAsync(
                    log => log.UserId == NotificationBootstrapData.OperationsInboxUserId
                        && log.Status == "received"
                        && (log.EventId == eventId || log.RecipientIdentifier == recipientIdentifier),
                    context.CancellationToken);

            if (alreadyReceived)
            {
                _logger.LogInformation(
                    "[NotificationService] Skipping duplicate JobCreatedEvent {MessageId} for job {JobId}",
                    context.Message.MessageId,
                    payload.JobId);
                return;
            }

            var notificationEvent = new NotificationEvent(
                MessageId: Guid.NewGuid(),
                MessageName: "ProductionJobCreatedNotification",
                MessageType: MessageType.Event,
                MessageVersion: "1.0",
                PublishedBy: "NotificationService",
                ConsumedBy: new[] { "NotificationService" },
                CorrelationId: context.Message.CorrelationId,
                CausationId: context.Message.MessageId,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: false,
                Payload: new NotificationEventPayload(
                    NotificationType: "ProductionJobCreated",
                    Priority: "High",
                    TargetUsers: new[]
                    {
                        new NotificationEventPayloadTargetUsersItem(
                            NotificationBootstrapData.OperationsInboxUserId,
                            "staff")
                    },
                    TemplateId: "operations-job-created",
                    Parameters: new Dictionary<string, object>
                    {
                        ["jobId"] = payload.JobId.ToString(),
                        ["jobNumber"] = payload.JobNumber,
                        ["orderId"] = payload.OrderId.ToString(),
                        ["orderItemId"] = payload.OrderItemId.ToString(),
                        ["technology"] = payload.ProcessType,
                        ["createdAt"] = payload.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
                    },
                    Metadata: new NotificationEventPayloadMetadata(
                        Language: "en",
                        Source: "JobService")
                )
            );

            await _publishEndpoint.Publish(notificationEvent, context.CancellationToken);

            var deliveryLog = new DeliveryLog
            {
                EventId = eventId,
                UserId = NotificationBootstrapData.OperationsInboxUserId,
                ChannelType = "rabbitmq-event",
                RecipientIdentifier = recipientIdentifier,
                Status = "received",
                MessageContent = $"Production job ticket created: {payload.JobNumber}, Job {payload.JobId}, Order {payload.OrderId}, Technology {payload.ProcessType}",
                AttemptNumber = 1,
                DeliveredAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.DeliveryLogs.Add(deliveryLog);
            await _dbContext.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation(
                "[NotificationService] Published job ticket notification and created delivery log {DeliveryLogId} for JobCreatedEvent {MessageId}",
                deliveryLog.Id,
                context.Message.MessageId);
        }

        private static bool IsRoutedToNotificationService(JobCreatedEvent message)
        {
            return message.ConsumedBy?.Any(
                consumer => consumer.Equals("NotificationService", StringComparison.OrdinalIgnoreCase)) == true;
        }
    }
}
