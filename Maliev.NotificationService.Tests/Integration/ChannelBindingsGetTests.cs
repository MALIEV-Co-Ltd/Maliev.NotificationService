using System.Net;
using System.Net.Http.Json;
using Maliev.NotificationService.Api.Authorization;
using Maliev.NotificationService.Api.Models.Requests;
using Maliev.NotificationService.Api.Models.Responses;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Data;
using Maliev.NotificationService.Data.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Integration;

[Collection("Integration")]
public class ChannelBindingsGetTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _adminClient;

    public ChannelBindingsGetTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _adminClient = factory.CreateAuthenticatedClient(
            permissions: NotificationPermissions.All.Keys.ToArray());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _factory.CleanDatabaseAsync();
    }

    [Fact]
    public async Task GetChannelBinding_ExistingId_ReturnsOk()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

        var bindingId = Guid.NewGuid();
        context.ChannelBindings.Add(new ChannelBinding
        {
            Id = bindingId,
            UserId = "test-user",
            ChannelType = "email",
            ChannelIdentifier = encryptionService.Encrypt("test@example.com"),
            IsValid = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var response = await _adminClient.GetAsync($"/notification/v1/channel-bindings/{bindingId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ChannelBindingResponse>();
        Assert.NotNull(result);
        Assert.Equal(bindingId, result.Id);
        Assert.Equal("email", result.ChannelType);
    }

    [Fact]
    public async Task GetChannelBinding_NonExistentId_ReturnsNotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _adminClient.GetAsync($"/notification/v1/channel-bindings/{nonExistentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteChannelBinding_NotFound_ReturnsNotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _adminClient.DeleteAsync($"/notification/v1/channel-bindings/{nonExistentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
