using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.NotificationService.Domain.Entities;

[Table("notification_templates")]
public class NotificationTemplate : BaseEntity
{
    [Required]
    [MaxLength(100)]
    [Column("template_key")]
    public string TemplateKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(160)]
    [Column("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [Column("version")]
    public int Version { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("language")]
    public string Language { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("channel_type")]
    public string ChannelType { get; set; } = string.Empty;

    [Required]
    [Column("content_template")]
    public string ContentTemplate { get; set; } = string.Empty;

    [Required]
    [Column("subject_template")]
    public string SubjectTemplate { get; set; } = string.Empty;

    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Required]
    [Column("parameters", TypeName = "jsonb")]
    public string[] Parameters { get; set; } = Array.Empty<string>();
}
