using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Twilio;
using Twilio.Exceptions;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Maliev.NotificationService.Api.Providers;

/// <summary>
/// WhatsApp channel provider implementation using Twilio WhatsApp API.
/// Supports WhatsApp message delivery via Twilio's WhatsApp Business integration.
/// </summary>
public partial class WhatsAppProvider : IChannelProvider
{
    private readonly ILogger<WhatsAppProvider> _logger;
    private readonly IConfiguration _configuration;
    private readonly PartitionedRateLimiter<string> _rateLimiter;
    private readonly string? _fromWhatsAppNumber;
    private readonly bool _isConfigured;

    public string ChannelType => "whatsapp";

    public WhatsAppProvider(
        ILogger<WhatsAppProvider> logger,
        IConfiguration _configuration)
    {
        _logger = logger;
        this._configuration = _configuration;

        // Configure Twilio for WhatsApp
        var accountSid = _configuration["Twilio:AccountSid"];
        var authToken = _configuration["Twilio:AuthToken"];
        _fromWhatsAppNumber = _configuration["Twilio:WhatsAppNumber"];

        if (!string.IsNullOrEmpty(accountSid) && !string.IsNullOrEmpty(authToken) && !string.IsNullOrEmpty(_fromWhatsAppNumber))
        {
            TwilioClient.Init(accountSid, authToken);
            _isConfigured = true;
        }
        else
        {
            _logger.LogWarning("Twilio WhatsApp credentials not fully configured. WhatsApp sending will be simulated.");
            _isConfigured = false;
        }

        // Define rate limiter matches the policy in Program.cs
        _rateLimiter = PartitionedRateLimiter.Create<string, string>(resource =>
        {
            return RateLimitPartition.GetTokenBucketLimiter("whatsapp", _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 80,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                TokensPerPeriod = 80,
                AutoReplenishment = true
            });
        });
    }

    public async Task<DeliveryResult> SendAsync(
        string recipientId,
        string message,
        Dictionary<string, string>? metadata,
        CancellationToken ct)
    {
        // Enforce rate limiting
        using var lease = await _rateLimiter.AcquireAsync("whatsapp", 1, ct);
        if (!lease.IsAcquired)
        {
            _logger.LogWarning("Rate limit exceeded for WhatsApp provider");
            return DeliveryResult.RateLimited();
        }

        try
        {
            // Validate E.164 phone number format
            var validationResult = await ValidateRecipientAsync(recipientId, ct);
            if (!validationResult.IsValid)
            {
                return DeliveryResult.Failed(
                    failureType: DeliveryFailureType.InvalidRecipient,
                    errorMessage: validationResult.ErrorMessage ?? "Invalid phone number",
                    isRetryable: false);
            }

            // If not configured, simulate sending
            if (!_isConfigured)
            {
                _logger.LogInformation(
                    "Simulating WhatsApp send: To={Recipient}, MessageLength={Length}",
                    recipientId,
                    message.Length);

                await Task.Delay(200, ct);
                var simulatedMessageId = $"wamid_{Guid.NewGuid():N}";

                return DeliveryResult.Successful(
                    messageId: simulatedMessageId,
                    providerResponse: "WhatsApp message sent successfully (simulated - no Twilio credentials configured)");
            }

            _logger.LogInformation(
                "Sending WhatsApp message via Twilio: To={Recipient}, MessageLength={Length}",
                recipientId,
                message.Length);

            // Truncate message if too long (WhatsApp allows 4096 chars)
            var whatsappMessage = message.Length > 4096 ? message.Substring(0, 4093) + "..." : message;

            // Format numbers for WhatsApp (must include "whatsapp:" prefix)
            var toWhatsApp = $"whatsapp:{recipientId}";
            var fromWhatsApp = $"whatsapp:{_fromWhatsAppNumber}";

            // Create message options
            var messageOptions = new CreateMessageOptions(new PhoneNumber(toWhatsApp))
            {
                From = new PhoneNumber(fromWhatsApp),
                Body = whatsappMessage
            };

            // Add optional media URL if provided in metadata
            if (metadata?.ContainsKey("mediaUrl") == true)
            {
                messageOptions.MediaUrl = new List<Uri> { new Uri(metadata["mediaUrl"]) };
            }

            // Send WhatsApp message
            var twilioMessage = await MessageResource.CreateAsync(messageOptions);

            _logger.LogInformation(
                "WhatsApp message sent successfully via Twilio: To={Recipient}, MessageId={MessageId}, Status={Status}",
                recipientId,
                twilioMessage.Sid,
                twilioMessage.Status);

            return DeliveryResult.Successful(
                messageId: twilioMessage.Sid,
                providerResponse: $"WhatsApp message sent successfully. Status: {twilioMessage.Status}");
        }
        catch (ApiException ex)
        {
            _logger.LogError(
                ex,
                "Twilio WhatsApp API error while sending message to {Recipient}: {ErrorCode} - {ErrorMessage}",
                recipientId,
                ex.Code,
                ex.Message);

            // Determine if error is retryable
            var isRetryable = ex.Code >= 50000 || // Server errors
                             ex.Code == 20429 || // Rate limit
                             ex.Code == 20003 || // Authentication unavailable
                             ex.Code == 63016 || // WhatsApp number not enabled
                             ex.Code == 63033;   // Template message error (transient)

            var failureType = ex.Code switch
            {
                21211 => DeliveryFailureType.InvalidRecipient, // Invalid 'To' phone number
                63007 => DeliveryFailureType.InvalidRecipient, // Recipient not on WhatsApp
                63015 => DeliveryFailureType.InvalidRecipient, // Recipient opted out
                21614 => DeliveryFailureType.InvalidRecipient, // Invalid 'From' number
                20429 => DeliveryFailureType.RateLimitExceeded,
                _ => isRetryable ? DeliveryFailureType.Transient : DeliveryFailureType.ProviderError
            };

            return DeliveryResult.Failed(
                failureType: failureType,
                errorMessage: $"Twilio WhatsApp error {ex.Code}: {ex.Message}",
                isRetryable: isRetryable);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send WhatsApp message to {Recipient}",
                recipientId);

            return DeliveryResult.Failed(
                failureType: DeliveryFailureType.Transient,
                errorMessage: ex.Message,
                isRetryable: true);
        }
    }

    public Task<ValidationResult> ValidateRecipientAsync(string recipientId, CancellationToken ct)
    {
        // Validate E.164 phone number format: + followed by 1-15 digits
        // Example: +66812345678, +15551234567
        if (string.IsNullOrWhiteSpace(recipientId))
        {
            return Task.FromResult(ValidationResult.Invalid("Phone number is required"));
        }

        if (!PhoneNumberRegex().IsMatch(recipientId))
        {
            return Task.FromResult(ValidationResult.Invalid(
                "Invalid phone number format. Expected E.164 format: +[1-15 digits]"));
        }

        return Task.FromResult(ValidationResult.Valid());
    }

    public Task<bool> GetHealthAsync(CancellationToken ct)
    {
        // Return configuration status
        return Task.FromResult(_isConfigured);
    }

    [GeneratedRegex(@"^\+[1-9]\d{1,14}$", RegexOptions.None)]
    private static partial Regex PhoneNumberRegex();
}
