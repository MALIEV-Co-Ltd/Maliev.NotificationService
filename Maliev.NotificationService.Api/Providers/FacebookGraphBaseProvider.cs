using System.Text.Json;
using System.Text.RegularExpressions;
using Maliev.NotificationService.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Maliev.NotificationService.Api.Providers;

/// <summary>
/// Base class for providers using the Facebook Graph API (Facebook Messenger and Instagram).
/// </summary>
public abstract partial class FacebookGraphBaseProvider : IChannelProvider
{
    protected readonly ILogger _logger;
    protected readonly IHttpClientFactory _httpClientFactory;
    protected readonly bool _isConfigured;
    protected readonly string? _accessToken;
    protected readonly string _clientName;

    public abstract string ChannelType { get; }

    protected FacebookGraphBaseProvider(
        ILogger logger,
        IHttpClientFactory httpClientFactory,
        string? accessToken,
        string clientName)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _accessToken = accessToken;
        _clientName = clientName;
        _isConfigured = !string.IsNullOrEmpty(_accessToken);

        if (!_isConfigured)
        {
            _logger.LogWarning("{ChannelType} PageAccessToken not configured. Sending will be simulated.", ChannelType);
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
            var validationResult = await ValidateRecipientAsync(recipientId, ct);
            if (!validationResult.IsValid)
            {
                return DeliveryResult.Failed(
                    failureType: DeliveryFailureType.InvalidRecipient,
                    errorMessage: validationResult.ErrorMessage ?? $"Invalid {ChannelType} recipient ID",
                    isRetryable: false);
            }

            if (!_isConfigured)
            {
                return await SimulateSendAsync(recipientId, message, ct);
            }

            return await ExecuteSendAsync(recipientId, message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send {ChannelType} message to {Recipient}", ChannelType, recipientId);
            return DeliveryResult.Failed(
                failureType: DeliveryFailureType.Transient,
                errorMessage: ex.Message,
                isRetryable: true);
        }
    }

    private async Task<DeliveryResult> SimulateSendAsync(string recipientId, string message, CancellationToken ct)
    {
        _logger.LogInformation("Simulating {ChannelType} send: To={Recipient}, MessageLength={Length}",
            ChannelType, recipientId, message.Length);

        await Task.Delay(150, ct);
        var simulatedId = $"{GetMessageIdPrefix()}_mid.{Guid.NewGuid():N}";

        return DeliveryResult.Successful(
            messageId: simulatedId,
            providerResponse: $"{ChannelType} message sent successfully (simulated)");
    }

    private async Task<DeliveryResult> ExecuteSendAsync(string recipientId, string message, CancellationToken ct)
    {
        _logger.LogInformation("Sending {ChannelType} message: To={Recipient}, MessageLength={Length}",
            ChannelType, recipientId, message.Length);

        var client = _httpClientFactory.CreateClient(_clientName);
        var payload = new
        {
            recipient = new { id = recipientId },
            message = new { text = message }
        };

        var response = await client.PostAsJsonAsync(
            $"me/messages?access_token={_accessToken}",
            payload,
            ct);

        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(responseContent);
            var messageId = doc.RootElement.GetProperty("message_id").GetString() ?? $"{GetMessageIdPrefix()}_mid.{Guid.NewGuid():N}";

            _logger.LogInformation("{ChannelType} message sent successfully: To={Recipient}, MessageId={MessageId}",
                ChannelType, recipientId, messageId);

            return DeliveryResult.Successful(
                messageId: messageId,
                providerResponse: responseContent);
        }

        _logger.LogError("{ChannelType} API error: Status={Status}, Response={Response}",
            ChannelType, response.StatusCode, responseContent);

        return DeliveryResult.Failed(
            failureType: MapHttpStatusCodeToFailureType(response.StatusCode),
            errorMessage: $"{ChannelType} API error: {response.StatusCode}",
            isRetryable: IsRetryableStatus(response.StatusCode));
    }

    public abstract Task<ValidationResult> ValidateRecipientAsync(string recipientId, CancellationToken ct);

    public async Task<bool> GetHealthAsync(CancellationToken ct)
    {
        if (!_isConfigured) return false;

        try
        {
            var client = _httpClientFactory.CreateClient(_clientName);
            var response = await client.GetAsync($"me?fields=id&access_token={_accessToken}", ct);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("{ChannelType} health check failed: {Status} {Content}",
                    ChannelType, response.StatusCode, content);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ChannelType} health check threw an exception", ChannelType);
            return false;
        }
    }

    protected abstract string GetMessageIdPrefix();

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
}
