using Moq;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.MessagingContracts.Generated;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Data;
using Maliev.NotificationService.Data.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Maliev.NotificationService.Tests.Testing;

namespace Maliev.NotificationService.Api.Tests.Integration;

public class RetryServiceTests : IClassFixture<BaseIntegrationTestFactory<Program, NotificationDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, NotificationDbContext> _factory;

    public RetryServiceTests(BaseIntegrationTestFactory<Program, NotificationDbContext> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(1, "critical", 1)]
    [InlineData(2, "critical", 2)]
    [InlineData(3, "critical", 4)]
    [InlineData(1, "standard", 5)]
    [InlineData(2, "standard", 5)]
    public void CalculateRetryDelay_ShouldReturnExpectedDelay(int attempt, string priority, double expectedSeconds)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var retryService = scope.ServiceProvider.GetRequiredService<IRetryService>();

        // Act
        var delay = retryService.CalculateRetryDelay(attempt, priority);

        // Assert
        Assert.Equal(expectedSeconds, delay.TotalSeconds);
    }

    [Fact]
    public async Task ScheduleRetryAsync_MaxRetriesExceeded_ReturnsFailed()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var retryService = scope.ServiceProvider.GetRequiredService<IRetryService>();

        var evt = CreateEvent("critical");

        // Act - Attempt 4 (Max is 3 for critical)
        var result = await retryService.ScheduleRetryAsync(evt, 4, "Error");

        // Assert
        Assert.False(result.WasScheduled);
        Assert.Contains("Max retries", result.Reason);
    }

    [Fact]
    public async Task ScheduleRetryAsync_ValidAttempt_SchedulesMessage()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var retryService = scope.ServiceProvider.GetRequiredService<IRetryService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        var evt = CreateEvent("critical");

        // Act
        var result = await retryService.ScheduleRetryAsync(evt, 1, "Error");

        // Assert
        Assert.True(result.WasScheduled);
        Assert.NotNull(result.ScheduledTime);

        var entry = await dbContext.RetryQueueEntries.FirstOrDefaultAsync(e => e.EventId == evt.MessageId.ToString());
        Assert.NotNull(entry);
        Assert.Equal(1, entry.AttemptNumber);
    }

    private NotificationEvent CreateEvent(string priority)
    {
        return new NotificationEvent(
            MessageId: Guid.NewGuid(),
            MessageName: "NotificationEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Test",
            ConsumedBy: new[] { "NotificationService" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new NotificationEventPayload(
                NotificationType: "Test",
                Priority: priority,
                TargetUsers: new[] { new NotificationEventPayloadTargetUsersItem("user1", "customer") },
                TemplateId: string.Empty,
                Parameters: new Dictionary<string, string>(),
                Metadata: new NotificationEventPayloadMetadata("en", "test")
            )
        );
    }
}
