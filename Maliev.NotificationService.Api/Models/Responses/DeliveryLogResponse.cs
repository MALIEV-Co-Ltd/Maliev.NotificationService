namespace Maliev.NotificationService.Api.Models.Responses;

/// <summary>
/// Response model for delivery log
/// </summary>
public class DeliveryLogResponse
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Notification event identifier
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// User identifier
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Channel type used for delivery
    /// </summary>
    public string ChannelType { get; set; } = string.Empty;

    /// <summary>
    /// Obfuscated recipient identifier
    /// </summary>
    public string RecipientIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Delivery status (sent, delivered, failed, pending, rate_limited)
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Truncated message content (first 500 characters)
    /// </summary>
    public string? MessageContent { get; set; }

    /// <summary>
    /// Provider-specific message identifier
    /// </summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>
    /// Delivery attempt number
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Timestamp when message was delivered (null if not delivered)
    /// </summary>
    public DateTimeOffset? DeliveredAt { get; set; }

    /// <summary>
    /// Record creation timestamp
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
