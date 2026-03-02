using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.NotificationService.Domain.Entities;

[Table("delivery_logs")]
public class DeliveryLog : BaseEntity
{
    [Required]
    [MaxLength(100)]
    [Column("event_id")]
    public string EventId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("channel_type")]
    public string ChannelType { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    [Column("recipient_identifier")]
    public string RecipientIdentifier { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = string.Empty;

    [Column("message_content")]
    public string? MessageContent { get; set; }

    [Column("provider_response")]
    public string? ProviderResponse { get; set; }

    [MaxLength(200)]
    [Column("provider_message_id")]
    public string? ProviderMessageId { get; set; }

    [Column("attempt_number")]
    public int AttemptNumber { get; set; } = 1;

    [Column("delivered_at")]
    public DateTimeOffset? DeliveredAt { get; set; }
}
