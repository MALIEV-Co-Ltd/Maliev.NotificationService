using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Background service that permanently deletes delivery logs older than 90 days.
/// Runs daily at 2 AM UTC using timer-based execution (no archival to cold storage).
/// </summary>
public class DeliveryLogCleanupService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DeliveryLogCleanupService> _logger;
    private Timer? _timer;
    private const int RetentionDays = 90;

    public DeliveryLogCleanupService(
        IServiceProvider serviceProvider,
        ILogger<DeliveryLogCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Delivery log cleanup service starting");

        // Calculate time until next 2 AM UTC
        var now = DateTimeOffset.UtcNow;
        var next2Am = now.Date.AddDays(1).AddHours(2); // Tomorrow at 2 AM UTC

        if (now.Hour < 2)
        {
            // If current time is before 2 AM, run today at 2 AM
            next2Am = now.Date.AddHours(2);
        }

        var initialDelay = next2Am - now;

        // Ensure delay is never negative (can happen during fast startup/tests)
        if (initialDelay < TimeSpan.Zero)
        {
            _logger.LogWarning("Calculated negative delay, scheduling for next day at 2 AM");
            initialDelay = TimeSpan.FromHours(24) + initialDelay;
        }

        // Ensure minimum delay to avoid timer errors
        if (initialDelay < TimeSpan.FromSeconds(1))
        {
            initialDelay = TimeSpan.FromSeconds(1);
        }

        _logger.LogInformation(
            "Delivery log cleanup service scheduled. Next run: {NextRun} (in {Hours:F2} hours)",
            next2Am,
            initialDelay.TotalHours);

        // Schedule timer to run daily at 2 AM UTC
        _timer = new Timer(
            async state => await CleanupDeliveryLogsAsync(state),
            null,
            initialDelay,
            TimeSpan.FromDays(1)); // Run every 24 hours

        return Task.CompletedTask;
    }

    internal async Task CleanupDeliveryLogsAsync(object? state)

    {
        try
        {
            _logger.LogInformation("Starting delivery log cleanup");

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-RetentionDays);

            // Efficiently delete old logs using ExecuteDeleteAsync (no archival)
            var deletedCount = await dbContext.DeliveryLogs
                .Where(l => l.CreatedAt < cutoffDate)
                .ExecuteDeleteAsync();

            _logger.LogInformation(
                "Delivery log cleanup completed: Deleted {Count} logs older than {Days} days (cutoff: {CutoffDate})",
                deletedCount,
                RetentionDays,
                cutoffDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error during delivery log cleanup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Delivery log cleanup service stopping");

        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
