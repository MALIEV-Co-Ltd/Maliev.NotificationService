using System.ComponentModel.DataAnnotations;

namespace Maliev.NotificationService.Api.Models.Events;

/// <summary>
/// CloudEvents-inspired notification event schema
/// </summary>
public class NotificationEvent
{
    [Required]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string Source { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset Time { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    public string DataContentType { get; set; } = "application/json";

    [Required]
    public string SpecVersion { get; set; } = "1.0";

    [Required]
    public NotificationEventData Data { get; set; } = new();
}

public class NotificationEventData
{
    [Required]
    public string NotificationType { get; set; } = string.Empty;

    [Required]
    public string Priority { get; set; } = "standard"; // "critical" or "standard"

    [Required]
    [MinLength(1)]
    public List<TargetUser> TargetUsers { get; set; } = new();

    [Required]
    public string TemplateId { get; set; } = string.Empty;

    [Required]
    public Dictionary<string, string> Parameters { get; set; } = new();

    public NotificationMetadata? Metadata { get; set; }
}

public class TargetUser
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string UserType { get; set; } = string.Empty; // "customer", "staff", "administrator"
}

public class NotificationMetadata
{
    public string Language { get; set; } = "en";
    public string? Source { get; set; }
    public string? CorrelationId { get; set; }
}
