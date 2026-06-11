using Maliev.MessagingContracts.Contracts.Jobs;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.NotificationService.Api.Consumers
{
    public class JobStatusChangedEventConsumer : IConsumer<JobStatusChangedEvent>
    {
        private readonly ILogger<JobStatusChangedEventConsumer> _logger;
        private readonly NotificationDbContext _dbContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public JobStatusChangedEventConsumer(
            ILogger<JobStatusChangedEventConsumer> logger,
            NotificationDbContext dbContext,
            IPublishEndpoint publishEndpoint)
        {
            _logger = logger;
            _dbContext = dbContext;
            _publishEndpoint = publishEndpoint;
        }

        public async Task Consume(ConsumeContext<JobStatusChangedEvent> context)
        {
            var payload = context.Message.Payload;
            if (!string.Equals(payload.NewStatus, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "[NotificationService] Ignoring JobStatusChangedEvent for job {JobId} with status {Status}",
                    payload.JobId,
                    payload.NewStatus);
                return;
            }

            var eventId = context.Message.MessageId.ToString();
            var alreadyReceived = await _dbContext.DeliveryLogs
                .AsNoTracking()
                .AnyAsync(
                    log => log.EventId == eventId && log.UserId == NotificationBootstrapData.OperationsInboxUserId,
                    context.CancellationToken);

            if (alreadyReceived)
            {
                _logger.LogInformation(
                    "[NotificationService] Skipping duplicate completed job notification for event {MessageId}, job {JobId}",
                    context.Message.MessageId,
                    payload.JobId);
                return;
            }

            var assignedMachineId = string.IsNullOrWhiteSpace(payload.AssignedMachineId)
                ? "Unassigned"
                : payload.AssignedMachineId;

            var notificationEvent = new NotificationEvent(
                MessageId: Guid.NewGuid(),
                MessageName: "JobCompletedQcReadyNotification",
                MessageType: MessageType.Event,
                MessageVersion: "1.0",
                PublishedBy: "NotificationService",
                ConsumedBy: new[] { "NotificationService" },
                CorrelationId: context.Message.CorrelationId,
                CausationId: context.Message.MessageId,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: false,
                Payload: new NotificationEventPayload(
                    NotificationType: "JobCompletedQcReady",
                    Priority: "High",
                    TargetUsers: new[]
                    {
                        new NotificationEventPayloadTargetUsersItem(
                            NotificationBootstrapData.OperationsInboxUserId,
                            "staff")
                    },
                    TemplateId: "operations-job-completed-qc-ready",
                    Parameters: new Dictionary<string, object>
                    {
                        ["jobId"] = payload.JobId.ToString(),
                        ["orderId"] = payload.OrderId.ToString(),
                        ["technology"] = payload.Technology,
                        ["assignedMachineId"] = assignedMachineId,
                        ["completedAt"] = payload.ChangedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                        ["changedBy"] = payload.ChangedBy
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
                RecipientIdentifier = $"job-{payload.JobId}",
                Status = "received",
                MessageContent = $"Job completed and ready for QC intake: Job {payload.JobId}, Order {payload.OrderId}, Machine {assignedMachineId}",
                AttemptNumber = 1,
                DeliveredAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.DeliveryLogs.Add(deliveryLog);
            await _dbContext.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation(
                "[NotificationService] Published QC intake notification and created delivery log {DeliveryLogId} for completed job event {MessageId}",
                deliveryLog.Id,
                context.Message.MessageId);
        }
    }
}
