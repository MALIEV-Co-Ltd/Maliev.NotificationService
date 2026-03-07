using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.NotificationService.Domain.Entities;

[Table("dead_letter_records")]
public class DeadLetterRecord : BaseEntity
{
    [Required]
    [MaxLength(100)]
    [Column("event_id")]
    public string EventId { get; set; } = string.Empty;

    [Required]
    [Column("event_payload")]
    public string EventPayload { get; set; } = string.Empty;

    [Required]
    [Column("failure_reasons")]
    public string FailureReasons { get; set; } = "[]";

    [Column("total_attempts")]
    public int TotalAttempts { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("escalation_status")]
    public string EscalationStatus { get; set; } = "pending";

    [Column("escalated_at")]
    public DateTimeOffset? EscalatedAt { get; set; }

    [Column("resolved_at")]
    public DateTimeOffset? ResolvedAt { get; set; }

    [MaxLength(100)]
    [Column("resolved_by")]
    public string? ResolvedBy { get; set; }
}
