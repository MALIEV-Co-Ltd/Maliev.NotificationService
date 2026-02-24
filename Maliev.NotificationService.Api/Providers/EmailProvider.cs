using brevo_csharp.Api;
using brevo_csharp.Client;
using brevo_csharp.Model;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using SystemTask = System.Threading.Tasks.Task;

namespace Maliev.NotificationService.Api.Providers;

/// <summary>
/// Email channel provider implementation using Brevo (formerly Sendinblue) API.
/// Supports transactional emails with template rendering.
/// </summary>
public partial class EmailProvider : IChannelProvider
{
    private readonly ILogger<EmailProvider> _logger;
    private readonly IConfiguration _configuration;
    private readonly TransactionalEmailsApi _emailApi;
    private readonly PartitionedRateLimiter<string> _rateLimiter;
    private readonly string? _senderEmail;
    private readonly string? _senderName;

    public string ChannelType => "email";

    public EmailProvider(
        ILogger<EmailProvider> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        // Configure Brevo API
        var apiKey = _configuration["Brevo:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            brevo_csharp.Client.Configuration.Default.ApiKey.Add("api-key", apiKey);
            _emailApi = new TransactionalEmailsApi();
        }
        else
        {
            _logger.LogWarning("Brevo API key not configured. Email sending will be simulated.");
            _emailApi = null!;
        }

        // Define rate limiter matches the policy in Program.cs
        _rateLimiter = PartitionedRateLimiter.Create<string, string>(resource =>
        {
            return RateLimitPartition.GetTokenBucketLimiter("email", _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 100,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                TokensPerPeriod = 100,
                AutoReplenishment = true
            });
        });

        _senderEmail = _configuration["Brevo:SenderEmail"] ?? "noreply@maliev.com";
        _senderName = _configuration["Brevo:SenderName"] ?? "Maliev Notification Service";
    }

    public async Task<DeliveryResult> SendAsync(
        string recipientId,
        string message,
        Dictionary<string, string>? metadata,
        CancellationToken ct)
    {
        // Enforce rate limiting
        using var lease = await _rateLimiter.AcquireAsync("email", 1, ct);
        if (!lease.IsAcquired)
        {
            _logger.LogWarning("Rate limit exceeded for Email provider");
            return DeliveryResult.RateLimited();
        }

        try
        {
            // Validate email format
            var validationResult = await ValidateRecipientAsync(recipientId, ct);
            if (!validationResult.IsValid)
            {
                return DeliveryResult.Failed(
                    failureType: DeliveryFailureType.InvalidRecipient,
                    errorMessage: validationResult.ErrorMessage ?? "Invalid email address",
                    isRetryable: false);
            }

            // If API is not configured, simulate sending
            if (_emailApi == null)
            {
                _logger.LogInformation(
                    "Simulating email send: To={Recipient}, MessageLength={Length}",
                    recipientId,
                    message.Length);

                await SystemTask.Delay(100, ct);
                var simulatedMessageId = $"email_simulated_{Guid.NewGuid():N}";

                return DeliveryResult.Successful(
                    messageId: simulatedMessageId,
                    providerResponse: "Email sent successfully (simulated - no API key configured)");
            }

            _logger.LogInformation(
                "Sending email via Brevo: To={Recipient}, MessageLength={Length}",
                recipientId,
                message.Length);

            // Extract subject from metadata or use default
            var subject = metadata?.GetValueOrDefault("subject") ?? "Notification from Maliev";
            var recipientName = metadata?.GetValueOrDefault("recipientName");

            // Create email send request
            var sendSmtpEmail = new SendSmtpEmail
            {
                Sender = new SendSmtpEmailSender(_senderName, _senderEmail),
                To = new List<SendSmtpEmailTo>
                {
                    new SendSmtpEmailTo(recipientId, recipientName)
                },
                Subject = subject,
                HtmlContent = message
            };

            // Add optional CC and BCC if provided in metadata
            if (metadata?.ContainsKey("cc") == true)
            {
                var ccEmails = metadata["cc"].Split(',', StringSplitOptions.RemoveEmptyEntries);
                sendSmtpEmail.Cc = ccEmails.Select(email => new SendSmtpEmailCc(email.Trim())).ToList();
            }

            if (metadata?.ContainsKey("bcc") == true)
            {
                var bccEmails = metadata["bcc"].Split(',', StringSplitOptions.RemoveEmptyEntries);
                sendSmtpEmail.Bcc = bccEmails.Select(email => new SendSmtpEmailBcc(email.Trim())).ToList();
            }

            // Send email
            var result = await _emailApi.SendTransacEmailAsync(sendSmtpEmail);

            _logger.LogInformation(
                "Email sent successfully via Brevo: To={Recipient}, MessageId={MessageId}",
                recipientId,
                result.MessageId);

            return DeliveryResult.Successful(
                messageId: result.MessageId,
                providerResponse: $"Email sent successfully. Message ID: {result.MessageId}");
        }
        catch (ApiException ex)
        {
            _logger.LogError(
                ex,
                "Brevo API error while sending email to {Recipient}: {ErrorCode} - {ErrorMessage}",
                recipientId,
                ex.ErrorCode,
                ex.Message);

            // Determine if error is retryable
            var isRetryable = ex.ErrorCode >= 500 || ex.ErrorCode == 429; // Server errors or rate limiting

            return DeliveryResult.Failed(
                failureType: isRetryable ? DeliveryFailureType.Transient : DeliveryFailureType.ProviderError,
                errorMessage: $"Brevo API error: {ex.Message}",
                isRetryable: isRetryable);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send email to {Recipient}",
                recipientId);

            return DeliveryResult.Failed(
                failureType: DeliveryFailureType.Transient,
                errorMessage: ex.Message,
                isRetryable: true);
        }
    }

    public System.Threading.Tasks.Task<ValidationResult> ValidateRecipientAsync(string recipientId, CancellationToken ct)
    {
        // Validate email format using regex
        if (string.IsNullOrWhiteSpace(recipientId))
        {
            return System.Threading.Tasks.Task.FromResult(ValidationResult.Invalid("Email address is required"));
        }

        if (!EmailRegex().IsMatch(recipientId))
        {
            return System.Threading.Tasks.Task.FromResult(ValidationResult.Invalid("Invalid email address format"));
        }

        return System.Threading.Tasks.Task.FromResult(ValidationResult.Valid());
    }

    public async System.Threading.Tasks.Task<bool> GetHealthAsync(CancellationToken ct)
    {
        // If API is not configured, return false
        if (_emailApi == null)
        {
            return false;
        }

        try
        {
            // Check Brevo API health by getting account information
            var accountApi = new AccountApi();
            await SystemTask.Run(() => accountApi.GetAccount(), ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Brevo API health check failed");
            return false;
        }
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();
}
