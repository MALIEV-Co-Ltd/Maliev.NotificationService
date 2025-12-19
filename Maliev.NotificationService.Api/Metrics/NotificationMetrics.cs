using System.Diagnostics.Metrics;

namespace Maliev.NotificationService.Api.Metrics;

public static class NotificationMetrics
{
    private static readonly Meter Meter = new("Maliev.NotificationService", "1.0");

    // Counters
    public static readonly Counter<long> NotificationsSent = Meter.CreateCounter<long>(
        "notification_sent_total",
        description: "Total number of notifications sent");

    public static readonly Counter<long> NotificationsFailed = Meter.CreateCounter<long>(
        "notification_failed_total",
        description: "Total number of failed notifications");

    public static readonly Counter<long> FallbackTriggered = Meter.CreateCounter<long>(
        "notification_fallback_total",
        description: "Total number of fallback channel activations");

    public static readonly Counter<long> DeduplicationCacheHits = Meter.CreateCounter<long>(
        "notification_deduplication_cache_hits",
        description: "Number of duplicate events detected");

    public static readonly Counter<long> DeduplicationCacheMisses = Meter.CreateCounter<long>(
        "notification_deduplication_cache_misses",
        description: "Number of unique events processed");

    // Histogram for latency
    public static readonly Histogram<double> DeliveryLatency = Meter.CreateHistogram<double>(
        "notification_delivery_latency_ms",
        unit: "ms",
        description: "Notification delivery latency");

    // Observable Gauge for queue depth
    public static ObservableGauge<int>? RetryQueueDepth { get; private set; }

    public static void Initialize(Func<int> getRetryQueueDepth)
    {
        RetryQueueDepth = Meter.CreateObservableGauge(
            "notification_retry_queue_depth",
            getRetryQueueDepth,
            description: "Current retry queue depth");
    }
}
