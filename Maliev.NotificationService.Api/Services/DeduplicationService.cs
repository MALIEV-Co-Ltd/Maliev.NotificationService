using Maliev.NotificationService.Api.Metrics;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography;
using System.Text;

namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Deduplication service using database atomic insert with Redis as a performance cache.
/// The database unique constraint on EventId provides true ACID-level atomicity.
/// Redis provides a fast path to avoid unnecessary DB writes for known duplicates.
/// </summary>
public class DeduplicationService : IDeduplicationService
{
    private readonly IDistributedCache _cache;
    private readonly NotificationDbContext _dbContext;
    private readonly ILogger<DeduplicationService> _logger;
    private const int CacheTtlHours = 24;

    public DeduplicationService(
        IDistributedCache cache,
        NotificationDbContext dbContext,
        ILogger<DeduplicationService> logger)
    {
        _cache = cache;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> IsDuplicateAsync(
        string eventId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateCacheKey(eventId, timestamp);

        // Fast path: check Redis cache first to avoid DB hit for known duplicates
        try
        {
            var cached = await _cache.GetAsync(cacheKey, cancellationToken);
            if (cached != null)
            {
                NotificationMetrics.DeduplicationCacheHits.Add(1);
                _logger.LogDebug("Duplicate event detected via cache: EventId={EventId}", eventId);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache read failed for EventId={EventId}. Falling through to DB check.", eventId);
        }

        // Atomic path: use database unique constraint on EventId
        // If the INSERT succeeds, this is the first processing attempt.
        // If a unique constraint violation occurs, another process already claimed this event.
        try
        {
            var entry = new DeduplicationEntry
            {
                EventId = eventId,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(CacheTtlHours)
            };
            _dbContext.DeduplicationEntries.Add(entry);
            await _dbContext.SaveChangesAsync(cancellationToken);

            NotificationMetrics.DeduplicationCacheMisses.Add(1);
            _logger.LogDebug("Event registered in deduplication store: EventId={EventId}", eventId);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            NotificationMetrics.DeduplicationCacheHits.Add(1);
            _logger.LogDebug("Duplicate event detected via DB constraint: EventId={EventId}", eventId);
            return true;
        }

        // Populate Redis cache for future fast-path lookups (best effort)
        try
        {
            var markerBytes = Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToString("O"));
            await _cache.SetAsync(
                cacheKey,
                markerBytes,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(CacheTtlHours)
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to populate Redis cache for EventId={EventId}. Dedup still protected by DB.", eventId);
        }

        return false;
    }

    public async Task ClearCacheEntryAsync(
        string eventId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateCacheKey(eventId, timestamp);

        try
        {
            await _cache.RemoveAsync(cacheKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear Redis cache entry for EventId={EventId}", eventId);
        }

        var dbEntry = await _dbContext.DeduplicationEntries
            .FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
        if (dbEntry is not null)
        {
            _dbContext.DeduplicationEntries.Remove(dbEntry);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogDebug("Cleared deduplication entry: EventId={EventId}", eventId);
    }

    private static string GenerateCacheKey(string eventId, DateTimeOffset timestamp)
    {
        var input = $"{eventId}:{timestamp.ToUnixTimeSeconds()}";
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(inputBytes);
        var hashBase64 = Convert.ToBase64String(hashBytes);
        return $"dedup:{hashBase64}";
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException?.Message?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true
            || ex.InnerException?.Message?.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true
            || ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }
}
