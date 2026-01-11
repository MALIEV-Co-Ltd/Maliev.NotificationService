using System.Net;
using System.Net.Http.Json;
using Maliev.NotificationService.Data;
using Maliev.NotificationService.Api.Models.Requests;
using Maliev.NotificationService.Api.Models.Responses;
using Maliev.NotificationService.Api.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Integration;

/// <summary>
/// Integration tests for Preferences and Channel Bindings APIs
/// Tests end-to-end CRUD operations with real Testcontainers infrastructure
/// </summary>
public class PreferencesApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _adminClient;

    public PreferencesApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient(
            permissions: new[]
            {
                NotificationPermissions.PreferencesRead,
                NotificationPermissions.PreferencesUpdate,
                NotificationPermissions.PreferencesDelete,
                NotificationPermissions.PreferencesReadAny,
                NotificationPermissions.BindingsCreate,
                NotificationPermissions.BindingsRead,
                NotificationPermissions.BindingsUpdate,
                NotificationPermissions.BindingsDelete,
                NotificationPermissions.BindingsListUser,
                NotificationPermissions.TemplatesCreate,
                NotificationPermissions.TemplatesRead,
                NotificationPermissions.TemplatesUpdate
            });
        _adminClient = factory.CreateAuthenticatedClient(
            roles: new[] { "Administrator" },
            permissions: NotificationPermissions.All.Keys.ToArray());
    }

    #region Preference Tests (T050)

    [Fact]
    public async Task CreatePreferences_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new CreatePreferenceRequest
        {
            UserId = $"user-{Guid.NewGuid()}",
            PrimaryChannelType = "email",
            FallbackChannelTypes = new List<string> { "sms", "slack" },
            OptOutCategories = new List<string> { "marketing" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/notification/v1/preferences", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PreferenceResponse>();
        Assert.NotNull(result);
        Assert.Equal(request.UserId, result.UserId);
        Assert.Equal("email", result.PrimaryChannelType);
        Assert.Equal(2, result.FallbackChannelTypes.Count);
        Assert.Contains("sms", result.FallbackChannelTypes);
        Assert.Contains("slack", result.FallbackChannelTypes);
        Assert.Single(result.OptOutCategories);
        Assert.Contains("marketing", result.OptOutCategories);
    }

    [Fact]
    public async Task CreatePreferences_DuplicateUserId_ReturnsConflict()
    {
        // Arrange
        var userId = $"user-{Guid.NewGuid()}";
        var request = new CreatePreferenceRequest
        {
            UserId = userId,
            PrimaryChannelType = "email"
        };

        // Act - Create first preference
        var firstResponse = await _client.PostAsJsonAsync("/notification/v1/preferences", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Act - Attempt to create duplicate
        var secondResponse = await _client.PostAsJsonAsync("/notification/v1/preferences", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task CreatePreferences_InvalidChannelType_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreatePreferenceRequest
        {
            UserId = $"user-{Guid.NewGuid()}",
            PrimaryChannelType = "invalid-channel"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/notification/v1/preferences", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPreferences_ExistingUser_ReturnsPreferences()
    {
        // Arrange
        var userId = $"user-{Guid.NewGuid()}";
        var createRequest = new CreatePreferenceRequest
        {
            UserId = userId,
            PrimaryChannelType = "line",
            FallbackChannelTypes = new List<string> { "email" }
        };
        await _client.PostAsJsonAsync("/notification/v1/preferences", createRequest);

        // Act
        var response = await _client.GetAsync($"/notification/v1/preferences/{userId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PreferenceResponse>();
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("line", result.PrimaryChannelType);
    }

    [Fact]
    public async Task GetPreferences_NonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var userId = $"user-{Guid.NewGuid()}";

        // Act
        var response = await _client.GetAsync($"/notification/v1/preferences/{userId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePreferences_ExistingUser_ReturnsUpdatedPreferences()
    {
        // Arrange
        var userId = $"user-{Guid.NewGuid()}";
        var createRequest = new CreatePreferenceRequest
        {
            UserId = userId,
            PrimaryChannelType = "email"
        };
        await _client.PostAsJsonAsync("/notification/v1/preferences", createRequest);

        var updateRequest = new UpdatePreferenceRequest
        {
            PrimaryChannelType = "whatsapp",
            FallbackChannelTypes = new List<string> { "email", "sms" },
            OptOutCategories = new List<string> { "promotions" }
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/notification/v1/preferences/{userId}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PreferenceResponse>();
        Assert.NotNull(result);
        Assert.Equal("whatsapp", result.PrimaryChannelType);
        Assert.Equal(2, result.FallbackChannelTypes.Count);
        Assert.Single(result.OptOutCategories);
        Assert.Contains("promotions", result.OptOutCategories);
    }

    [Fact]
    public async Task UpdatePreferences_NonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var userId = $"user-{Guid.NewGuid()}";
        var updateRequest = new UpdatePreferenceRequest
        {
            PrimaryChannelType = "sms"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/notification/v1/preferences/{userId}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletePreferences_ExistingUser_ReturnsNoContent()
    {
        // Arrange
        var userId = $"user-{Guid.NewGuid()}";
        var createRequest = new CreatePreferenceRequest
        {
            UserId = userId,
            PrimaryChannelType = "email"
        };
        await _client.PostAsJsonAsync("/notification/v1/preferences", createRequest);

        // Act
        var response = await _client.DeleteAsync($"/notification/v1/preferences/{userId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify deletion
        var getResponse = await _client.GetAsync($"/notification/v1/preferences/{userId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeletePreferences_NonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var userId = $"user-{Guid.NewGuid()}";

        // Act
        var response = await _client.DeleteAsync($"/notification/v1/preferences/{userId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Channel Binding Tests (T051)

    [Fact]
    public async Task CreateChannelBinding_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new CreateChannelBindingRequest
        {
            UserId = $"user-{Guid.NewGuid()}",
            ChannelType = "email",
            ChannelIdentifier = "user@example.com"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/notification/v1/channel-bindings", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ChannelBindingResponse>();
        Assert.NotNull(result);
        Assert.Equal(request.UserId, result.UserId);
        Assert.Equal("email", result.ChannelType);
        Assert.StartsWith("u***", result.ChannelIdentifier); // Obfuscated
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task CreateChannelBinding_DuplicateUserAndChannel_ReturnsConflict()
    {
        // Arrange
        var userId = $"user-{Guid.NewGuid()}";
        var request = new CreateChannelBindingRequest
        {
            UserId = userId,
            ChannelType = "line",
            ChannelIdentifier = "U1234567890abcdef"
        };

        // Act - Create first binding
        var firstResponse = await _client.PostAsJsonAsync("/notification/v1/channel-bindings", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Act - Attempt to create duplicate
        var secondResponse = await _client.PostAsJsonAsync("/notification/v1/channel-bindings", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateChannelBinding_ValidRequest_ReturnsUpdatedBinding()
    {
        // Arrange
        var createRequest = new CreateChannelBindingRequest
        {
            UserId = $"user-{Guid.NewGuid()}",
            ChannelType = "sms",
            ChannelIdentifier = "+66812345678"
        };
        var createResponse = await _client.PostAsJsonAsync("/notification/v1/channel-bindings", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ChannelBindingResponse>();
        Assert.NotNull(created);

        var updateRequest = new UpdateChannelBindingRequest
        {
            ChannelIdentifier = "+66887654321"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/notification/v1/channel-bindings/{created.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ChannelBindingResponse>();
        Assert.NotNull(result);
        Assert.StartsWith("+668", result.ChannelIdentifier); // Obfuscated
    }

    [Fact]
    public async Task UpdateChannelBinding_InvalidateBinding_SetsInvalidatedFields()
    {
        // Arrange
        var createRequest = new CreateChannelBindingRequest
        {
            UserId = $"user-{Guid.NewGuid()}",
            ChannelType = "whatsapp",
            ChannelIdentifier = "+66891234567"
        };
        var createResponse = await _client.PostAsJsonAsync("/notification/v1/channel-bindings", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ChannelBindingResponse>();
        Assert.NotNull(created);

        var updateRequest = new UpdateChannelBindingRequest
        {
            IsValid = false,
            InvalidatedReason = "User blocked notifications"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/notification/v1/channel-bindings/{created.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ChannelBindingResponse>();
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.NotNull(result.InvalidatedAt);
        Assert.Equal("User blocked notifications", result.InvalidatedReason);
    }

    [Fact]
    public async Task DeleteChannelBinding_ExistingBinding_ReturnsNoContent()
    {
        // Arrange
        var createRequest = new CreateChannelBindingRequest
        {
            UserId = $"user-{Guid.NewGuid()}",
            ChannelType = "slack",
            ChannelIdentifier = "U12345ABC"
        };
        var createResponse = await _client.PostAsJsonAsync("/notification/v1/channel-bindings", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ChannelBindingResponse>();
        Assert.NotNull(created);

        // Act
        var response = await _client.DeleteAsync($"/notification/v1/channel-bindings/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetUserChannelBindings_ReturnsAllBindings()
    {
        // Arrange
        var userId = $"user-{Guid.NewGuid()}";

        // Create multiple bindings for same user
        var emailBinding = new CreateChannelBindingRequest
        {
            UserId = userId,
            ChannelType = "email",
            ChannelIdentifier = "user@example.com"
        };
        var lineBinding = new CreateChannelBindingRequest
        {
            UserId = userId,
            ChannelType = "line",
            ChannelIdentifier = "U1234567890"
        };

        await _client.PostAsJsonAsync("/notification/v1/channel-bindings", emailBinding);
        await _client.PostAsJsonAsync("/notification/v1/channel-bindings", lineBinding);

        // Act
        var response = await _client.GetAsync($"/notification/v1/channel-bindings/user/{userId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<List<ChannelBindingResponse>>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, b => b.ChannelType == "email");
        Assert.Contains(result, b => b.ChannelType == "line");
    }

    [Fact]
    public async Task GetUserChannelBindings_FilterByIsValid_ReturnsFilteredResults()
    {
        // Arrange
        var userId = $"user-{Guid.NewGuid()}";

        // Create valid binding
        var validBinding = new CreateChannelBindingRequest
        {
            UserId = userId,
            ChannelType = "email",
            ChannelIdentifier = "valid@example.com"
        };
        await _client.PostAsJsonAsync("/notification/v1/channel-bindings", validBinding);

        // Create invalid binding
        var invalidBindingCreate = new CreateChannelBindingRequest
        {
            UserId = userId,
            ChannelType = "sms",
            ChannelIdentifier = "+66123456789"
        };
        var invalidResponse = await _client.PostAsJsonAsync("/notification/v1/channel-bindings", invalidBindingCreate);
        var invalidCreated = await invalidResponse.Content.ReadFromJsonAsync<ChannelBindingResponse>();
        Assert.NotNull(invalidCreated);

        // Invalidate the second binding
        await _client.PutAsJsonAsync($"/notification/v1/channel-bindings/{invalidCreated.Id}",
            new UpdateChannelBindingRequest { IsValid = false });

        // Act - Filter for valid only
        var response = await _client.GetAsync($"/notification/v1/channel-bindings/user/{userId}?isValid=true");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<List<ChannelBindingResponse>>();
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.All(result, b => Assert.True(b.IsValid));
    }

    #endregion

    #region Template API Tests (T072)

    [Fact]
    public async Task CreateTemplate_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new CreateTemplateRequest
        {
            TemplateKey = $"order-confirmed-{Guid.NewGuid():N}",
            Version = 1,
            Language = "en",
            ChannelType = Models.Enums.ChannelType.Email,
            ContentTemplate = "Hello {{name}}, your order #{{orderId}} has been confirmed!",
            Parameters = new[] { "name", "orderId" }
        };

        // Act
        var response = await _adminClient.PostAsJsonAsync("/notification/v1/templates", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<TemplateResponse>();
        Assert.NotNull(result);
        Assert.Equal(request.TemplateKey, result.TemplateKey);
        Assert.Equal(request.Version, result.Version);
        Assert.Equal(request.Language, result.Language);
        Assert.Equal(request.ChannelType, result.ChannelType);
        Assert.Equal(request.ContentTemplate, result.ContentTemplate);
        Assert.Equal(2, result.Parameters.Length);
    }

    [Fact]
    public async Task CreateTemplate_DuplicateTemplate_ReturnsConflict()
    {
        // Arrange
        var request = new CreateTemplateRequest
        {
            TemplateKey = $"duplicate-test-{Guid.NewGuid():N}",
            Version = 1,
            Language = "en",
            ChannelType = Models.Enums.ChannelType.Email,
            ContentTemplate = "Test template",
            Parameters = Array.Empty<string>()
        };

        // Act - Create first template
        var firstResponse = await _adminClient.PostAsJsonAsync("/notification/v1/templates", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Act - Attempt to create duplicate
        var secondResponse = await _adminClient.PostAsJsonAsync("/notification/v1/templates", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task GetTemplate_ExistingTemplate_ReturnsTemplate()
    {
        // Arrange - Create template
        var createRequest = new CreateTemplateRequest
        {
            TemplateKey = "get-test",
            Version = 1,
            Language = "th",
            ChannelType = Models.Enums.ChannelType.Sms,
            ContentTemplate = "สวัสดี {{name}}",
            Parameters = new[] { "name" }
        };
        var createResponse = await _adminClient.PostAsJsonAsync("/notification/v1/templates", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TemplateResponse>();
        Assert.NotNull(created);

        // Act
        var response = await _adminClient.GetAsync($"/notification/v1/templates/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<TemplateResponse>();
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("get-test", result.TemplateKey);
    }

    [Fact]
    public async Task GetTemplate_NonExistentTemplate_ReturnsNotFound()
    {
        // Act
        var response = await _adminClient.GetAsync($"/notification/v1/templates/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTemplate_ExistingTemplate_ReturnsOk()
    {
        // Arrange - Create template
        var createRequest = new CreateTemplateRequest
        {
            TemplateKey = "update-test",
            Version = 1,
            Language = "en",
            ChannelType = Models.Enums.ChannelType.Email,
            ContentTemplate = "Old content {{param1}}",
            Parameters = new[] { "param1" }
        };
        var createResponse = await _adminClient.PostAsJsonAsync("/notification/v1/templates", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TemplateResponse>();
        Assert.NotNull(created);

        var updateRequest = new UpdateTemplateRequest
        {
            ContentTemplate = "New content {{param1}} {{param2}}",
            Parameters = new[] { "param1", "param2" }
        };

        // Act
        var response = await _adminClient.PutAsJsonAsync($"/notification/v1/templates/{created.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<TemplateResponse>();
        Assert.NotNull(result);
        Assert.Equal("New content {{param1}} {{param2}}", result.ContentTemplate);
        Assert.Equal(2, result.Parameters.Length);
    }

    [Fact]
    public async Task UpdateTemplate_NonExistentTemplate_ReturnsNotFound()
    {
        // Arrange
        var updateRequest = new UpdateTemplateRequest
        {
            ContentTemplate = "New content",
            Parameters = Array.Empty<string>()
        };

        // Act
        var response = await _adminClient.PutAsJsonAsync($"/notification/v1/templates/{Guid.NewGuid()}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    private async Task CleanupTestDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        // Clean up test data
        dbContext.UserNotificationPreferences.RemoveRange(dbContext.UserNotificationPreferences);
        dbContext.ChannelBindings.RemoveRange(dbContext.ChannelBindings);

        await dbContext.SaveChangesAsync();
    }
}
