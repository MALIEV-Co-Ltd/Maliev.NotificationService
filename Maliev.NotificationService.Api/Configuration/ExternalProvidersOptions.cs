namespace Maliev.NotificationService.Api.Configuration;

/// <summary>
/// Options for external notification providers.
/// </summary>
public class ExternalProvidersOptions
{
    public const string SectionName = "ExternalProviders";

    public FacebookOptions Facebook { get; set; } = new();
    public InstagramOptions Instagram { get; set; } = new();
}

public class FacebookOptions
{
    public string? PageAccessToken { get; set; }
}

public class InstagramOptions
{
    public string? PageAccessToken { get; set; }
}
