using System.Text.Json;
using System.Text.RegularExpressions;

namespace Maliev.NotificationService.Api.Providers;

/// <summary>
/// Facebook Messenger channel provider implementation using Facebook Graph API.
/// Implements IChannelProvider for Facebook Messenger message delivery.
/// For MVP, this is a mock implementation that simulates Facebook Graph API.
/// In production, use HttpClient with Facebook Graph API Messenger endpoints.
/// </summary>
public partial class FacebookMessengerProvider : IChannelProvider
{
    private readonly ILogger<FacebookMessengerProvider> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly bool _isConfigured;

    public string ChannelType => "facebook";

    public FacebookMessengerProvider(
        ILogger<FacebookMessengerProvider> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;

        _isConfigured = !string.IsNullOrEmpty(_configuration["ExternalProviders:Facebook:PageAccessToken"]);
        if (!_isConfigured)
        {
            _logger.LogWarning("Facebook PageAccessToken not configured. Facebook Messenger sending will be simulated.");
        }
    }

    public async Task<DeliveryResult> SendAsync(
        string recipientId,
        string message,
        Dictionary<string, string>? metadata,
        CancellationToken ct)
    {
        try
        {
            // Validate Facebook PSID (Page-Scoped ID)
            var validationResult = await ValidateRecipientAsync(recipientId, ct);
            if (!validationResult.IsValid)
            {
                return DeliveryResult.Failed(
                    failureType: DeliveryFailureType.InvalidRecipient,
                    errorMessage: validationResult.ErrorMessage ?? "Invalid Facebook PSID",
                    isRetryable: false);
            }

            _logger.LogInformation(
                "Sending Facebook Messenger message: To={Recipient}, MessageLength={Length}",
                recipientId,
                message.Length);

            if (!_isConfigured)
            {
                _logger.LogInformation(
                    "Simulating Facebook Messenger send: To={Recipient}, MessageLength={Length}",
                    recipientId,
                    message.Length);

                await Task.Delay(170, ct);
                var simulatedId = $"fb_mid.{Guid.NewGuid():N}";

                return DeliveryResult.Successful(
                    messageId: simulatedId,
                    providerResponse: "Facebook Messenger message sent successfully (simulated - no credentials configured)");
            }

            var accessToken = _configuration["ExternalProviders:Facebook:PageAccessToken"];
            var client = _httpClientFactory.CreateClient("Facebook");
            var payload = new
            {
                recipient = new { id = recipientId },
                message = new { text = message }
            };

            var response = await client.PostAsJsonAsync(
                $"me/messages?access_token={accessToken}",
                payload,
                ct);

            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseContent);
                var messageId = doc.RootElement.GetProperty("message_id").GetString() ?? $"fb_mid.{Guid.NewGuid():N}";

                _logger.LogInformation(
                    "Facebook Messenger message sent successfully: To={Recipient}, MessageId={MessageId}",
                    recipientId,
                    messageId);

                return DeliveryResult.Successful(
                    messageId: messageId,
                    providerResponse: responseContent);
            }

            _logger.LogError(
                "Facebook Graph API error: Status={Status}, Response={Response}",
                response.StatusCode,
                responseContent);

            return DeliveryResult.Failed(
                failureType: MapHttpStatusCodeToFailureType(response.StatusCode),
                errorMessage: $"Facebook API error: {response.StatusCode}",
                isRetryable: IsRetryableStatus(response.StatusCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send Facebook Messenger message to {Recipient}",
                recipientId);

            return DeliveryResult.Failed(
                failureType: DeliveryFailureType.Transient,
                errorMessage: ex.Message,
                isRetryable: true);
        }
    }

    public Task<ValidationResult> ValidateRecipientAsync(string recipientId, CancellationToken ct)
    {
        // Validate Facebook PSID format: Numeric string (15-17 digits typically)
        // Example: 1234567890123456
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

    public async Task<bool> GetHealthAsync(CancellationToken ct)
    {
        if (!_isConfigured) return false;

        try
        {
            var accessToken = _configuration["ExternalProviders:Facebook:PageAccessToken"];
            var client = _httpClientFactory.CreateClient("Facebook");
            var response = await client.GetAsync($"me?fields=id&access_token={accessToken}", ct);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static DeliveryFailureType MapHttpStatusCodeToFailureType(System.Net.HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.BadRequest => DeliveryFailureType.InvalidRecipient,
            System.Net.HttpStatusCode.Unauthorized => DeliveryFailureType.AuthenticationFailed,
            System.Net.HttpStatusCode.Forbidden => DeliveryFailureType.AuthenticationFailed,
            System.Net.HttpStatusCode.TooManyRequests => DeliveryFailureType.RateLimitExceeded,
            _ => DeliveryFailureType.ProviderError
        };
    }

    private static bool IsRetryableStatus(System.Net.HttpStatusCode statusCode)
    {
        return (int)statusCode >= 500 || statusCode == System.Net.HttpStatusCode.TooManyRequests;
    }

    [GeneratedRegex(@"^\d{10,20}$", RegexOptions.None)]
    private static partial Regex FacebookPsidRegex();
}
