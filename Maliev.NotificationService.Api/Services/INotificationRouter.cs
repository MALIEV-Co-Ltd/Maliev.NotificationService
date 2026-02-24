using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.MessagingContracts.Generated;
using Maliev.NotificationService.Api.Providers;

namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Service responsible for routing notifications to appropriate channel providers
/// based on user preferences, channel bindings, and default channel logic.
/// </summary>
public interface INotificationRouter
{
    /// <summary>
    /// Routes a notification event to the appropriate channel provider for delivery.
    /// Handles preference resolution, channel selection, and fallback logic.
    /// </summary>
    /// <param name="notificationEvent">The notification event to route</param>
    /// <param name="targetUser">The specific target user to deliver to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Routing result containing selected channel, delivery status, and any errors</returns>
    Task<RoutingResult> RouteAsync(
        NotificationEvent notificationEvent,
        NotificationEventPayloadTargetUsersItem targetUser,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of notification routing operation.
/// </summary>
public record RoutingResult
{
    /// <summary>
    /// Whether routing was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Selected channel type for delivery.
    /// </summary>
    public string? SelectedChannel { get; init; }

    /// <summary>
    /// Fallback channel used if primary failed.
    /// </summary>
    public string? FallbackChannelUsed { get; init; }

    /// <summary>
    /// Whether the notification was delivered.
    /// </summary>
    public bool WasDelivered { get; init; }

    /// <summary>
    /// Delivery result from the provider.
    /// </summary>
    public DeliveryResult? DeliveryResult { get; init; }

    /// <summary>
    /// Reason for skipping delivery (if applicable).
    /// </summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// Error message if routing or delivery failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether the failure is retryable.
    /// </summary>
    public bool IsRetryable { get; init; }

    /// <summary>
    /// Creates a successful routing result.
    /// </summary>
    public static RoutingResult Successful(
        string selectedChannel,
        DeliveryResult deliveryResult,
        string? fallbackChannel = null) =>
        new()
        {
            Success = true,
            SelectedChannel = selectedChannel,
            FallbackChannelUsed = fallbackChannel,
            WasDelivered = deliveryResult.Success,
            DeliveryResult = deliveryResult,
            IsRetryable = false
        };

    /// <summary>
    /// Creates a failed routing result.
    /// </summary>
    public static RoutingResult Failed(
        string errorMessage,
        bool isRetryable = false,
        string? selectedChannel = null) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage,
            IsRetryable = isRetryable,
            SelectedChannel = selectedChannel,
            WasDelivered = false
        };

    /// <summary>
    /// Creates a skipped routing result (e.g., user opted out).
    /// </summary>
    public static RoutingResult Skipped(string reason) =>
        new()
        {
            Success = true,
            SkipReason = reason,
            WasDelivered = false,
            IsRetryable = false
        };
}
