using Maliev.MessagingContracts.Generated;
using Maliev.NotificationService.Api.Models.Enums;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Data;
using Maliev.NotificationService.Data.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Maliev.NotificationService.Api.Consumers;

/// <summary>
/// MassTransit consumer for processing notification events.
/// Handles deduplication, routing, delivery, retry logic, and dead-letter queue routing.
/// </summary>
public class NotificationEventConsumer : IConsumer<NotificationEvent>
{
    private readonly IDeduplicationService _deduplicationService;
    private readonly INotificationRouter _notificationRouter;
    private readonly IRetryService _retryService;
    private readonly IAlertingService _alertingService;
    private readonly NotificationDbContext _dbContext;
    private readonly ILogger<NotificationEventConsumer> _logger;

    public NotificationEventConsumer(
        IDeduplicationService deduplicationService,
        INotificationRouter notificationRouter,
        IRetryService retryService,
        IAlertingService alertingService,
        NotificationDbContext dbContext,
        ILogger<NotificationEventConsumer> logger)
    {
        _deduplicationService = deduplicationService;
        _notificationRouter = notificationRouter;
        _retryService = retryService;
        _alertingService = alertingService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<NotificationEvent> context)
    {
        var notificationEvent = context.Message;
        var payload = notificationEvent.Payload;
        var cancellationToken = context.CancellationToken;

        try
        {
            _logger.LogInformation(
                "Processing notification event: EventId={EventId}, Type={Type}, Priority={Priority}, TargetUsers={UserCount}",
                notificationEvent.MessageId,
                payload.NotificationType,
                payload.Priority,
                payload.TargetUsers.Count);

            // Step 1: Check for duplicate events
            var isRetry = context.Headers.TryGetHeader("X-Is-Retry", out var isRetryObj) && isRetryObj?.ToString() == "true";

            if (isRetry)
            {
                _logger.LogInformation("Processing retry event (skipping deduplication): EventId={EventId}", notificationEvent.MessageId);
            }
            else
            {
                var isDuplicate = await _deduplicationService.IsDuplicateAsync(
                    notificationEvent.MessageId.ToString(),
                    notificationEvent.OccurredAtUtc,
                    cancellationToken);

                if (isDuplicate)
                {
                    _logger.LogWarning(
                        "Duplicate notification event detected: EventId={EventId}. Skipping processing.",
                        notificationEvent.MessageId);
                    return;
                }
            }

            // Step 2: Process each target user in parallel
            var processingTasks = payload.TargetUsers.Select(async targetUser =>
            {
                // Check if already delivered to this user for this event to avoid duplicates during retries
                var alreadyDelivered = await _dbContext.DeliveryLogs.AsNoTracking()
                    .AnyAsync(l => l.EventId == notificationEvent.MessageId.ToString()
                                && l.UserId == targetUser.UserId
                                && l.Status == "delivered", cancellationToken);

                if (!alreadyDelivered)
                {
                    await ProcessUserNotificationAsync(notificationEvent, targetUser, cancellationToken);
                }
                else
                {
                    _logger.LogInformation(
                        "User already notified for this event: UserId={UserId}, EventId={EventId}",
                        targetUser.UserId,
                        notificationEvent.MessageId);
                }
            });

            await Task.WhenAll(processingTasks);

            _logger.LogInformation(
                "Completed processing notification event: EventId={EventId}",
                notificationEvent.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error processing notification event: EventId={EventId}",
                notificationEvent.MessageId);
            throw;
        }
    }

    private async Task ProcessUserNotificationAsync(
        NotificationEvent notificationEvent,
        NotificationEventPayloadTargetUsersItem targetUser,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Routing notification for user: UserId={UserId}, EventId={EventId}",
                targetUser.UserId,
                notificationEvent.MessageId);

            // Step 3: Route notification to appropriate channel
            var routingResult = await _notificationRouter.RouteAsync(
                notificationEvent,
                targetUser,
                cancellationToken);

