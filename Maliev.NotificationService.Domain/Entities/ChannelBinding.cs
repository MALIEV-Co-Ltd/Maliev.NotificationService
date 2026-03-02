using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.NotificationService.Domain.Entities;

[Table("channel_bindings")]
public class ChannelBinding : BaseEntity
{
    [Required]
    [MaxLength(100)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("channel_type")]
    public string ChannelType { get; set; } = string.Empty;

    [Required]
    [Column("channel_identifier")]
    public string ChannelIdentifier { get; set; } = string.Empty;

    [Required]
    [Column("is_valid")]
    public bool IsValid { get; set; } = true;

    [Column("invalidated_at")]
    public DateTimeOffset? InvalidatedAt { get; set; }

    [Column("invalidated_reason")]
    public string? InvalidatedReason { get; set; }
}
