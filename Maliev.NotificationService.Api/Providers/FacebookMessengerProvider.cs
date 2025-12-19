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

    public string ChannelType => "facebook";

    public FacebookMessengerProvider(
        ILogger<FacebookMessengerProvider> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
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

            // TODO: Replace with actual Facebook Graph API call
            // Example using Facebook Graph API:
            // var accessToken = _configuration["ExternalProviders:Facebook:PageAccessToken"];
            // var client = _httpClientFactory.CreateClient("Facebook");
            // var payload = new
            // {
            //     recipient = new { id = recipientId },
            //     message = new { text = message }
            // };
            // var response = await client.PostAsJsonAsync($"/v18.0/me/messages?access_token={accessToken}", payload, ct);

            // For now, simulate successful delivery
            await Task.Delay(170, ct); // Simulate network latency

            var messageId = $"fb_mid.{Guid.NewGuid():N}";

            _logger.LogInformation(
                "Facebook Messenger message sent successfully: To={Recipient}, MessageId={MessageId}",
                recipientId,
                messageId);

            return DeliveryResult.Successful(
                messageId: messageId,
                providerResponse: "Facebook Messenger message sent successfully (mock)");
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

    public Task<bool> GetHealthAsync(CancellationToken ct)
    {
        // For mock implementation, always return healthy
        // In production, check Facebook Graph API connectivity
        // Example: Make a GET request to /me endpoint to verify access token
        return Task.FromResult(true);
    }

    [GeneratedRegex(@"^\d{10,20}$", RegexOptions.None)]
    private static partial Regex FacebookPsidRegex();
}
