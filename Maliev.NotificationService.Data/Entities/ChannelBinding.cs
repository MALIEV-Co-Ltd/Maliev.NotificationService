using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.NotificationService.Data.Entities;

/// <summary>
/// Maps users to their channel-specific identifiers (LINE ID, email, phone number, etc.).
/// </summary>
[Table("channel_bindings")]
public class ChannelBinding : BaseEntity
{
    /// <summary>
    /// User identifier from User Service
    /// </summary>
    [Required]
    [MaxLength(100)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Channel type (email, line, whatsapp, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("channel_type")]
    public string ChannelType { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted channel-specific identifier (email address, LINE user ID, etc.)
    /// Should be encrypted at rest using PostgreSQL pgcrypto or application-level encryption
    /// </summary>
    [Required]
    [Column("channel_identifier")]
    public string ChannelIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Whether this binding is currently valid
    /// </summary>
    [Required]
    [Column("is_valid")]
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Timestamp when binding was invalidated
    /// </summary>
    [Column("invalidated_at")]
    public DateTimeOffset? InvalidatedAt { get; set; }

    /// <summary>
    /// Reason for invalidation (e.g., "User blocked bot", "Invalid recipient")
    /// MUST be NOT NULL when IsValid=false
    /// </summary>
    [Column("invalidated_reason")]
    public string? InvalidatedReason { get; set; }
}