            // Step 4: Handle routing/delivery result
            if (routingResult.Success && routingResult.WasDelivered)
            {
                // Success - notification delivered
                _logger.LogInformation(
                    "Notification delivered successfully: EventId={EventId}, UserId={UserId}, Channel={Channel}",
                    notificationEvent.MessageId,
                    targetUser.UserId,
                    routingResult.SelectedChannel);

                await CreateDeliveryLogAsync(
                    notificationEvent,
                    targetUser,
                    routingResult,
                    DeliveryStatus.Delivered,
                    cancellationToken);
            }
            else if (routingResult.Success && !string.IsNullOrEmpty(routingResult.SkipReason))
            {
                // Skipped (e.g., user opted out)
                _logger.LogInformation(
                    "Notification skipped: EventId={EventId}, UserId={UserId}, Reason={Reason}",
                    notificationEvent.MessageId,
                    targetUser.UserId,
                    routingResult.SkipReason);

                await CreateDeliveryLogAsync(
                    notificationEvent,
                    targetUser,
                    routingResult,
                    DeliveryStatus.Failed,
                    cancellationToken,
                    additionalInfo: $"Skipped: {routingResult.SkipReason}");
            }
            else if (!routingResult.Success && routingResult.IsRetryable)
            {
                // Retryable failure - schedule retry
                await HandleRetryableFailureAsync(
                    notificationEvent,
                    targetUser,
                    routingResult,
                    cancellationToken);
            }
            else
            {
                // Non-retryable failure or max retries exceeded - move to dead letter
                await HandleNonRetryableFailureAsync(
                    notificationEvent,
                    targetUser,
                    routingResult,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing notification for user: UserId={UserId}, EventId={EventId}",
                targetUser.UserId,
                notificationEvent.MessageId);

            // Log the failure and move to dead letter queue
            await HandleNonRetryableFailureAsync(
                notificationEvent,
                targetUser,
                null,
                cancellationToken,
                exception: ex);
        }
    }

    private async Task HandleRetryableFailureAsync(
        NotificationEvent notificationEvent,
        NotificationEventPayloadTargetUsersItem targetUser,
        RoutingResult routingResult,
        CancellationToken cancellationToken)
    {
        // Determine current attempt number from retry queue or default to 1
        var attemptNumber = await GetCurrentAttemptNumberAsync(notificationEvent.MessageId.ToString(), cancellationToken);

        // Get max retry attempts based on priority
        var maxRetries = string.Equals(notificationEvent.Payload.Priority, "critical", StringComparison.OrdinalIgnoreCase) ? 3 : 1;

        if (attemptNumber >= maxRetries)
        {
            _logger.LogWarning(
                "Max retry attempts ({MaxRetries}) exceeded for notification: EventId={EventId}, UserId={UserId}",
                maxRetries,
                notificationEvent.MessageId,
                targetUser.UserId);

            await HandleNonRetryableFailureAsync(
                notificationEvent,
                targetUser,
                routingResult,
                cancellationToken);
            return;
        }

        _logger.LogWarning(
            "Retryable failure for notification: EventId={EventId}, UserId={UserId}, Attempt={Attempt}, Error={Error}",
            notificationEvent.MessageId,
            targetUser.UserId,
            attemptNumber,
            routingResult.ErrorMessage);

        // Schedule retry
        var retryResult = await _retryService.ScheduleRetryAsync(
            notificationEvent,
            attemptNumber + 1,
            routingResult.ErrorMessage ?? "Unknown error",
            cancellationToken);

        // Log the failed attempt
        await CreateDeliveryLogAsync(
            notificationEvent,
            targetUser,
            routingResult,
            DeliveryStatus.Failed,
            cancellationToken,
            attemptNumber: attemptNumber + 1);

        if (retryResult.WasScheduled)
        {
            _logger.LogInformation(
                "Retry scheduled: EventId={EventId}, Attempt={Attempt}, ScheduledTime={ScheduledTime}",
                notificationEvent.MessageId,
                attemptNumber + 1,
                retryResult.ScheduledTime);
        }
        else
        {
            _logger.LogWarning(
                "Failed to schedule retry: EventId={EventId}, Reason={Reason}",
                notificationEvent.MessageId,
                retryResult.Reason);
        }
    }

    private async Task HandleNonRetryableFailureAsync(
        NotificationEvent notificationEvent,
        NotificationEventPayloadTargetUsersItem targetUser,
        RoutingResult? routingResult,
        CancellationToken cancellationToken,
        Exception? exception = null)
    {
        var errorMessage = exception?.Message
            ?? routingResult?.ErrorMessage
            ?? "Unknown failure";

        _logger.LogError(
            "Non-retryable failure for notification: EventId={EventId}, UserId={UserId}, Error={Error}",
            notificationEvent.MessageId,
            targetUser.UserId,
            errorMessage);

        // Get all failure reasons from retry history for this specific user
        var failureReasons = await GetFailureReasonsAsync(notificationEvent.MessageId.ToString(), targetUser.UserId, cancellationToken);
        failureReasons.Add(errorMessage);

        // Create dead letter record
        var deadLetterRecord = new DeadLetterRecord
        {
            EventId = notificationEvent.MessageId.ToString(),
            // Optimizing storage: Only store essential info for retry
            EventPayload = JsonSerializer.Serialize(new
            {
                Event = notificationEvent,
                TargetUserId = targetUser.UserId
            }),
            FailureReasons = JsonSerializer.Serialize(failureReasons),
            TotalAttempts = failureReasons.Count,
            EscalationStatus = "pending",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.DeadLetterRecords.Add(deadLetterRecord);

        // Log the final failed attempt
        if (routingResult != null)
        {
            await CreateDeliveryLogAsync(
                notificationEvent,
                targetUser,
                routingResult,
                DeliveryStatus.Failed,
                cancellationToken,
                additionalInfo: "Moved to dead letter queue");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Notification moved to dead letter queue: EventId={EventId}, DeadLetterId={DeadLetterId}",
            notificationEvent.MessageId,
            deadLetterRecord.Id);

        // Trigger monitoring alert for critical notification failures
        if (notificationEvent.Payload.Priority?.Equals("critical", StringComparison.OrdinalIgnoreCase) == true)
        {
            await _alertingService.SendCriticalFailureAlertAsync(
                notificationEvent.MessageId.ToString(),
                targetUser.UserId,
                notificationEvent.Payload.NotificationType,
                deadLetterRecord.FailureReasons,
                deadLetterRecord.TotalAttempts,
                cancellationToken);
        }
    }

    private async Task CreateDeliveryLogAsync(
        NotificationEvent notificationEvent,
        NotificationEventPayloadTargetUsersItem targetUser,
        RoutingResult routingResult,
        DeliveryStatus status,
        CancellationToken cancellationToken,
        int attemptNumber = 1,
        string? additionalInfo = null)
    {
        var deliveryLog = new DeliveryLog
        {
            EventId = notificationEvent.MessageId.ToString(),
            UserId = targetUser.UserId,
            ChannelType = routingResult.SelectedChannel ?? "unknown",
            RecipientIdentifier = targetUser.UserId, // Use UserId as recipient identifier for tracking
            Status = status.ToString().ToLowerInvariant(),
            MessageContent = TruncateMessage(routingResult.ErrorMessage ?? additionalInfo ?? notificationEvent.Payload.TemplateId, 500),
            ProviderResponse = routingResult.DeliveryResult?.ProviderResponse ?? additionalInfo,
            ProviderMessageId = routingResult.DeliveryResult?.MessageId,
            AttemptNumber = attemptNumber,
            DeliveredAt = status == DeliveryStatus.Delivered ? DateTimeOffset.UtcNow : null,
            CreatedAt = DateTimeOffset.UtcNow
        };

        try
        {
            _dbContext.DeliveryLogs.Add(deliveryLog);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Log warning for duplicate delivery log if it's a unique constraint violation
            _logger.LogWarning(ex, "Failed to create delivery log for EventId={EventId}, UserId={UserId}. It may have been created concurrently.",
                notificationEvent.MessageId, targetUser.UserId);
        }
    }

    private async Task<int> GetCurrentAttemptNumberAsync(string eventId, CancellationToken cancellationToken)
    {
        var retryEntry = await _dbContext.RetryQueueEntries
            .Where(r => r.EventId == eventId)
            .OrderByDescending(r => r.AttemptNumber)
            .FirstOrDefaultAsync(cancellationToken);

        return retryEntry?.AttemptNumber ?? 0;
    }

    private async Task<List<string>> GetFailureReasonsAsync(string eventId, string userId, CancellationToken cancellationToken)
    {
        var deliveryLogs = await _dbContext.DeliveryLogs
            .Where(d => d.EventId == eventId && d.UserId == userId && d.Status == "failed")
            .OrderBy(d => d.CreatedAt)
            .Select(d => d.ProviderResponse ?? "Unknown error")
            .ToListAsync(cancellationToken);

        return deliveryLogs;
    }

    private static string TruncateMessage(string message, int maxLength)
    {
        if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
            return message;

        return message[..maxLength];
    }
}
