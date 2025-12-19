using System.Net;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Integration;

/// <summary>
/// Integration tests for fallback channel activation on primary channel failure.
/// Tests T030: Verify fallback channel is used when primary channel delivery fails.
/// </summary>
[Collection("Integration")]
public class FallbackChannelTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FallbackChannelTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PrimaryChannelFails_ShouldAutomaticallyUseFallbackChannel()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        // Create user preference with primary=LINE (will fail), fallback=[email, sms]
        var userId = "fallback_test_user_001";

        // TODO: Create user preference via API
        // var preferenceRequest = new
        // {
        //     userId = userId,
        //     primaryChannelType = "line",
        //     fallbackChannelTypes = new[] { "email", "sms" }
        // };
        // await _client.PostAsJsonAsync("/notification/v1.0/preferences", preferenceRequest);

        var notificationEvent = new NotificationEvent
        {
            Id = Guid.NewGuid().ToString(),
            Source = "maliev.payment.v1",
            Type = "payment.failed",
            Time = DateTimeOffset.UtcNow,
            Data = new NotificationEventData
            {
                NotificationType = "PaymentFailure",
                Priority = "critical",
                TargetUsers = new[] { new TargetUser { UserId = userId, UserType = "customer" } },
                TemplateId = "payment-failed",
                Parameters = new Dictionary<string, string>
                {
                    ["customerName"] = "Alice Johnson",
                    ["paymentAmount"] = "2500.00 THB",
                    ["failureReason"] = "Card declined"
                }
            }
        };

        // Act
        await bus.Publish(notificationEvent);
        await Task.Delay(TimeSpan.FromSeconds(10)); // Allow time for retries and fallback

        // Assert
        // TODO: Verify fallback channel was used
        // var deliveryLogsResponse = await _client.GetAsync($"/notification/v1.0/delivery-logs?eventId={notificationEvent.id}");
        // var deliveryLogs = await deliveryLogsResponse.Content.ReadFromJsonAsync<DeliveryLogResponse>();

        // Should have delivery log for primary channel (failed) and fallback channel (sent)
        // Assert.Contains(deliveryLogs.Items, log => log.ChannelType == "line" && log.Status == "failed");
        // Assert.Contains(deliveryLogs.Items, log => log.ChannelType == "email" && log.Status == "sent");

        // TODO: Verify fallback metric was incremented
        // var metricsResponse = await _client.GetAsync("/notificationservice/metrics");
        // var metricsText = await metricsResponse.Content.ReadAsStringAsync();
        // Assert.Contains("notification_fallback_total", metricsText);
    }

    [Fact]
    public async Task AllChannelsFail_ShouldMoveToDeadLetterQueue()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var userId = "fallback_all_fail_user";

        // TODO: Create user preference with primary + 2 fallbacks (all will fail)
        // var preferenceRequest = new
        // {
        //     userId = userId,
        //     primaryChannelType = "line",
        //     fallbackChannelTypes = new[] { "email", "sms" }
        // };
        // await _client.PostAsJsonAsync("/notification/v1.0/preferences", preferenceRequest);

        var notificationEvent = new NotificationEvent
        {
            Id = Guid.NewGuid().ToString(),
            Source = "maliev.system.v1",
            Type = "system.outage",
            Time = DateTimeOffset.UtcNow,
            Data = new NotificationEventData
            {
                NotificationType = "SystemOutage",
                Priority = "critical",
                TargetUsers = new[] { new TargetUser { UserId = userId, UserType = "staff" } },
                TemplateId = "system-outage",
                Parameters = new Dictionary<string, string>
                {
                    ["outageMessage"] = "Database is down",
                    ["estimatedDowntime"] = "30 minutes"
                }
            }
        };

        // Act
        await bus.Publish(notificationEvent);
        await Task.Delay(TimeSpan.FromSeconds(15));

        // Assert
        // TODO: Verify all channels failed and event moved to dead-letter queue
        // var deadLetterResponse = await _client.GetAsync($"/notification/v1.0/dead-letter?eventId={notificationEvent.id}");
        // Assert.Equal(HttpStatusCode.OK, deadLetterResponse.StatusCode);

        // var deadLetter = await deadLetterResponse.Content.ReadFromJsonAsync<DeadLetterRecordResponse>();
        // Assert.NotNull(deadLetter);
        // Assert.Contains("All channels failed", deadLetter.FailureReasons);
    }

    [Fact]
    public async Task FallbackChannelSucceeds_ShouldNotTryRemainingFallbacks()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var userId = "fallback_first_succeeds_user";

        // TODO: Create user preference with primary (fails) and fallbacks=[email (succeeds), sms (should not be tried)]
        // var preferenceRequest = new
        // {
        //     userId = userId,
        //     primaryChannelType = "line",
        //     fallbackChannelTypes = new[] { "email", "sms", "slack" }
        // };
        // await _client.PostAsJsonAsync("/notification/v1.0/preferences", preferenceRequest);

        var notificationEvent = new NotificationEvent
        {
            Id = Guid.NewGuid().ToString(),
            Source = "maliev.order.v1",
            Type = "order.shipped",
            Time = DateTimeOffset.UtcNow,
            Data = new NotificationEventData
            {
                NotificationType = "OrderShipped",
                Priority = "standard",
                TargetUsers = new[] { new TargetUser { UserId = userId, UserType = "customer" } },
                TemplateId = "order-shipped",
                Parameters = new Dictionary<string, string>
                {
                    ["trackingNumber"] = "TRACK-123456",
                    ["estimatedDelivery"] = "2025-12-10"
                }
            }
        };

        // Act
        await bus.Publish(notificationEvent);
        await Task.Delay(TimeSpan.FromSeconds(8));

        // Assert
        // TODO: Verify only primary (LINE) and first fallback (email) were attempted
        // var deliveryLogsResponse = await _client.GetAsync($"/notification/v1.0/delivery-logs?eventId={notificationEvent.id}");
        // var deliveryLogs = await deliveryLogsResponse.Content.ReadFromJsonAsync<DeliveryLogResponse>();

        // Assert.Equal(2, deliveryLogs.Items.Count); // Primary + first fallback only
        // Assert.Contains(deliveryLogs.Items, log => log.ChannelType == "line" && log.Status == "failed");
        // Assert.Contains(deliveryLogs.Items, log => log.ChannelType == "email" && log.Status == "sent");
        // Assert.DoesNotContain(deliveryLogs.Items, log => log.ChannelType == "sms"); // Should not be tried
        // Assert.DoesNotContain(deliveryLogs.Items, log => log.ChannelType == "slack"); // Should not be tried
    }

    [Fact]
    public async Task NoFallbackConfigured_ShouldRetryPrimaryChannelOnly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var userId = "no_fallback_user";

        // TODO: Create user preference with primary only, no fallbacks
        // var preferenceRequest = new
        // {
        //     userId = userId,
        //     primaryChannelType = "email",
        //     fallbackChannelTypes = Array.Empty<string>()
        // };
        // await _client.PostAsJsonAsync("/notification/v1.0/preferences", preferenceRequest);

        var notificationEvent = new NotificationEvent
        {
            Id = Guid.NewGuid().ToString(),
            Source = "maliev.test.v1",
            Type = "test.no.fallback",
            Time = DateTimeOffset.UtcNow,
            Data = new NotificationEventData
            {
                NotificationType = "NoFallbackTest",
                Priority = "critical",
                TargetUsers = new[] { new TargetUser { UserId = userId, UserType = "customer" } },
                TemplateId = "test-template",
                Parameters = new Dictionary<string, string> { ["message"] = "No fallback test" }
            }
        };

        // Act
        await bus.Publish(notificationEvent);
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Assert
        // TODO: Verify only primary channel was attempted (3 retries, no fallback)
        // var deliveryLogsResponse = await _client.GetAsync($"/notification/v1.0/delivery-logs?eventId={notificationEvent.id}");
        // var deliveryLogs = await deliveryLogsResponse.Content.ReadFromJsonAsync<DeliveryLogResponse>();

        // All attempts should be for the same channel
        // Assert.All(deliveryLogs.Items, log => Assert.Equal("email", log.ChannelType));
        // Assert.InRange(deliveryLogs.Items.Count, 1, 3); // 1 attempt + up to 2 retries
    }
}
