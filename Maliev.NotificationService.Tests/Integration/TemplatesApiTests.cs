using System.Net;
using System.Net.Http.Json;
using Maliev.NotificationService.Api.Authorization;
using Maliev.NotificationService.Api.Models.Requests;
using Maliev.NotificationService.Data;
using Maliev.NotificationService.Data.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Integration;

[Collection("Integration")]
public class TemplatesApiTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _adminClient;

    public TemplatesApiTests(TestWebApplicationFactory factory)
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
    public async Task GetTemplates_NoFilter_ReturnsAllTemplates()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        context.NotificationTemplates.Add(new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            TemplateKey = "alpha-template",
            ChannelType = "email",
            Language = "en",
            ContentTemplate = "Hello {{name}}",
            Parameters = new[] { "name" },
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        context.NotificationTemplates.Add(new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            TemplateKey = "beta-template",
            ChannelType = "sms",
            Language = "en",
            ContentTemplate = "Your code: {{code}}",
            Parameters = new[] { "code" },
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var response = await _adminClient.GetAsync("/notification/v1/templates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedTemplateResponse>();
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 2);
    }

    [Fact]
    public async Task GetTemplates_WithFilter_ReturnsFilteredResults()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        var uniqueKey = $"filter-test-{Guid.NewGuid():N}";
        context.NotificationTemplates.Add(new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            TemplateKey = uniqueKey,
            ChannelType = "email",
            Language = "en",
            ContentTemplate = "Filtered content",
            Parameters = Array.Empty<string>(),
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var response = await _adminClient.GetAsync($"/notification/v1/templates?filter={uniqueKey}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedTemplateResponse>();
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetTemplates_EmptyDatabase_ReturnsEmptyList()
    {
        var response = await _adminClient.GetAsync("/notification/v1/templates?filter=nonexistent-template-xyz123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PaginatedTemplateResponse>();
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task DeleteTemplate_ExistingTemplate_ReturnsNoContent()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        var templateId = Guid.NewGuid();
        context.NotificationTemplates.Add(new NotificationTemplate
        {
            Id = templateId,
            TemplateKey = $"delete-test-{Guid.NewGuid():N}",
            ChannelType = "email",
            Language = "en",
            ContentTemplate = "Delete me",
            Parameters = Array.Empty<string>(),
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var response = await _adminClient.DeleteAsync($"/notification/v1/templates/{templateId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTemplate_NonExistentId_ReturnsNotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _adminClient.DeleteAsync($"/notification/v1/templates/{nonExistentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateTemplate_InvalidTemplateKey_ReturnsBadRequest()
    {
        var request = new CreateTemplateRequest
        {
            TemplateKey = "INVALID_KEY_WITH_CAPS",
            ChannelType = Models.Enums.ChannelType.Email,
            Language = "en",
            ContentTemplate = "Hello",
            Parameters = Array.Empty<string>(),
            Version = 1
        };

        var response = await _adminClient.PostAsJsonAsync("/notification/v1/templates", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTemplate_UndeclaredParameterInTemplate_ReturnsBadRequest()
    {
        var request = new CreateTemplateRequest
        {
            TemplateKey = "valid-key",
            ChannelType = Models.Enums.ChannelType.Email,
            Language = "en",
            ContentTemplate = "Hello {{name}} and {{unknown}}",
            Parameters = new[] { "name" }, // "unknown" not declared
            Version = 1
        };

        var response = await _adminClient.PostAsJsonAsync("/notification/v1/templates", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTemplate_UnusedDeclaredParameter_ReturnsBadRequest()
    {
        var request = new CreateTemplateRequest
        {
            TemplateKey = "valid-key",
            ChannelType = Models.Enums.ChannelType.Email,
            Language = "en",
            ContentTemplate = "Hello {{name}}",
            Parameters = new[] { "name", "unused" }, // "unused" not used in template
            Version = 1
        };

        var response = await _adminClient.PostAsJsonAsync("/notification/v1/templates", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTemplate_UndeclaredParameterInTemplate_ReturnsBadRequest()
    {
        // First create a template to update
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var templateId = Guid.NewGuid();
        context.NotificationTemplates.Add(new NotificationTemplate
        {
            Id = templateId,
            TemplateKey = $"update-val-{Guid.NewGuid():N}",
            ChannelType = "email",
            Language = "en",
            ContentTemplate = "Hello {{name}}",
            Parameters = new[] { "name" },
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var updateRequest = new { ContentTemplate = "Hello {{name}} and {{unknown}}", Parameters = new[] { "name" } };
        var response = await _adminClient.PutAsJsonAsync($"/notification/v1/templates/{templateId}", updateRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Minimal DTO for deserializing paginated templates response
    private class PaginatedTemplateResponse
    {
        public List<object> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
