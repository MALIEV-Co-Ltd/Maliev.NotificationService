using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Tests.Testing;
using Maliev.NotificationService.Api.Tests.Integration;

namespace Maliev.NotificationService.Tests.Integration;

[Collection("Integration")]
public class DeduplicationServiceTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public DeduplicationServiceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Redis clear if needed, but BaseIntegrationTestFactory might handle it
    }

    [Fact]
    public async Task IsDuplicateAsync_NewEvent_ReturnsFalse()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDeduplicationService>();
        var eventId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var result = await service.IsDuplicateAsync(eventId, timestamp);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsDuplicateAsync_ExistingEvent_ReturnsTrue()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDeduplicationService>();
        var eventId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        await service.IsDuplicateAsync(eventId, timestamp); // First time
        var result = await service.IsDuplicateAsync(eventId, timestamp); // Second time

        // Assert
        Assert.True(result);
    }
}
