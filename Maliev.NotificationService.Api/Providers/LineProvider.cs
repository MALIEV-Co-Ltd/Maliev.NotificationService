using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;

namespace Maliev.NotificationService.Api.Providers;

/// <summary>
/// LINE Messaging API channel provider implementation.
/// Supports push messages to LINE users via LINE Messaging API.
/// </summary>
public partial class LineProvider : IChannelProvider
{
    private readonly ILogger<LineProvider> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PartitionedRateLimiter<string> _rateLimiter;
    private readonly string? _channelAccessToken;
    private readonly string _baseAddress;
    private readonly bool _isConfigured;

    public string ChannelType => "line";

    public LineProvider(
        ILogger<LineProvider> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;

        // Configure LINE Messaging API
        _channelAccessToken = _configuration["LINE:ChannelAccessToken"];
        _baseAddress = _configuration["ApiBaseAddresses:LINE"] ?? "https://api.line.me/v2/bot";
        _isConfigured = !string.IsNullOrEmpty(_channelAccessToken);

        // Define rate limiter matches the policy in Program.cs
        _rateLimiter = PartitionedRateLimiter.Create<string, string>(resource =>
        {
            return RateLimitPartition.GetTokenBucketLimiter("line", _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 1000,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                TokensPerPeriod = 10,
                AutoReplenishment = true
            });
        });

        if (!_isConfigured)
        {
            _logger.LogWarning("LINE Channel Access Token not configured. LINE sending will be simulated.");
        }
    }

    public async Task<DeliveryResult> SendAsync(
        string recipientId,
        string message,
        Dictionary<string, string>? metadata,
        CancellationToken ct)
    {
        // Enforce rate limiting
        using var lease = await _rateLimiter.AcquireAsync("line", 1, ct);
        if (!lease.IsAcquired)
        {
            _logger.LogWarning("Rate limit exceeded for LINE provider");
            return DeliveryResult.RateLimited();
        }

        try
        {
            // Validate LINE user ID format
            var validationResult = await ValidateRecipientAsync(recipientId, ct);
            if (!validationResult.IsValid)
            {
                return DeliveryResult.Failed(
                    failureType: DeliveryFailureType.InvalidRecipient,
                    errorMessage: validationResult.ErrorMessage ?? "Invalid LINE user ID",
                    isRetryable: false);
            }

            // If not configured, simulate sending
            if (!_isConfigured)
            {
                _logger.LogInformation(
                    "Simulating LINE send: To={Recipient}, MessageLength={Length}",
                    recipientId,
                    message.Length);

                await Task.Delay(150, ct);
                var simulatedMessageId = $"line_{Guid.NewGuid():N}";

                return DeliveryResult.Successful(
                    messageId: simulatedMessageId,
                    providerResponse: "LINE message sent successfully (simulated - no Channel Access Token configured)");
            }

            _logger.LogInformation(
                "Sending LINE message via LINE Messaging API: To={Recipient}, MessageLength={Length}",
                recipientId,
                message.Length);

            // Truncate message if too long (LINE allows 5000 characters)
            var lineMessage = message.Length > 5000 ? message.Substring(0, 4997) + "..." : message;

            // Create LINE push message request
            var pushMessageRequest = new
            {
                to = recipientId,
                messages = new[]
                {
                    new
                    {
                        type = "text",
                        text = lineMessage
                    }
                }
            };

            using var httpClient = _httpClientFactory.CreateClient("Line");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _channelAccessToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var jsonContent = JsonSerializer.Serialize(pushMessageRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{_baseAddress}/message/push", content, ct);

            if (response.IsSuccessStatusCode)
            {
                // LINE API doesn't return a message ID in the response, generate one
                var messageId = $"line_{Guid.NewGuid():N}";

                _logger.LogInformation(
                    "LINE message sent successfully: To={Recipient}, MessageId={MessageId}, StatusCode={StatusCode}",
                    recipientId,
                    messageId,
                    response.StatusCode);

                return DeliveryResult.Successful(
                    messageId: messageId,
                    providerResponse: $"LINE message sent successfully. Status: {response.StatusCode}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "LINE API error: StatusCode={StatusCode}, Response={Response}",
                    response.StatusCode,
                    errorContent);

                // Parse error response
                var isRetryable = (int)response.StatusCode >= 500 || (int)response.StatusCode == 429;

                var failureType = (int)response.StatusCode switch
                {
                    400 => DeliveryFailureType.InvalidRecipient, // Bad request - invalid user ID or message
                    401 => DeliveryFailureType.AuthenticationFailed, // Invalid channel access token
                    403 => DeliveryFailureType.AuthenticationFailed, // Forbidden
                    404 => DeliveryFailureType.InvalidRecipient, // User not found
                    429 => DeliveryFailureType.RateLimitExceeded, // Rate limit exceeded
                    _ => isRetryable ? DeliveryFailureType.Transient : DeliveryFailureType.ProviderError
                };

                return DeliveryResult.Failed(
                    failureType: failureType,
                    errorMessage: $"LINE API error {response.StatusCode}: {errorContent}",
                    isRetryable: isRetryable);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "HTTP error while sending LINE message to {Recipient}",
                recipientId);

            return DeliveryResult.Failed(
                failureType: DeliveryFailureType.Transient,
                errorMessage: $"LINE API connection error: {ex.Message}",
                isRetryable: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send LINE message to {Recipient}",
                recipientId);

            return DeliveryResult.Failed(
                failureType: DeliveryFailureType.Transient,
                errorMessage: ex.Message,
                isRetryable: true);
        }
    }

    public Task<ValidationResult> ValidateRecipientAsync(string recipientId, CancellationToken ct)
    {
        // Validate LINE user ID format: U followed by 32 hexadecimal characters
        // Example: Udeadbeef01234567890abcdef123456
        if (string.IsNullOrWhiteSpace(recipientId))
        {
            return Task.FromResult(ValidationResult.Invalid("LINE user ID is required"));
        }

        if (!LineUserIdRegex().IsMatch(recipientId))
        {
            return Task.FromResult(ValidationResult.Invalid(
                "Invalid LINE user ID format. Expected format: U[0-9a-f]{32}"));
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
            // Check LINE Messaging API connectivity by getting bot info
            using var httpClient = _httpClientFactory.CreateClient("Line");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _channelAccessToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await httpClient.GetAsync($"{_baseAddress}/info", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LINE API health check failed");
            return false;
        }
    }

    [GeneratedRegex(@"^U[0-9a-f]{32}$", RegexOptions.IgnoreCase)]
    private static partial Regex LineUserIdRegex();
}
