namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Service for sending alerts to monitoring systems when critical failures occur.
/// </summary>
public interface IAlertingService
{
    /// <summary>
    /// Sends an alert for a critical notification delivery failure.
    /// </summary>
    /// <param name="eventId">The notification event ID</param>
    /// <param name="userId">The affected user ID</param>
    /// <param name="notificationType">The type of notification that failed</param>
    /// <param name="failureReason">Reason for the failure</param>
    /// <param name="attemptCount">Number of delivery attempts made</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendCriticalFailureAlertAsync(
        string eventId,
        string userId,
        string notificationType,
        string failureReason,
        int attemptCount,
        CancellationToken cancellationToken = default);
}
