using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.NotificationService.Domain.Entities;

[Table("user_notification_preferences")]
public class UserNotificationPreference
{
    [Key]
    [Required]
    [MaxLength(100)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("primary_channel_type")]
    public string PrimaryChannelType { get; set; } = string.Empty;

    [Required]
    [Column("fallback_channel_types", TypeName = "jsonb")]
    public List<string> FallbackChannelTypes { get; set; } = new();

    [Required]
    [Column("opt_out_categories", TypeName = "jsonb")]
    public List<string> OptOutCategories { get; set; } = new();

    [Required]
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
