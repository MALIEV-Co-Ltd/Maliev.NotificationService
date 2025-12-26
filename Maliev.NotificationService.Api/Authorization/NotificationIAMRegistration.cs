using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.NotificationService.Api.Authorization;

namespace Maliev.NotificationService.Api.Authorization;

public class NotificationIAMRegistration : IAMRegistrationService
{
    private readonly IConfiguration _configuration;

    public NotificationIAMRegistration(
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationIAMRegistration> logger,
        IConfiguration configuration)
        : base(httpClientFactory, logger, "notification")
    {
        _configuration = configuration;
    }

    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        return NotificationPermissions.All.Select(p => new PermissionRegistration
        {
            PermissionId = p,
            Description = $"Permission: {p}"
        });
    }

    protected override IEnumerable<RoleRegistration> GetPredefinedRoles()
    {
        return NotificationPredefinedRoles.All;
    }

    public async Task RegisterWithCheckAsync(CancellationToken cancellationToken)
    {
        var iamEnabled = _configuration.GetValue<bool>("Features:PermissionBasedAuthEnabled");
        if (!iamEnabled)
        {
            return;
        }

        await base.RegisterAsync(cancellationToken);
    }
}
