using System.Text.RegularExpressions;

namespace Maliev.NotificationService.Api.Providers;

/// <summary>
/// Instagram Messaging API channel provider implementation using Facebook Graph API.
/// Implements IChannelProvider for Instagram Direct Message delivery.
/// For MVP, this is a mock implementation that simulates Instagram Messaging API.
/// In production, use HttpClient with Facebook Graph API Instagram Messaging endpoints.
/// </summary>
public partial class InstagramProvider : IChannelProvider
{
    private readonly ILogger<InstagramProvider> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public string ChannelType => "instagram";

    public InstagramProvider(
        ILogger<InstagramProvider> logger,
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
            // Validate Instagram IGSID (Instagram-Scoped ID)
            var validationResult = await ValidateRecipientAsync(recipientId, ct);
            if (!validationResult.IsValid)
            {
                return DeliveryResult.Failed(
                    failureType: DeliveryFailureType.InvalidRecipient,
                    errorMessage: validationResult.ErrorMessage ?? "Invalid Instagram IGSID",
                    isRetryable: false);
            }

            _logger.LogInformation(
                "Sending Instagram message: To={Recipient}, MessageLength={Length}",
                recipientId,
                message.Length);

            // TODO: Replace with actual Instagram Messaging API call
            // Example using Facebook Graph API:
            // var accessToken = _configuration["ExternalProviders:Instagram:PageAccessToken"];
            // var client = _httpClientFactory.CreateClient("Instagram");
            // var payload = new
            // {
            //     recipient = new { id = recipientId },
            //     message = new { text = message }
            // };
            // var response = await client.PostAsJsonAsync($"/v18.0/me/messages?access_token={accessToken}", payload, ct);

            // For now, simulate successful delivery
            await Task.Delay(160, ct); // Simulate network latency

            var messageId = $"ig_mid.{Guid.NewGuid():N}";

            _logger.LogInformation(
                "Instagram message sent successfully: To={Recipient}, MessageId={MessageId}",
                recipientId,
                messageId);

            return DeliveryResult.Successful(
                messageId: messageId,
                providerResponse: "Instagram message sent successfully (mock)");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send Instagram message to {Recipient}",
                recipientId);

            return DeliveryResult.Failed(
                failureType: DeliveryFailureType.Transient,
                errorMessage: ex.Message,
                isRetryable: true);
        }
    }

    public Task<ValidationResult> ValidateRecipientAsync(string recipientId, CancellationToken ct)
    {
        // Validate Instagram IGSID format: Numeric string (similar to Facebook PSID)
        // Example: 1234567890123456
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

    public Task<bool> GetHealthAsync(CancellationToken ct)
    {
        // For mock implementation, always return healthy
        // In production, check Instagram Messaging API connectivity
        // Example: Make a GET request to /me endpoint to verify access token
        return Task.FromResult(true);
    }

    [GeneratedRegex(@"^\d{10,20}$", RegexOptions.None)]
    private static partial Regex InstagramIgsidRegex();
}
