using Maliev.NotificationService.Api.Services;

namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Background service that periodically cleans up stale retry queue entries.
/// </summary>
public class RetryCleanupBackgroundService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RetryCleanupBackgroundService> _logger;
    private Timer? _timer;

    public RetryCleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<RetryCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retry cleanup background service starting");

        // Run every hour, starting in 5 minutes
        _timer = new Timer(
            async state => await DoWorkAsync(state),
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromHours(1));

        return Task.CompletedTask;
    }

    private async Task DoWorkAsync(object? state)
    {
        try
        {
            _logger.LogInformation("Starting stale retry cleanup");

            using var scope = _serviceProvider.CreateScope();
            var retryService = scope.ServiceProvider.GetRequiredService<IRetryService>();

            await retryService.CleanupStaleRetriesAsync();

            _logger.LogInformation("Completed stale retry cleanup");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during stale retry cleanup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retry cleanup background service stopping");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
