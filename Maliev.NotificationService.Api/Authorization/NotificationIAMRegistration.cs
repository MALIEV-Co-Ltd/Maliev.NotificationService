using Maliev.Aspire.ServiceDefaults.IAM;

namespace Maliev.NotificationService.Api.Authorization;

/// <summary>
/// Registers Notification Service permissions and roles with IAM via RabbitMQ.
/// </summary>
public class NotificationIAMRegistration : IAMRegistrationService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationIAMRegistration"/> class.
    /// </summary>
    public NotificationIAMRegistration(
        IConfiguration configuration,
        ILogger<NotificationIAMRegistration> logger)
        : base(configuration, logger, "notification")
    {
    }

    /// <inheritdoc/>
    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        return NotificationPermissions.All.Select(p => new PermissionRegistration
        {
            PermissionId = p.Key,
            Description = p.Value
        });
    }

    /// <inheritdoc/>
    protected override IEnumerable<RoleRegistration> GetPredefinedRoles()
    {
        return NotificationPredefinedRoles.All.Select(r => new RoleRegistration
        {
            RoleId = r.RoleId,
            Description = r.Description,
            PermissionIds = r.Permissions.ToList(),
            IsCustom = false // Predefined roles are not custom roles
        });
    }
}
