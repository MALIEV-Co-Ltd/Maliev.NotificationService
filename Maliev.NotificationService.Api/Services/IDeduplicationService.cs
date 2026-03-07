namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Service for detecting and preventing duplicate notification deliveries.
/// Uses Redis cache with SHA256-based keys and 24-hour TTL.
/// </summary>
public interface IDeduplicationService
{
    /// <summary>
    /// Checks if the given event is a duplicate within the 24-hour deduplication window.
    /// If not a duplicate, registers the event in the cache.
    /// </summary>
    /// <param name="eventId">Unique event identifier</param>
    /// <param name="timestamp">Event timestamp</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the event is a duplicate, false otherwise</returns>
    Task<bool> IsDuplicateAsync(string eventId, DateTimeOffset timestamp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears a specific event from the deduplication cache.
    /// Useful for manual reprocessing of failed events.
    /// </summary>
    /// <param name="eventId">Event identifier to clear</param>
    /// <param name="timestamp">Event timestamp</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearCacheEntryAsync(string eventId, DateTimeOffset timestamp, CancellationToken cancellationToken = default);
}
