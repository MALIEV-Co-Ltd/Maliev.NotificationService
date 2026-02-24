namespace Maliev.NotificationService.Api.Models.Responses;

/// <summary>
/// Response model for user notification preferences
/// </summary>
public class PreferenceResponse
{
    /// <summary>
    /// User identifier
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Primary notification channel
    /// </summary>
    public string PrimaryChannelType { get; set; } = string.Empty;

    /// <summary>
    /// Ordered list of fallback channels
    /// </summary>
    public List<string> FallbackChannelTypes { get; set; } = new();

    /// <summary>
    /// Notification categories user opted out of
    /// </summary>
    public List<string> OptOutCategories { get; set; } = new();

    /// <summary>
    /// Record creation timestamp
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
