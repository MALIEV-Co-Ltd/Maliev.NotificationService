using Maliev.NotificationService.Api.Models.Enums;

namespace Maliev.NotificationService.Api.Models.Responses;

/// <summary>
/// Response model for notification template
/// </summary>
public class TemplateResponse
{
    public Guid Id { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Language { get; set; } = string.Empty;
    public ChannelType ChannelType { get; set; }
    public string SubjectTemplate { get; set; } = string.Empty;
    public string ContentTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string[] Parameters { get; set; } = Array.Empty<string>();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
