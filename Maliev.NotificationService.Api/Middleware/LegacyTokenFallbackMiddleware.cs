using Maliev.NotificationService.Api.Authorization;
using System.Security.Claims;

namespace Maliev.NotificationService.Api.Middleware;

public class LegacyTokenFallbackMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public LegacyTokenFallbackMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var iamEnabled = _configuration.GetValue<bool>("Features:PermissionBasedAuthEnabled");

        if (iamEnabled && context.User.Identity?.IsAuthenticated == true)
        {
            var hasPermissions = context.User.HasClaim(c => c.Type == "permissions");

            if (!hasPermissions)
            {
                // Fallback to notification-user role for tokens without permission claims
                var identity = (ClaimsIdentity)context.User.Identity;

                // Add permissions from the default user role
                var userRole = NotificationPredefinedRoles.All.FirstOrDefault(r => r.RoleId == NotificationPredefinedRoles.User);

                if (userRole.Permissions != null)
                {
                    foreach (var permissionId in userRole.Permissions)
                    {
                        identity.AddClaim(new Claim("permissions", permissionId));
                    }
                }

                // Also add the role claim
                identity.AddClaim(new Claim(ClaimTypes.Role, NotificationPredefinedRoles.User));
            }
        }

        await _next(context);
    }
}
