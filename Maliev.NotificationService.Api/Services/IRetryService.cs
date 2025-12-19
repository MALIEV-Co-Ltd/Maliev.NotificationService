using Maliev.MessagingContracts.Contracts;

namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Service responsible for scheduling and managing notification retry logic.
/// Implements exponential backoff for critical notifications and fixed delay for standard notifications.
/// </summary>
public interface IRetryService
{
    /// <summary>
    /// Schedules a failed notification for retry using exponential backoff.
    /// </summary>
    /// <param name="notificationEvent">The failed notification event</param>
    /// <param name="attemptNumber">Current retry attempt number (1-based)</param>
    /// <param name="lastError">Error message from the failed attempt</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating whether the retry was scheduled or if max retries were exceeded</returns>
    Task<RetryResult> ScheduleRetryAsync(
        NotificationEvent notificationEvent,
        int attemptNumber,
        string lastError,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the retry delay using exponential backoff for critical notifications
    /// or fixed delay for standard notifications.
    /// </summary>
    /// <param name="attemptNumber">Current retry attempt number (1-based)</param>
    /// <param name="priority">Notification priority (critical or standard)</param>
    /// <returns>TimeSpan delay before the next retry attempt</returns>
    TimeSpan CalculateRetryDelay(int attemptNumber, string priority = "critical");

    /// <summary>
    /// Cleans up stale retry queue entries older than 24 hours.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CleanupStaleRetriesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a retry scheduling operation.
/// </summary>
public record RetryResult
{
    /// <summary>
    /// Whether the retry was successfully scheduled.
    /// </summary>
    public required bool WasScheduled { get; init; }

    /// <summary>
    /// Reason why the retry was not scheduled (e.g., max retries exceeded).
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// The scheduled time for the retry.
    /// </summary>
    public DateTimeOffset? ScheduledTime { get; init; }

    /// <summary>
    /// Creates a successful retry result.
    /// </summary>
    public static RetryResult Success(DateTimeOffset scheduledTime) =>
        new()
        {
            WasScheduled = true,
            ScheduledTime = scheduledTime
        };

    /// <summary>
    /// Creates a failed retry result.
    /// </summary>
    public static RetryResult Failed(string reason) =>
        new()
        {
            WasScheduled = false,
            Reason = reason
        };
}
