using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Maliev.NotificationService.Api.Authorization;
using Maliev.NotificationService.Api.Tests.Integration;
using Maliev.NotificationService.Api.Tests.TestHelpers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.NotificationService.Tests.Integration;

public class AuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthorizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Startup_ShouldRegisterPermissionsAndRoles_WhenEnabled()
    {
        // Arrange
        using var clientFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:PermissionBasedAuthEnabled", "true");
            builder.UseSetting("IAM:BaseUrl", "http://iam-service:8080");
        });

        // Act
        // Accessing any endpoint to trigger host startup
        var client = clientFactory.CreateClient();
        await client.GetAsync("/notification/liveness");

        // Wait for background registration via MassTransit harness
        var harness = clientFactory.Services.GetRequiredService<MassTransit.Testing.ITestHarness>();

        // Assert
        Assert.True(await harness.Published.Any<Maliev.MessagingContracts.Generated.PermissionRegistrationRequest>(
            x => x.Context.Message.ServiceName == "notification"), "Should publish permission registration request");

        var registrationRequest = harness.Published.Select<Maliev.MessagingContracts.Generated.PermissionRegistrationRequest>()
            .First(x => x.Context.Message.ServiceName == "notification").Context.Message;

        Assert.Equal(NotificationPermissions.All.Count, registrationRequest.Permissions.Count);
        Assert.Equal(NotificationPredefinedRoles.All.Count, registrationRequest.Roles.Count);
    }

    [Fact]
    public async Task GetUserChannelBindings_OwnUser_ShouldSucceed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(userId.ToString());

        // Act
        var response = await client.GetAsync($"/notification/v1/channel-bindings/user/{userId}");

        // Assert
        // We expect OK (200) even without explicit permission because it's own user
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUserChannelBindings_OtherUser_WithoutPermission_ShouldReturnForbidden()
    {
        // Arrange
        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(aliceId.ToString());

        // Act
        var response = await client.GetAsync($"/notification/v1/channel-bindings/user/{bobId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUserChannelBindings_OtherUser_WithPermission_ShouldSucceed()
    {
        // Arrange
        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(aliceId.ToString(),
            additionalClaims: new Dictionary<string, string> { { "permissions", NotificationPermissions.BindingsListUser } });

        // Act
        var response = await client.GetAsync($"/notification/v1/channel-bindings/user/{bobId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateTemplate_WithoutPermission_ShouldReturnForbidden()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(); // No permissions

        // Act
        var response = await client.PostAsJsonAsync("/notification/v1/templates", new
        {
            TemplateKey = "test-template-1",
            Version = 1,
            Language = "en",
            ChannelType = 0, // Email
            ContentTemplate = "Hello {{name}}",
            Parameters = new[] { "name" }
        });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateTemplate_WithPermission_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient(additionalClaims: new Dictionary<string, string>
        {
            { "permissions", NotificationPermissions.TemplatesCreate }
        });

        // Act
        var response = await client.PostAsJsonAsync("/notification/v1/templates", new
        {
            TemplateKey = "test-template-2",
            Version = 1,
            Language = "en",
            ChannelType = 0, // Email
            ContentTemplate = "Hello {{name}}",
            Parameters = new[] { "name" }
        });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Access_WithRevokedPermission_ShouldReturnForbidden()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(userId.ToString()); // No permissions claim

        // Act
        var response = await client.PostAsJsonAsync("/notification/v1/templates", new
        {
            TemplateKey = "test-template-3",
            Version = 1,
            Language = "en",
            ChannelType = 0, // Email
            ContentTemplate = "Hello {{name}}",
            Parameters = new[] { "name" }
        });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
