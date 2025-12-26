using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.NotificationService.Data.Entities;

/// <summary>
/// Stores user-specific notification channel preferences and opt-out settings.
/// </summary>
[Table("user_notification_preferences")]
public class UserNotificationPreference
{
    /// <summary>
    /// User identifier from User Service (Primary Key)
    /// </summary>
    [Key]
    [Required]
    [MaxLength(100)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Primary notification channel (email, line, whatsapp, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("primary_channel_type")]
    public string PrimaryChannelType { get; set; } = string.Empty;

    /// <summary>
    /// Ordered array of fallback channels (stored as JSONB)
    /// </summary>
    [Required]
    [Column("fallback_channel_types", TypeName = "jsonb")]
    public List<string> FallbackChannelTypes { get; set; } = new();

    /// <summary>
    /// Notification categories user opted out of (stored as JSONB)
    /// e.g., ["marketing", "promotions"]
    /// </summary>
    [Required]
    [Column("opt_out_categories", TypeName = "jsonb")]
    public List<string> OptOutCategories { get; set; } = new();

    /// <summary>
    /// Record creation timestamp
    /// </summary>
    [Required]
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Last update timestamp
    /// </summary>
    [Required]
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

