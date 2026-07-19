using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.NotificationService.Domain.Entities;

[Table("deduplication_entries")]
public class DeduplicationEntry : BaseEntity
{
    [Required]
    [MaxLength(100)]
    [Column("event_id")]
    public string EventId { get; set; } = string.Empty;

    [Column("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddHours(24);
}
