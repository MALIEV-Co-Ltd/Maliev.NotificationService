using System.Text;
using System.Text.Json;

namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Alerting service that sends HTTP POST webhooks to external monitoring systems
/// when critical notification delivery failures occur.
/// </summary>
public class AlertingService : IAlertingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AlertingService> _logger;

    public AlertingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AlertingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendCriticalFailureAlertAsync(
        string eventId,
        string userId,
        string notificationType,
        string failureReason,
        int attemptCount,
        CancellationToken cancellationToken = default)
    {
        var webhookUrl = _configuration["Alerting:WebhookUrl"];

        if (string.IsNullOrEmpty(webhookUrl))
        {
            _logger.LogWarning(
                "Alerting webhook URL not configured. Skipping alert for critical failure: EventId={EventId}",
                eventId);
            return;
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient("Alerting");

            var alertPayload = new
            {
                severity = "critical",
                service = "notification-service",
                alert_type = "notification_delivery_failure",
                timestamp = DateTimeOffset.UtcNow,
                details = new
                {
                    event_id = eventId,
                    user_id = userId,
                    notification_type = notificationType,
                    failure_reason = failureReason,
                    attempt_count = attemptCount,
                    all_channels_exhausted = true
                },
                message = $"Critical notification delivery failure after {attemptCount} attempts: {notificationType} for user {userId}"
            };

            var json = JsonSerializer.Serialize(alertPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation(
                "Sending critical failure alert to monitoring system: EventId={EventId}, UserId={UserId}, Type={Type}",
                eventId,
                userId,
                notificationType);

            var response = await httpClient.PostAsync(webhookUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Successfully sent critical failure alert: EventId={EventId}, StatusCode={StatusCode}",
                    eventId,
                    response.StatusCode);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to send critical failure alert: EventId={EventId}, StatusCode={StatusCode}, Reason={Reason}",
                    eventId,
                    response.StatusCode,
                    response.ReasonPhrase);
            }
        }
        catch (Exception ex)
        {
            // Don't throw - alerting failure shouldn't break notification processing
            _logger.LogError(
                ex,
                "Error sending critical failure alert: EventId={EventId}",
                eventId);
        }
    }
}
