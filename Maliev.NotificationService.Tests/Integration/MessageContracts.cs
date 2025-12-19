namespace Maliev.NotificationService.Api.Tests.Integration;

/// <summary>
/// CloudEvents-compliant notification event for MassTransit publishing
/// </summary>
public class NotificationEvent
{
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTimeOffset Time { get; set; }
    public string DataContentType { get; set; } = "application/json";
    public string SpecVersion { get; set; } = "1.0";
    public NotificationEventData Data { get; set; } = new();
}

public class NotificationEventData
{
    public string NotificationType { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public TargetUser[] TargetUsers { get; set; } = Array.Empty<TargetUser>();
    public string TemplateId { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
    public NotificationMetadata? Metadata { get; set; }
}

public class TargetUser
{
    public string UserId { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
}

public class NotificationMetadata
{
    public string Language { get; set; } = "en";
    public string Source { get; set; } = string.Empty;
}
