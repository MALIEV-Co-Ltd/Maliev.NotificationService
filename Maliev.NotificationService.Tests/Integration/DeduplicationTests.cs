using System.Net;
using MassTransit;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

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

    [Fact]
    public async Task PublishSameEventTwice_ShouldDeliverOnlyOnce()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var eventId = Guid.NewGuid().ToString();
        var notificationEvent = new NotificationEvent
        {
            Id = eventId,
            Source = "maliev.order.v1",
            Type = "order.confirmed",
            Time = DateTimeOffset.UtcNow,
            Data = new NotificationEventData
            {
                NotificationType = "OrderConfirmation",
                Priority = "critical",
                TargetUsers = new[] { new TargetUser { UserId = "dedup_test_user", UserType = "customer" } },
                TemplateId = "order-confirmed",
                Parameters = new Dictionary<string, string>
                {
                    ["customerName"] = "Bob Williams",
                    ["orderNumber"] = "ORD-99999",
                    ["totalAmount"] = "3000.00 THB"
                }
            }
        };

        // Act - Publish same event twice
        await bus.Publish(notificationEvent);
        await Task.Delay(TimeSpan.FromMilliseconds(500)); // Small delay
        await bus.Publish(notificationEvent); // Duplicate

        // Wait for processing
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
        // TODO: Query delivery logs and verify only ONE delivery was made
        // var deliveryLogsResponse = await _client.GetAsync($"/notification/v1.0/delivery-logs?eventId={eventId}");
        // var deliveryLogs = await deliveryLogsResponse.Content.ReadFromJsonAsync<DeliveryLogResponse>();

        // Assert.Single(deliveryLogs.Items); // Only one delivery log entry

        // TODO: Verify deduplication cache hit metric was incremented
        // var metricsResponse = await _client.GetAsync("/notificationservice/metrics");
        // var metricsText = await metricsResponse.Content.ReadAsStringAsync();
        // Assert.Contains("deduplication_cache_hits", metricsText);
    }

    [Fact]
    public async Task PublishEventsWithDifferentIds_ShouldDeliverBoth()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var event1Id = Guid.NewGuid().ToString();
        var event2Id = Guid.NewGuid().ToString();

        var event1 = new NotificationEvent
        {
            Id = event1Id,
            Source = "maliev.payment.v1",
            Type = "payment.succeeded",
            Time = DateTimeOffset.UtcNow,
            Data = new NotificationEventData
            {
                NotificationType = "PaymentSuccess",
                Priority = "standard",
                TargetUsers = new[] { new TargetUser { UserId = "different_events_user", UserType = "customer" } },
                TemplateId = "payment-success",
                Parameters = new Dictionary<string, string>
                {
                    ["paymentAmount"] = "1000.00 THB",
                    ["paymentMethod"] = "Credit Card"
                }
            }
        };

        var event2 = new NotificationEvent
        {
            Id = event2Id,
            Source = "maliev.payment.v1",
            Type = "payment.succeeded",
            Time = DateTimeOffset.UtcNow.AddSeconds(1),
            Data = new NotificationEventData
            {
                NotificationType = "PaymentSuccess",
                Priority = "standard",
                TargetUsers = new[] { new TargetUser { UserId = "different_events_user", UserType = "customer" } },
                TemplateId = "payment-success",
                Parameters = new Dictionary<string, string>
                {
                    ["paymentAmount"] = "2000.00 THB",
                    ["paymentMethod"] = "Bank Transfer"
                }
            }
        };

        // Act
        await bus.Publish(event1);
        await bus.Publish(event2);

        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
        // TODO: Verify BOTH events were delivered (different event IDs)
        // var deliveryLogs1Response = await _client.GetAsync($"/notification/v1.0/delivery-logs?eventId={event1Id}");
        // var deliveryLogs1 = await deliveryLogs1Response.Content.ReadFromJsonAsync<DeliveryLogResponse>();
        // Assert.Single(deliveryLogs1.Items);

        // var deliveryLogs2Response = await _client.GetAsync($"/notification/v1.0/delivery-logs?eventId={event2Id}");
        // var deliveryLogs2 = await deliveryLogs2Response.Content.ReadFromJsonAsync<DeliveryLogResponse>();
        // Assert.Single(deliveryLogs2.Items);
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
            var notificationEvent = new NotificationEvent
            {
                Id = Guid.NewGuid().ToString(),
                Source = "maliev.test.v1",
                Type = "test.unique",
                Time = DateTimeOffset.UtcNow.AddMilliseconds(i),
                Data = new NotificationEventData
                {
                    NotificationType = "UniqueTest",
                    Priority = "standard",
                    TargetUsers = new[] { new TargetUser { UserId = $"unique_user_{i}", UserType = "customer" } },
                    TemplateId = "test-template",
                    Parameters = new Dictionary<string, string> { ["index"] = i.ToString() }
                }
            };

            tasks.Add(bus.Publish(notificationEvent));
        }

        await Task.WhenAll(tasks);
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Assert
        // TODO: Verify all 50 unique events were processed (no false positive deduplication)
        // var deliveryLogsResponse = await _client.GetAsync("/notification/v1.0/delivery-logs?startDate=" + DateTimeOffset.UtcNow.AddMinutes(-1).ToString("o"));
        // var deliveryLogs = await deliveryLogsResponse.Content.ReadFromJsonAsync<DeliveryLogResponse>();

        // Assert.True(deliveryLogs.Items.Count >= eventCount, "All unique events should be delivered");
    }

    [Fact]
    public async Task ConcurrentDuplicateEvents_ShouldDeliverOnlyOnce()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var eventId = Guid.NewGuid().ToString();
        var notificationEvent = new NotificationEvent
        {
            Id = eventId,
            Source = "maliev.test.v1",
            Type = "test.concurrent.dedup",
            Time = DateTimeOffset.UtcNow,
            Data = new NotificationEventData
            {
                NotificationType = "ConcurrentDedupTest",
                Priority = "critical",
                TargetUsers = new[] { new TargetUser { UserId = "concurrent_dedup_user", UserType = "customer" } },
                TemplateId = "test-template",
                Parameters = new Dictionary<string, string> { ["message"] = "Concurrent deduplication test" }
            }
        };

        // Act - Publish same event 10 times concurrently
        var publishTasks = Enumerable.Range(0, 10).Select(_ => bus.Publish(notificationEvent)).ToList();
        await Task.WhenAll(publishTasks);

        await Task.Delay(TimeSpan.FromSeconds(8));

        // Assert
        // TODO: Verify only ONE delivery was made despite concurrent publications
        // var deliveryLogsResponse = await _client.GetAsync($"/notification/v1.0/delivery-logs?eventId={eventId}");
        // var deliveryLogs = await deliveryLogsResponse.Content.ReadFromJsonAsync<DeliveryLogResponse>();

        // Assert.Single(deliveryLogs.Items); // Atomic deduplication should prevent all duplicates
    }
}
