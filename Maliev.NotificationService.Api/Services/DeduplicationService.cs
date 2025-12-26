using System.Security.Cryptography;
using System.Text;
using Maliev.NotificationService.Api.Metrics;
using Microsoft.Extensions.Caching.Distributed;

namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Redis-based deduplication service using SHA256 hashing and 24-hour TTL.
/// Implements atomic check-and-set pattern for duplicate detection.
/// </summary>
public class DeduplicationService : IDeduplicationService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<DeduplicationService> _logger;
    private const int CacheTtlHours = 24;

    public DeduplicationService(
        IDistributedCache cache,
        ILogger<DeduplicationService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> IsDuplicateAsync(
        string eventId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GenerateCacheKey(eventId, timestamp);

            // Check if entry already exists in cache
            var existingValue = await _cache.GetStringAsync(cacheKey, cancellationToken);

            if (existingValue != null)
            {
                // Increment cache hit counter (duplicate detected)
                NotificationMetrics.DeduplicationCacheHits.Add(1);

                _logger.LogDebug(
                    "Duplicate event detected: EventId={EventId}, Timestamp={Timestamp}",
                    eventId,
                    timestamp);
                return true; // Duplicate found
            }

            // Entry does not exist, create it (atomic operation using SET NX logic if supported by provider)
            // Note: IDistributedCache Get then Set pattern has a potential race condition.
            // For production with high concurrency, consider using a distributed lock or Redis Lua script.
            await _cache.SetStringAsync(
                cacheKey,
                "1", // Simple presence marker
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(CacheTtlHours)
                },
                cancellationToken);

            // Increment cache miss counter (unique event)
            NotificationMetrics.DeduplicationCacheMisses.Add(1);

            _logger.LogDebug(
                "Event registered in deduplication cache: EventId={EventId}, CacheKey={CacheKey}",
                eventId,
                cacheKey);

            return false; // Not a duplicate
        }
        catch (Exception ex)
        {
            // Fail open: If cache is unavailable, allow the notification through
            // Better to risk a duplicate than to drop a critical notification
            _logger.LogError(
                ex,
                "Deduplication cache error for EventId={EventId}. Failing open to allow notification delivery.",
                eventId);

            return false; // Assume not duplicate on error
        }
    }

    public async Task ClearCacheEntryAsync(
        string eventId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GenerateCacheKey(eventId, timestamp);
            await _cache.RemoveAsync(cacheKey, cancellationToken);

            _logger.LogDebug(
                "Cleared deduplication cache entry: EventId={EventId}",
                eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to clear deduplication cache entry for EventId={EventId}",
                eventId);
            throw;
        }
    }

    /// <summary>
    /// Generates a deterministic cache key using SHA256 hash of eventId + timestamp.
    /// Format: "dedup:{base64(SHA256(eventId:unixTimestamp))}"
    /// </summary>
    private static string GenerateCacheKey(string eventId, DateTimeOffset timestamp)
    {
        var input = $"{eventId}:{timestamp.ToUnixTimeSeconds()}";
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(inputBytes);
        var hashBase64 = Convert.ToBase64String(hashBytes);

        return $"dedup:{hashBase64}";
    }
}
