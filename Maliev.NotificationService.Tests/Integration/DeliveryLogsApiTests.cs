using System.Net;
using System.Net.Http.Json;
using Maliev.NotificationService.Api.Models.Responses;
using Maliev.NotificationService.Data.Entities;
using Maliev.NotificationService.Api.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Maliev.NotificationService.Data;

namespace Maliev.NotificationService.Api.Tests.Integration;

[Collection("Integration")]
public class DeliveryLogsApiTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _adminClient;

    public DeliveryLogsApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _adminClient = factory.CreateAuthenticatedClient(
            permissions: new[] { NotificationPermissions.LogsRead });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _factory.CleanDatabaseAsync();
    }

    [Fact]
    public async Task GetDeliveryLogs_ReturnsPagedResults()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        var log = new DeliveryLog
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            EventId = Guid.NewGuid().ToString(),
            ChannelType = "email",
            Status = "sent",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            RecipientIdentifier = "test@example.com"
        };
        context.DeliveryLogs.Add(log);
        await context.SaveChangesAsync();

        // Act
        var response = await _adminClient.GetAsync("/notification/v1/delivery-logs");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResponse<DeliveryLogResponse>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result!.Items);
    }

    [Fact]
    public async Task GetDeliveryLogById_ReturnsLog()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        var log = new DeliveryLog
        {
            Id = Guid.NewGuid(),
            UserId = "user-2",
            EventId = Guid.NewGuid().ToString(),
            ChannelType = "sms",
            Status = "delivered",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            RecipientIdentifier = "+66812345678"
        };
        context.DeliveryLogs.Add(log);
        await context.SaveChangesAsync();

        // Act
        var response = await _adminClient.GetAsync($"/notification/v1/delivery-logs/{log.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DeliveryLogResponse>();
        Assert.NotNull(result);
        Assert.Equal(log.Id, result!.Id);
    }

    [Fact]
    public async Task GetDeliveryLogs_WithFilters_ReturnsFilteredResults()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        var userId = "filter-user";
        var log1 = new DeliveryLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventId = Guid.NewGuid().ToString(),
            ChannelType = "email",
            Status = "sent",
            RecipientIdentifier = "user@test.com"
        };
        var log2 = new DeliveryLog
        {
            Id = Guid.NewGuid(),
            UserId = "other-user",
            EventId = Guid.NewGuid().ToString(),
            ChannelType = "sms",
            Status = "failed",
            RecipientIdentifier = "other@test.com"
        };
        context.DeliveryLogs.AddRange(log1, log2);
        await context.SaveChangesAsync();

        // Act
        var response = await _adminClient.GetAsync($"/notification/v1/delivery-logs?userId={userId}&status=sent");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResponse<DeliveryLogResponse>>();
        Assert.NotNull(result);
        Assert.Single(result!.Items);
        Assert.Equal(userId, result.Items[0].UserId);
    }
}
