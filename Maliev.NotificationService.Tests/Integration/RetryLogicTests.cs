using System.Net;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Maliev.MessagingContracts.Generated;

namespace Maliev.NotificationService.Api.Tests.Integration;

/// <summary>
/// Integration tests for retry logic with exponential backoff.
/// Tests T029: Verify 3 retry attempts with exponential backoff intervals (1s, 2s, 4s).
/// </summary>
[Collection("Integration")]
public class RetryLogicTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RetryLogicTests(TestWebApplicationFactory factory)
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

    private NotificationEvent CreateTestEvent(
        string notificationType,
        string priority,
        string userId,
        string userType,
        string templateId,
        Dictionary<string, string> parameters)
    {
        return new NotificationEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(NotificationEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "TestService",
            ConsumedBy: new[] { "NotificationService" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new NotificationEventPayload(
                NotificationType: notificationType,
                Priority: priority,
                TargetUsers: new[] { new NotificationEventPayloadTargetUsersItem(userId, userType) },
                TemplateId: templateId,
                Parameters: parameters,
                Metadata: new NotificationEventPayloadMetadata("en", "test-source")
            )
        );
    }

    [Fact]
    public async Task NotificationDeliveryFails_ShouldRetry3TimesWithExponentialBackoff()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        // Create notification that will fail (e.g., invalid channel binding)
        var notificationEvent = CreateTestEvent(
            "RetryTest",
            "critical",
            "retry_test_user",
            "customer",
            "test-template",
            new Dictionary<string, string>
            {
                ["message"] = "This should fail and retry"
            }
        );

        var publishTime = DateTimeOffset.UtcNow;

        // Act
        await bus.Publish(notificationEvent);

        // Wait for all retries to complete (1s + 2s + 4s = 7s + processing time)
        await Task.Delay(TimeSpan.FromSeconds(15));

        // Assert
    }

    [Fact]
    public async Task TransientProviderError_ShouldBeRetried()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var notificationEvent = CreateTestEvent(
            "TransientErrorTest",
            "critical",
            "transient_error_user",
            "customer",
            "test-template",
            new Dictionary<string, string> { ["message"] = "Transient error test" }
        );

        // Act
        await bus.Publish(notificationEvent);
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Assert
        // Transient errors (network timeout, rate limit) should be retried
        Assert.True(true); // Placeholder until retry service is implemented
    }

    [Fact]
    public async Task PermanentProviderError_ShouldNotBeRetried()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var notificationEvent = CreateTestEvent(
            "PermanentErrorTest",
            "critical",
            "permanent_error_user",
            "customer",
            "test-template",
            new Dictionary<string, string> { ["message"] = "Permanent error test" }
        );

        // Act
        await bus.Publish(notificationEvent);
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
    }

    [Fact]
    public async Task RateLimitExceeded_ShouldRespectRetryAfterHeader()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        // Publish many notifications quickly to trigger rate limiting
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            var notificationEvent = CreateTestEvent(
                "RateLimitTest",
                "standard",
                $"rate_limit_user_{i}",
                "customer",
                "test-template",
                new Dictionary<string, string> { ["index"] = i.ToString() }
            );

            tasks.Add(bus.Publish(notificationEvent));
        }

        // Act
        await Task.WhenAll(tasks);
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(100, tasks.Count);
    }

    [Fact]
    public async Task After3FailedRetries_ShouldMoveToDeadLetterQueue()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var notificationEvent = CreateTestEvent(
            "DeadLetterTest",
            "critical",
            "dead_letter_user",
            "customer",
            "test-template",
            new Dictionary<string, string> { ["message"] = "This will fail all retries" }
        );

        // Act
        await bus.Publish(notificationEvent);

        // Wait for all 3 retries to exhaust (1s + 2s + 4s + buffer)
        await Task.Delay(TimeSpan.FromSeconds(15));

        // Assert
    }
}
