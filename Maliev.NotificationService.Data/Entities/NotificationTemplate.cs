using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.NotificationService.Data.Entities;

/// <summary>
/// Represents a notification template for multilingual, parameterized content
/// </summary>
[Table("notification_templates")]
public class NotificationTemplate : BaseEntity
{
    /// <summary>
    /// Template key identifier (e.g., "order-confirmed", "payment-failed")
    /// </summary>
    [Required]
    [MaxLength(100)]
    [Column("template_key")]
    public string TemplateKey { get; set; } = string.Empty;

    /// <summary>
    /// Template version number for versioning support
    /// </summary>
    [Required]
    [Column("version")]
    public int Version { get; set; }

    /// <summary>
    /// Language code (ISO 639-1, e.g., "en", "th")
    /// </summary>
    [Required]
    [MaxLength(10)]
    [Column("language")]
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Channel type this template is designed for (email, line, whatsapp, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("channel_type")]
    public string ChannelType { get; set; } = string.Empty;

    /// <summary>
    /// Template content with {{parameter}} placeholders
    /// </summary>
    [Required]
    [Column("content_template")]
    public string ContentTemplate { get; set; } = string.Empty;

    /// <summary>
    /// List of required parameter names expected in the template
    /// Stored as JSONB array in PostgreSQL
    /// </summary>
    [Required]
    [Column("parameters", TypeName = "jsonb")]
    public string[] Parameters { get; set; } = Array.Empty<string>();
}

