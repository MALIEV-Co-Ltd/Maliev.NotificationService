using System.Text.RegularExpressions;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Twilio.Exceptions;

namespace Maliev.NotificationService.Api.Providers;

/// <summary>
/// SMS channel provider implementation using Twilio API.
/// Supports SMS message delivery with delivery status tracking.
/// </summary>
public partial class SmsProvider : IChannelProvider
{
    private readonly ILogger<SmsProvider> _logger;
    private readonly IConfiguration _configuration;
    private readonly string? _fromPhoneNumber;
    private readonly bool _isConfigured;

    public string ChannelType => "sms";

    public SmsProvider(
        ILogger<SmsProvider> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        // Configure Twilio
        var accountSid = _configuration["Twilio:AccountSid"];
        var authToken = _configuration["Twilio:AuthToken"];
        _fromPhoneNumber = _configuration["Twilio:PhoneNumber"];

        if (!string.IsNullOrEmpty(accountSid) && !string.IsNullOrEmpty(authToken) && !string.IsNullOrEmpty(_fromPhoneNumber))
        {
            TwilioClient.Init(accountSid, authToken);
            _isConfigured = true;
        }
        else
        {
            _logger.LogWarning("Twilio credentials not fully configured. SMS sending will be simulated.");
            _isConfigured = false;
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
                    "Simulating SMS send: To={Recipient}, MessageLength={Length}",
                    recipientId,
                    message.Length);

                await Task.Delay(180, ct);
                var simulatedMessageId = $"SM{Guid.NewGuid():N}";

                return DeliveryResult.Successful(
                    messageId: simulatedMessageId,
                    providerResponse: "SMS sent successfully (simulated - no Twilio credentials configured)");
            }

            _logger.LogInformation(
                "Sending SMS via Twilio: To={Recipient}, MessageLength={Length}",
                recipientId,
                message.Length);

            // Truncate message if too long (Twilio SMS limit is 1600 chars)
            var smsMessage = message.Length > 1600 ? message.Substring(0, 1597) + "..." : message;

            // Create message options
            var messageOptions = new CreateMessageOptions(new PhoneNumber(recipientId))
            {
                From = new PhoneNumber(_fromPhoneNumber),
                Body = smsMessage
            };

            // Add optional parameters from metadata
            if (metadata?.ContainsKey("statusCallback") == true)
            {
                messageOptions.StatusCallback = new Uri(metadata["statusCallback"]);
            }

            // Send SMS
            var twilioMessage = await MessageResource.CreateAsync(messageOptions);

            _logger.LogInformation(
                "SMS sent successfully via Twilio: To={Recipient}, MessageId={MessageId}, Status={Status}",
                recipientId,
                twilioMessage.Sid,
                twilioMessage.Status);

            return DeliveryResult.Successful(
                messageId: twilioMessage.Sid,
                providerResponse: $"SMS sent successfully. Status: {twilioMessage.Status}");
        }
        catch (ApiException ex)
        {
            _logger.LogError(
                ex,
                "Twilio API error while sending SMS to {Recipient}: {ErrorCode} - {ErrorMessage}",
                recipientId,
                ex.Code,
                ex.Message);

            // Determine if error is retryable
            // https://www.twilio.com/docs/api/errors
            var isRetryable = ex.Code >= 50000 || // Server errors
                             ex.Code == 20429 || // Rate limit
                             ex.Code == 20003 || // Authentication unavailable
                             ex.Code == 30003 || // Unreachable destination (temporary)
                             ex.Code == 30005;   // Unknown destination (temporary)

            var failureType = ex.Code switch
            {
                21211 => DeliveryFailureType.InvalidRecipient, // Invalid 'To' phone number
                21614 => DeliveryFailureType.InvalidRecipient, // Invalid 'From' phone number
                21608 => DeliveryFailureType.InvalidRecipient, // Unverified number
                20429 => DeliveryFailureType.RateLimitExceeded,
                _ => isRetryable ? DeliveryFailureType.Transient : DeliveryFailureType.ProviderError
            };

            return DeliveryResult.Failed(
                failureType: failureType,
                errorMessage: $"Twilio error {ex.Code}: {ex.Message}",
                isRetryable: isRetryable);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send SMS to {Recipient}",
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
        // In production, could verify API connectivity with a lightweight API call
        return Task.FromResult(_isConfigured);
    }

    [GeneratedRegex(@"^\+[1-9]\d{1,14}$", RegexOptions.None)]
    private static partial Regex PhoneNumberRegex();
}
