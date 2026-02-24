using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Data;
using Maliev.NotificationService.Data.Entities;
using Maliev.NotificationService.Tests.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Maliev.NotificationService.Tests.Integration;

public class CleanupTests : IClassFixture<BaseIntegrationTestFactory<Program, NotificationDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, NotificationDbContext> _factory;

    public CleanupTests(BaseIntegrationTestFactory<Program, NotificationDbContext> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DeliveryLogCleanupService_ShouldDeleteOldLogs()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        using var context = _factory.CreateDbContext();

        var oldLog = new DeliveryLog
        {
            EventId = Guid.NewGuid().ToString(),
            UserId = "user1",
            ChannelType = "email",
            Status = "delivered",
            RecipientIdentifier = "test1@example.com"
        };
        var newLog = new DeliveryLog
        {
            EventId = Guid.NewGuid().ToString(),
            UserId = "user2",
            ChannelType = "email",
            Status = "delivered",
            RecipientIdentifier = "test2@example.com"
        };

        context.DeliveryLogs.AddRange(oldLog, newLog);
        await context.SaveChangesAsync();

        // Manually set oldLog CreatedAt to 91 days ago
        var cutoff = DateTimeOffset.UtcNow.AddDays(-91);
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE delivery_logs SET created_at = {0} WHERE id = {1}",
            cutoff, oldLog.Id);

        var cleanupService = _factory.Services.GetServices<IHostedService>()
            .OfType<DeliveryLogCleanupService>()
            .Single();

        // Act
        await cleanupService.CleanupDeliveryLogsAsync(null);

        // Assert
        var remainingLogs = await context.DeliveryLogs.ToListAsync();
        Assert.Single(remainingLogs);
        Assert.Equal(newLog.Id, remainingLogs[0].Id);
    }

    [Fact]
    public async Task RetryService_CleanupStaleRetries_ShouldDeleteOldEntries()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        using var context = _factory.CreateDbContext();
        var retryService = _factory.Services.GetRequiredService<IRetryService>();

        var oldRetry = new RetryQueueEntry
        {
            EventId = Guid.NewGuid().ToString(),
            EventPayload = "{}",
            AttemptNumber = 1,
            ScheduledTime = DateTimeOffset.UtcNow.AddHours(-1),
            LastError = "Error"
        };
        var newRetry = new RetryQueueEntry
        {
            EventId = Guid.NewGuid().ToString(),
            EventPayload = "{}",
            AttemptNumber = 1,
            ScheduledTime = DateTimeOffset.UtcNow.AddHours(1),
            LastError = "Error"
        };

        context.RetryQueueEntries.AddRange(oldRetry, newRetry);
        await context.SaveChangesAsync();

        // Manually set oldRetry CreatedAt to 25 hours ago
        var cutoff = DateTimeOffset.UtcNow.AddHours(-25);
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE retry_queue_entries SET created_at = {0} WHERE id = {1}",
            cutoff, oldRetry.Id);

        // Act
        await retryService.CleanupStaleRetriesAsync();

        // Assert
        var remainingEntries = await context.RetryQueueEntries.ToListAsync();
        Assert.Single(remainingEntries);
        Assert.Equal(newRetry.Id, remainingEntries[0].Id);
    }
}
