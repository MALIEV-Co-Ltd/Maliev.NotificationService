namespace Maliev.NotificationService.Api.Models.Responses;

/// <summary>
/// Response model for channel binding
/// </summary>
public class ChannelBindingResponse
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Channel type
    /// </summary>
    public string ChannelType { get; set; } = string.Empty;

    /// <summary>
    /// Obfuscated channel identifier (e.g., "u***@example.com")
    /// </summary>
    public string ChannelIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Whether the channel binding is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Timestamp when binding was invalidated
    /// </summary>
    public DateTimeOffset? InvalidatedAt { get; set; }

    /// <summary>
    /// Reason for invalidation
    /// </summary>
    public string? InvalidatedReason { get; set; }

    /// <summary>
    /// Record creation timestamp
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
