using System.Security.Claims;
using Maliev.NotificationService.Api.Authorization;
using Maliev.NotificationService.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Unit.Middleware;

public class LegacyTokenFallbackMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AuthenticatedWithoutPermissions_AddsDefaultPermissions()
    {
        // Arrange
        var nextMock = new Mock<RequestDelegate>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:PermissionBasedAuthEnabled"] = "true"
            })
            .Build();

        var middleware = new LegacyTokenFallbackMiddleware(nextMock.Object, configuration);

        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "user-1"));
        var user = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = user };

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.User.HasClaim(c => c.Type == "permissions"));
        Assert.Contains(context.User.Claims, c => c.Type == "permissions" && c.Value == NotificationPermissions.PreferencesRead);
        Assert.Contains(context.User.Claims, c => c.Type == ClaimTypes.Role && c.Value == NotificationPredefinedRoles.User);
        nextMock.Verify(next => next(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_NotAuthenticated_DoesNothing()
    {
        // Arrange
        var nextMock = new Mock<RequestDelegate>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:PermissionBasedAuthEnabled"] = "true"
            })
            .Build();

        var middleware = new LegacyTokenFallbackMiddleware(nextMock.Object, configuration);

        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(context.User.HasClaim(c => c.Type == "permissions"));
        nextMock.Verify(next => next(context), Times.Once);
    }
}
