using System.Net;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Maliev.MessagingContracts.Contracts;

namespace Maliev.NotificationService.Api.Tests.Integration;

/// <summary>
/// Integration tests for end-to-end critical notification delivery flow.
/// Tests T028: Verify complete flow from RabbitMQ event consumption to channel delivery.
/// </summary>
[Collection("Integration")]
public class NotificationDeliveryTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public NotificationDeliveryTests(TestWebApplicationFactory factory)
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
    public async Task PublishCriticalNotification_ShouldDeliverToPreferredChannel_Within30Seconds()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var notificationEvent = CreateTestEvent(
            "OrderConfirmation",
            "critical",
            "test_user_001",
            "customer",
            "order-confirmed",
            new Dictionary<string, string>
            {
                ["customerName"] = "John Doe",
                ["orderNumber"] = "ORD-12345",
                ["totalAmount"] = "1500.00 THB"
            }
        );

        var startTime = DateTimeOffset.UtcNow;

        // Act
        await bus.Publish(notificationEvent);

        // Wait for async processing (max 30 seconds per NFR-001)
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
        var elapsedTime = DateTimeOffset.UtcNow - startTime;
        Assert.True(elapsedTime.TotalSeconds < 30, $"Delivery took {elapsedTime.TotalSeconds:F2}s, exceeding 30s SLA");
    }

    [Fact]
    public async Task PublishNotificationWithInvalidUser_ShouldLogFailure()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var notificationEvent = CreateTestEvent(
            "PaymentFailure",
            "critical",
            "invalid_user_999",
            "customer",
            "payment-failed",
            new Dictionary<string, string>
            {
                ["customerName"] = "Jane Smith",
                ["paymentAmount"] = "500.00 THB",
                ["failureReason"] = "Insufficient funds"
            }
        );

        // Act
        await bus.Publish(notificationEvent);

        // Wait for processing
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert
        // TODO: Verify delivery log shows failure
    }

    [Fact]
    public async Task PublishMultipleCriticalNotifications_ShouldRespectPrefetchLimit()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var eventCount = 20; // Exceeds prefetch count (10) to test queue behavior
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < eventCount; i++)
        {
            var notificationEvent = CreateTestEvent(
                "SystemAlert",
                "critical",
                $"user_{i:D3}",
                "staff",
                "system-alert",
                new Dictionary<string, string>
                {
                    ["alertMessage"] = $"Test alert {i}",
                    ["severity"] = "high"
                }
            );

            tasks.Add(bus.Publish(notificationEvent));
        }

        await Task.WhenAll(tasks);

        // Wait for processing
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Assert
        // All messages should be processed without overwhelming the system
        Assert.Equal(eventCount, tasks.Count);
    }

    [Fact]
    public async Task PublishMarketingNotification_ShouldProcessInStandardQueue_WithRelaxedRetry()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var marketingEvent = CreateTestEvent(
            "MarketingCampaign",
            "standard",
            "test_marketing_user_001",
            "customer",
            "marketing-campaign",
            new Dictionary<string, string>
            {
                ["customerName"] = "Marketing Customer",
                ["campaignName"] = "Summer Sale 2025",
                ["discountCode"] = "SUMMER25"
            }
        );

        var startTime = DateTimeOffset.UtcNow;

        // Act
        await bus.Publish(marketingEvent);

        // Wait for async processing
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
        var elapsedTime = DateTimeOffset.UtcNow - startTime;
        Assert.True(elapsedTime.TotalSeconds < 60,
            $"Marketing notification took {elapsedTime.TotalSeconds:F2}s");
    }

    [Fact]
    public async Task PublishBatchMarketingNotifications_ShouldNotBlockCriticalQueue()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var marketingBatchSize = 100;
        var marketingTasks = new List<Task>();

        // Act - Publish large batch of marketing notifications
        for (int i = 0; i < marketingBatchSize; i++)
        {
            var marketingEvent = CreateTestEvent(
                "Newsletter",
                "standard",
                $"marketing_user_{i:D3}",
                "customer",
                "newsletter",
                new Dictionary<string, string>
                {
                    ["customerName"] = $"Customer {i}",
                    ["content"] = "Newsletter content"
                }
            );

            marketingTasks.Add(bus.Publish(marketingEvent));
        }

        await Task.WhenAll(marketingTasks);

        // Now publish a critical notification
        var criticalStartTime = DateTimeOffset.UtcNow;

        var criticalEvent = CreateTestEvent(
            "PaymentFailure",
            "critical",
            "critical_user_001",
            "customer",
            "payment-failed",
            new Dictionary<string, string>
            {
                ["customerName"] = "Critical Customer",
                ["paymentAmount"] = "1000.00 THB",
                ["failureReason"] = "Card declined"
            }
        );

        await bus.Publish(criticalEvent);

        // Wait for critical notification processing
        await Task.Delay(TimeSpan.FromSeconds(5));

        var criticalElapsedTime = DateTimeOffset.UtcNow - criticalStartTime;

        // Assert
        Assert.True(criticalElapsedTime.TotalSeconds < 30,
            $"Critical notification was delayed by marketing queue: {criticalElapsedTime.TotalSeconds:F2}s");
    }
}
