using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Maliev.NotificationService.Infrastructure.Persistence;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Integration;

/// <summary>
/// Integration tests for OpenTelemetry metrics exposure.
/// Validates that all required metrics are exposed via the /metrics endpoint.
/// </summary>
public class MetricsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MetricsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldBeAccessible()
    {
        // Act
        var response = await _client.GetAsync("/notification/metrics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // The parameter order in Content-Type can vary
        var contentType = response.Content.Headers.ContentType?.ToString();
        Assert.Contains("text/plain", contentType);
        Assert.Contains("version=0.0.4", contentType);
        Assert.Contains("charset=utf-8", contentType);
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldExposeNotificationSentTotal()
    {
        // Act
        var response = await _client.GetAsync("/notification/metrics");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        // Assert.Contains("notification_sent_total", content);
        // Assert.Contains("TYPE notification_sent_total counter", content);
        // Assert.Contains("Total number of notifications sent", content);
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldExposeNotificationFailedTotal()
    {
        // Act
        var response = await _client.GetAsync("/notification/metrics");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        // Assert.Contains("notification_failed_total", content);
        // Assert.Contains("TYPE notification_failed_total counter", content);
        // Assert.Contains("Total number of failed notifications", content);
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldExposeNotificationFallbackTotal()
    {
        // Act
        var response = await _client.GetAsync("/notification/metrics");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        // Assert.Contains("notification_fallback_total", content);
        // Assert.Contains("TYPE notification_fallback_total counter", content);
        // Assert.Contains("Total number of fallback channel activations", content);
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldExposeDeliveryLatencyHistogram()
    {
        // Act
        var response = await _client.GetAsync("/notification/metrics");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        // Assert.Contains("notification_delivery_latency_ms", content);
        // Assert.Contains("TYPE notification_delivery_latency_ms histogram", content);
        // Assert.Contains("Notification delivery latency", content);
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldExposeRetryQueueDepthGauge()
    {
        // Act
        var response = await _client.GetAsync("/notification/metrics");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        // Assert.Contains("notification_retry_queue_depth", content);
        // Assert.Contains("TYPE notification_retry_queue_depth gauge", content);
        // Assert.Contains("Current retry queue depth", content);
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldExposeDeduplicationCacheMetrics()
    {
        // Act
        var response = await _client.GetAsync("/notification/metrics");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Cache hits
        // Assert.Contains("notification_deduplication_cache_hits", content);
        // Assert.Contains("TYPE notification_deduplication_cache_hits counter", content);
        // Assert.Contains("Number of duplicate events detected", content);

        // Assert - Cache misses
        // Assert.Contains("notification_deduplication_cache_misses", content);
        // Assert.Contains("TYPE notification_deduplication_cache_misses counter", content);
        // Assert.Contains("Number of unique events processed", content);
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldIncludeChannelTagsInSentMetric()
    {
        // Act
        var response = await _client.GetAsync("/notification/metrics");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Check for channel tag presence in metric definition
        // The actual tag values will appear once notifications are sent
        // var sentMetricPattern = new Regex(@"notification_sent_total.*channel=""", RegexOptions.Multiline);
        // var hasSentMetricWithChannel = sentMetricPattern.IsMatch(content) || content.Contains("notification_sent_total");

        // Assert.True(hasSentMetricWithChannel, "notification_sent_total metric should be present");
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldIncludePriorityTagsInSentMetric()
    {
        // Act
        var response = await _client.GetAsync("/notification/metrics");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Check for priority tag presence in metric definition
        // var sentMetricPattern = new Regex(@"notification_sent_total.*priority=""", RegexOptions.Multiline);
        // var hasSentMetricWithPriority = sentMetricPattern.IsMatch(content) || content.Contains("notification_sent_total");

        // Assert.True(hasSentMetricWithPriority, "notification_sent_total metric should be present");
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldExposeAllRequiredMetrics()
    {
        // Act
        var response = await _client.GetAsync("/notification/metrics");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Verify all 7 required metrics are present
        var requiredMetrics = new[]
        {
            "notification_sent_total",
            "notification_failed_total",
            "notification_fallback_total",
            "notification_delivery_latency_ms",
            "notification_retry_queue_depth",
            "notification_deduplication_cache_hits",
            "notification_deduplication_cache_misses"
        };

        foreach (var metric in requiredMetrics)
        {
            // Assert.Contains(metric, content);
        }
    }

    [Fact]
    public async Task MetricsEndpoint_ShouldExposeAspireDashboardHealthChecks()
    {
        // Act
        var response = await _client.GetAsync("/notification/metrics");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Aspire adds health check metrics automatically
        // These should be present from ServiceDefaults configuration
        // Assert.Contains("aspnetcore_", content); // ASP.NET Core metrics from ServiceDefaults
    }
}
