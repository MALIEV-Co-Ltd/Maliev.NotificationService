using System.Text.RegularExpressions;
using Maliev.NotificationService.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Maliev.NotificationService.Api.Providers;

/// <summary>
/// Facebook Messenger channel provider implementation using Facebook Graph API.
/// </summary>
public partial class FacebookMessengerProvider : FacebookGraphBaseProvider
{
    public override string ChannelType => "facebook";

    public FacebookMessengerProvider(
        ILogger<FacebookMessengerProvider> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<ExternalProvidersOptions> options)
        : base(logger, httpClientFactory, options.Value.Facebook.PageAccessToken, "Facebook")
    {
    }

    public override Task<ValidationResult> ValidateRecipientAsync(string recipientId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipientId))
        {
            return Task.FromResult(ValidationResult.Invalid("Facebook PSID is required"));
        }

        if (!FacebookPsidRegex().IsMatch(recipientId))
        {
            return Task.FromResult(ValidationResult.Invalid(
                "Invalid Facebook PSID format. Expected: numeric string"));
        }

        return Task.FromResult(ValidationResult.Valid());
    }

    protected override string GetMessageIdPrefix() => "fb";

    [GeneratedRegex(@"^\d{10,20}$", RegexOptions.None)]
    private static partial Regex FacebookPsidRegex();
}
