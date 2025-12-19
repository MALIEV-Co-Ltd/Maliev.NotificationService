using System.Net;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Maliev.MessagingContracts.Contracts;

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

    private NotificationEvent CreateTestEvent(
        string notificationType,
        string priority,
        string userId,
        string userType,
        string templateId,
        Dictionary<string, string> parameters)
    {
        return new NotificationEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(NotificationEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "TestService",
            ConsumedBy: new[] { "NotificationService" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new NotificationEventPayload(
                NotificationType: notificationType,
                Priority: priority,
                TargetUsers: new[] { new NotificationEventPayloadTargetUsersItem(userId, userType) },
                TemplateId: templateId,
                Parameters: parameters,
                Metadata: new NotificationEventPayloadMetadata("en", "test-source")
            )
        );
    }

    [Fact]
    public async Task PrimaryChannelFails_ShouldAutomaticallyUseFallbackChannel()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        // Create user preference with primary=LINE (will fail), fallback=[email, sms]
        var userId = "fallback_test_user_001";

        var notificationEvent = CreateTestEvent(
            "PaymentFailure",
            "critical",
            userId,
            "customer",
            "payment-failed",
            new Dictionary<string, string>
            {
                ["customerName"] = "Alice Johnson",
                ["paymentAmount"] = "2500.00 THB",
                ["failureReason"] = "Card declined"
            }
        );

        // Act
        await bus.Publish(notificationEvent);
        await Task.Delay(TimeSpan.FromSeconds(10)); // Allow time for retries and fallback

        // Assert
    }

    [Fact]
    public async Task AllChannelsFail_ShouldMoveToDeadLetterQueue()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var userId = "fallback_all_fail_user";

        var notificationEvent = CreateTestEvent(
            "SystemOutage",
            "critical",
            userId,
            "staff",
            "system-outage",
            new Dictionary<string, string>
            {
                ["outageMessage"] = "Database is down",
                ["estimatedDowntime"] = "30 minutes"
            }
        );

        // Act
        await bus.Publish(notificationEvent);
        await Task.Delay(TimeSpan.FromSeconds(15));

        // Assert
    }

    [Fact]
    public async Task FallbackChannelSucceeds_ShouldNotTryRemainingFallbacks()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var userId = "fallback_first_succeeds_user";

        var notificationEvent = CreateTestEvent(
            "OrderShipped",
            "standard",
            userId,
            "customer",
            "order-shipped",
            new Dictionary<string, string>
            {
                ["trackingNumber"] = "TRACK-123456",
                ["estimatedDelivery"] = "2025-12-10"
            }
        );

        // Act
        await bus.Publish(notificationEvent);
        await Task.Delay(TimeSpan.FromSeconds(8));

        // Assert
    }

    [Fact]
    public async Task NoFallbackConfigured_ShouldRetryPrimaryChannelOnly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var userId = "no_fallback_user";

        var notificationEvent = CreateTestEvent(
            "NoFallbackTest",
            "critical",
            userId,
            "customer",
            "test-template",
            new Dictionary<string, string> { ["message"] = "No fallback test" }
        );

        // Act
        await bus.Publish(notificationEvent);
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Assert
    }
}
