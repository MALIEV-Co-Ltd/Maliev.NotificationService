using Maliev.MessagingContracts.Generated;
using Maliev.NotificationService.Data;
using Maliev.NotificationService.Data.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Implements retry logic with exponential backoff for critical notifications
/// and fixed delay for standard notifications using MassTransit scheduled messages.
/// </summary>
public class RetryService : IRetryService
{
    private readonly NotificationDbContext _dbContext;
    private readonly IMessageScheduler _messageScheduler;
    private readonly ILogger<RetryService> _logger;

    // Retry configuration
    private const int MaxCriticalRetries = 3;
    private const int MaxStandardRetries = 1;
    private const int StandardRetryDelaySeconds = 5;
    private const int StaleRetryHours = 24;

    public RetryService(
        NotificationDbContext dbContext,
        IMessageScheduler messageScheduler,
        ILogger<RetryService> logger)
    {
        _dbContext = dbContext;
        _messageScheduler = messageScheduler;
        _logger = logger;
    }

    public async Task<RetryResult> ScheduleRetryAsync(
        NotificationEvent notificationEvent,
        int attemptNumber,
        string lastError,
        CancellationToken cancellationToken = default)
    {
        var payload = notificationEvent.Payload;
        try
        {
            var priority = payload.Priority?.ToLowerInvariant() ?? "critical";
            var maxRetries = priority == "standard" ? MaxStandardRetries : MaxCriticalRetries;

            // Check if max retries exceeded
            if (attemptNumber > maxRetries)
            {
                _logger.LogWarning(
                    "Max retries exceeded for event: EventId={EventId}, Attempts={Attempts}, Priority={Priority}",
                    notificationEvent.MessageId,
                    attemptNumber,
                    priority);

                return RetryResult.Failed($"Max retries ({maxRetries}) exceeded");
            }

            // Calculate retry delay
            var delay = CalculateRetryDelay(attemptNumber, priority);
            var scheduledTime = DateTimeOffset.UtcNow.Add(delay);

            // Create retry queue entry
            var retryEntry = new RetryQueueEntry
            {
                EventId = notificationEvent.MessageId.ToString(),
                EventPayload = JsonSerializer.Serialize(notificationEvent),
                AttemptNumber = attemptNumber,
                ScheduledTime = scheduledTime,
                LastError = lastError
            };

            _dbContext.RetryQueueEntries.Add(retryEntry);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Schedule the retry message with MassTransit
            await _messageScheduler.SchedulePublish(delay, notificationEvent, context =>
            {
                context.Headers.Set("X-Is-Retry", "true");
            }, cancellationToken);

            _logger.LogInformation(
                "Retry scheduled: EventId={EventId}, Attempt={Attempt}, ScheduledAt={ScheduledTime}, Delay={DelaySeconds}s",
                notificationEvent.MessageId,
                attemptNumber,
                scheduledTime,
                delay.TotalSeconds);

            return RetryResult.Success(scheduledTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to schedule retry for EventId={EventId}, Attempt={Attempt}",
                notificationEvent.MessageId,
                attemptNumber);

            throw;
        }
    }

    public TimeSpan CalculateRetryDelay(int attemptNumber, string priority = "critical")
    {
        if (priority.Equals("standard", StringComparison.OrdinalIgnoreCase))
        {
            // Fixed delay for standard/marketing notifications
            return TimeSpan.FromSeconds(StandardRetryDelaySeconds);
        }

        // Exponential backoff for critical notifications: 2^(attemptNumber-1) seconds
        // Attempt 1: 2^0 = 1s
        // Attempt 2: 2^1 = 2s
        // Attempt 3: 2^2 = 4s
        var delaySeconds = Math.Pow(2, attemptNumber - 1);
        return TimeSpan.FromSeconds(delaySeconds);
    }

    public async Task CleanupStaleRetriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffTime = DateTimeOffset.UtcNow.AddHours(-StaleRetryHours);

            var staleEntries = await _dbContext.RetryQueueEntries
                .Where(e => e.CreatedAt < cutoffTime)
                .ToListAsync(cancellationToken);

            if (staleEntries.Count > 0)
            {
                _dbContext.RetryQueueEntries.RemoveRange(staleEntries);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Cleaned up {Count} stale retry entries older than {Hours} hours",
                    staleEntries.Count,
                    StaleRetryHours);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup stale retry entries");
            throw;
        }
    }
}
