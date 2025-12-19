using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.NotificationService.Data.Entities;

/// <summary>
/// Tracks failed notifications pending retry
/// </summary>
[Table("retry_queue_entries")]
public class RetryQueueEntry : BaseEntity
{
    [Required]
    [MaxLength(100)]
    [Column("event_id")]
    public string EventId { get; set; } = string.Empty;

    [Required]
    [Column("event_payload")]
    public string EventPayload { get; set; } = string.Empty; // JSON serialized NotificationEvent

    [Column("attempt_number")]
    public int AttemptNumber { get; set; } = 1;

    [Required]
    [Column("scheduled_time")]
    public DateTimeOffset ScheduledTime { get; set; }

    [Column("last_error")]
    public string? LastError { get; set; }
}

