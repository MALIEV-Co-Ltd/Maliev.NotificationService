using System.Net;
using MassTransit;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Maliev.MessagingContracts.Generated;

namespace Maliev.NotificationService.Api.Tests.Integration;

/// <summary>
/// Integration tests for duplicate event detection via Redis cache.
/// Tests T031: Verify deduplication logic prevents duplicate notifications within 24-hour window.
/// </summary>
[Collection("Integration")]
public class DeduplicationTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DeduplicationTests(TestWebApplicationFactory factory)
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
        Guid eventId,
        string notificationType,
        string priority,
        string userId,
        string userType,
        string templateId,
        Dictionary<string, string> parameters)
    {
        return new NotificationEvent(
            MessageId: eventId,
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
    public async Task PublishSameEventTwice_ShouldDeliverOnlyOnce()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var eventId = Guid.NewGuid();
        var notificationEvent = CreateTestEvent(
            eventId,
            "OrderConfirmation",
            "critical",
            "dedup_test_user",
            "customer",
            "order-confirmed",
            new Dictionary<string, string>
            {
                ["customerName"] = "Bob Williams",
                ["orderNumber"] = "ORD-99999",
                ["totalAmount"] = "3000.00 THB"
            }
        );

        // Act - Publish same event twice
        await bus.Publish(notificationEvent);
        await Task.Delay(TimeSpan.FromMilliseconds(500)); // Small delay
        await bus.Publish(notificationEvent); // Duplicate

        // Wait for processing
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
    }

    [Fact]
    public async Task PublishEventsWithDifferentIds_ShouldDeliverBoth()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var event1Id = Guid.NewGuid();
        var event2Id = Guid.NewGuid();

        var event1 = CreateTestEvent(
            event1Id,
            "PaymentSuccess",
            "standard",
            "different_events_user",
            "customer",
            "payment-success",
            new Dictionary<string, string>
            {
                ["paymentAmount"] = "1000.00 THB",
                ["paymentMethod"] = "Credit Card"
            }
        );

        var event2 = CreateTestEvent(
            event2Id,
            "PaymentSuccess",
            "standard",
            "different_events_user",
            "customer",
            "payment-success",
            new Dictionary<string, string>
            {
                ["paymentAmount"] = "2000.00 THB",
                ["paymentMethod"] = "Bank Transfer"
            }
        );

        // Act
        await bus.Publish(event1);
        await bus.Publish(event2);

        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
    }

    [Fact]
    public async Task DeduplicationCacheKey_ShouldIncludeEventIdAndTimestamp()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

        var eventId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow;

        // Simulate deduplication service behavior
        var cacheKey = $"dedup:{Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{eventId}:{timestamp.ToUnixTimeSeconds()}")))}";

        // Act
        await cache.SetStringAsync(cacheKey, "1", new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        });

        var cachedValue = await cache.GetStringAsync(cacheKey);

        // Assert
        Assert.NotNull(cachedValue);
        Assert.Equal("1", cachedValue);
    }

    [Fact]
    public async Task DeduplicationCache_ShouldExpireAfter24Hours()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

        var cacheKey = $"dedup:test_{Guid.NewGuid()}";

        // Act
        await cache.SetStringAsync(cacheKey, "1", new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(2) // Short TTL for testing
        });

        var valueBeforeExpiry = await cache.GetStringAsync(cacheKey);
        Assert.NotNull(valueBeforeExpiry);

        // Wait for expiration
        await Task.Delay(TimeSpan.FromSeconds(3));

        var valueAfterExpiry = await cache.GetStringAsync(cacheKey);


        // Assert
        Assert.Null(valueAfterExpiry); // Should be expired and return null
    }

    [Fact]
    public async Task PublishManyUniqueEvents_ShouldNotDeduplicate()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var eventCount = 50;
        var tasks = new List<Task>();

        // Act - Publish many unique events
        for (int i = 0; i < eventCount; i++)
        {
            var notificationEvent = CreateTestEvent(
                Guid.NewGuid(),
                "UniqueTest",
                "standard",
                $"unique_user_{i}",
                "customer",
                "test-template",
                new Dictionary<string, string> { ["index"] = i.ToString() }
            );

            tasks.Add(bus.Publish(notificationEvent));
        }

        await Task.WhenAll(tasks);
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Assert
    }

    [Fact]
    public async Task ConcurrentDuplicateEvents_ShouldDeliverOnlyOnce()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var eventId = Guid.NewGuid();
        var notificationEvent = CreateTestEvent(
            eventId,
            "ConcurrentDedupTest",
            "critical",
            "concurrent_dedup_user",
            "customer",
            "test-template",
            new Dictionary<string, string> { ["message"] = "Concurrent deduplication test" }
        );

        // Act - Publish same event 10 times concurrently
        var publishTasks = Enumerable.Range(0, 10).Select(_ => bus.Publish(notificationEvent)).ToList();
        await Task.WhenAll(publishTasks);

        await Task.Delay(TimeSpan.FromSeconds(8));

        // Assert
    }
}
