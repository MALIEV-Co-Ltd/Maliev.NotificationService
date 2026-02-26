using System.Text.RegularExpressions;
using Maliev.NotificationService.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Maliev.NotificationService.Api.Providers;

/// <summary>
/// Instagram Messaging API channel provider implementation using Facebook Graph API.
/// </summary>
public partial class InstagramProvider : FacebookGraphBaseProvider
{
    public override string ChannelType => "instagram";

    public InstagramProvider(
        ILogger<InstagramProvider> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<ExternalProvidersOptions> options)
        : base(logger, httpClientFactory, options.Value.Instagram.PageAccessToken, "Instagram")
    {
    }

    public override Task<ValidationResult> ValidateRecipientAsync(string recipientId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipientId))
        {
            return Task.FromResult(ValidationResult.Invalid("Instagram IGSID is required"));
        }

        if (!InstagramIgsidRegex().IsMatch(recipientId))
        {
            return Task.FromResult(ValidationResult.Invalid(
                "Invalid Instagram IGSID format. Expected: numeric string"));
        }

        return Task.FromResult(ValidationResult.Valid());
    }

    protected override string GetMessageIdPrefix() => "ig";

    [GeneratedRegex(@"^\d{10,20}$", RegexOptions.None)]
    private static partial Regex InstagramIgsidRegex();
}
