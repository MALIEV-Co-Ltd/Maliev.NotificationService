using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Maliev.NotificationService.Api.Providers;

/// <summary>
/// Slack channel provider implementation using Slack Web API.
/// Supports sending messages via chat.postMessage API to users and channels.
/// </summary>
public partial class SlackProvider : IChannelProvider
{
    private readonly ILogger<SlackProvider> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _botToken;
    private readonly string _baseAddress;
    private readonly bool _isConfigured;

    public string ChannelType => "slack";

    public SlackProvider(
        ILogger<SlackProvider> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;

        // Configure Slack Bot Token
        _botToken = _configuration["Slack:BotToken"];
        _baseAddress = _configuration["ApiBaseAddresses:Slack"] ?? "https://slack.com/api";
        _isConfigured = !string.IsNullOrEmpty(_botToken);

        if (!_isConfigured)
        {
            _logger.LogWarning("Slack Bot Token not configured. Slack sending will be simulated.");
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
            // Validate Slack user ID or channel format
            var validationResult = await ValidateRecipientAsync(recipientId, ct);
            if (!validationResult.IsValid)
            {
                return DeliveryResult.Failed(
                    failureType: DeliveryFailureType.InvalidRecipient,
                    errorMessage: validationResult.ErrorMessage ?? "Invalid Slack user ID or channel",
                    isRetryable: false);
            }

            // If not configured, simulate sending
            if (!_isConfigured)
            {
                _logger.LogInformation(
                    "Simulating Slack send: To={Recipient}, MessageLength={Length}",
                    recipientId,
                    message.Length);

                await Task.Delay(120, ct);
                var simulatedMessageId = $"slack_{Guid.NewGuid():N}";

                return DeliveryResult.Successful(
                    messageId: simulatedMessageId,
                    providerResponse: "Slack message sent successfully (simulated - no Bot Token configured)");
            }

            _logger.LogInformation(
                "Sending Slack message via Web API: To={Recipient}, MessageLength={Length}",
                recipientId,
                message.Length);

            // Truncate message if too long (Slack allows 40000 characters, but be conservative)
            var slackMessage = message.Length > 4000 ? message.Substring(0, 3997) + "..." : message;

            // Build Slack message payload
            var payload = new
            {
                channel = recipientId,
                text = slackMessage,
                // Support markdown formatting
                mrkdwn = true
            };

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _botToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var jsonContent = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{_baseAddress}/chat.postMessage", content, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            // Parse Slack response
            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;
            var isOk = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();

            if (isOk)
            {
                // Extract message timestamp (ts) as message ID
                var messageTs = root.TryGetProperty("ts", out var tsProp) ? tsProp.GetString() : null;
                var messageId = messageTs ?? $"slack_{Guid.NewGuid():N}";

                _logger.LogInformation(
                    "Slack message sent successfully: To={Recipient}, MessageId={MessageId}",
                    recipientId,
                    messageId);

                return DeliveryResult.Successful(
                    messageId: messageId,
                    providerResponse: $"Slack message sent successfully. TS: {messageTs}");
            }
            else
            {
                // Parse error from Slack response
                var errorMessage = root.TryGetProperty("error", out var errorProp)
                    ? errorProp.GetString()
                    : "Unknown Slack API error";

                _logger.LogError(
                    "Slack API error: Error={Error}, Response={Response}",
                    errorMessage,
                    responseContent);

                // Determine error type and retryability
                var (failureType, isRetryable) = errorMessage switch
                {
                    "invalid_auth" => (DeliveryFailureType.AuthenticationFailed, false),
                    "token_revoked" => (DeliveryFailureType.AuthenticationFailed, false),
                    "account_inactive" => (DeliveryFailureType.AuthenticationFailed, false),
                    "channel_not_found" => (DeliveryFailureType.InvalidRecipient, false),
                    "not_in_channel" => (DeliveryFailureType.InvalidRecipient, false),
                    "user_not_found" => (DeliveryFailureType.InvalidRecipient, false),
                    "is_archived" => (DeliveryFailureType.InvalidRecipient, false),
                    "rate_limited" => (DeliveryFailureType.RateLimitExceeded, true),
                    "fatal_error" => (DeliveryFailureType.Transient, true),
                    _ => (DeliveryFailureType.ProviderError, false)
                };

                return DeliveryResult.Failed(
                    failureType: failureType,
                    errorMessage: $"Slack API error: {errorMessage}",
                    isRetryable: isRetryable);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "HTTP error while sending Slack message to {Recipient}",
                recipientId);

            return DeliveryResult.Failed(
                failureType: DeliveryFailureType.Transient,
                errorMessage: $"Slack API connection error: {ex.Message}",
                isRetryable: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send Slack message to {Recipient}",
                recipientId);

            return DeliveryResult.Failed(
                failureType: DeliveryFailureType.Transient,
                errorMessage: ex.Message,
                isRetryable: true);
        }
    }

    public Task<ValidationResult> ValidateRecipientAsync(string recipientId, CancellationToken ct)
    {
        // Validate Slack user ID (U...) or channel (#channel-name or C...)
        // User ID format: U followed by alphanumeric (e.g., U1234567890)
        // Channel ID format: C followed by alphanumeric (e.g., C1234567890)
        // Channel name format: #channel-name
        if (string.IsNullOrWhiteSpace(recipientId))
        {
            return Task.FromResult(ValidationResult.Invalid("Slack user ID or channel is required"));
        }

        if (!SlackIdRegex().IsMatch(recipientId))
        {
            return Task.FromResult(ValidationResult.Invalid(
                "Invalid Slack ID format. Expected: U[alphanumeric], C[alphanumeric], or #channel-name"));
        }

        return Task.FromResult(ValidationResult.Valid());
    }

    public async Task<bool> GetHealthAsync(CancellationToken ct)
    {
        // If not configured, return false
        if (!_isConfigured)
        {
            return false;
        }

        try
        {
            // Check Slack API connectivity using auth.test endpoint
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _botToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await httpClient.GetAsync($"{_baseAddress}/auth.test", ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            // Parse Slack response
            using var jsonDoc = JsonDocument.Parse(responseContent);
            var root = jsonDoc.RootElement;
            var isOk = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();

            return isOk;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Slack API health check failed");
            return false;
        }
    }

    [GeneratedRegex(@"^(U[A-Z0-9]+|C[A-Z0-9]+|#[a-z0-9-]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex SlackIdRegex();
}
