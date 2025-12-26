using Maliev.Aspire.ServiceDefaults.IAM;

namespace Maliev.NotificationService.Api.Authorization;

public static class NotificationPredefinedRoles
{
    public static readonly RoleRegistration Admin = new()
    {
        RoleId = "roles.notification.admin",
        Description = "Full control over notification system",
        PermissionIds = NotificationPermissions.All.ToList(),
        IsCustom = false
    };

    public static readonly RoleRegistration Manager = new()
    {
        RoleId = "roles.notification.manager",
        Description = "Manage templates and view all logs",
        PermissionIds = new List<string>
        {
            NotificationPermissions.TemplatesCreate,
            NotificationPermissions.TemplatesRead,
            NotificationPermissions.TemplatesUpdate,
            NotificationPermissions.TemplatesDelete,
            NotificationPermissions.TemplatesPublish,
            NotificationPermissions.TemplatesTest,
            NotificationPermissions.NotificationsSend,
            NotificationPermissions.NotificationsRead,
            NotificationPermissions.NotificationsRetry,
            NotificationPermissions.LogsRead,
            NotificationPermissions.LogsExport,
            NotificationPermissions.SystemViewStats
        },
        IsCustom = false
    };

    public static readonly RoleRegistration Sender = new()
    {
        RoleId = "roles.notification.sender",
        Description = "Send notifications and view delivery status",
        PermissionIds = new List<string>
        {
            NotificationPermissions.NotificationsSend,
            NotificationPermissions.NotificationsRead,
            NotificationPermissions.NotificationsRetry,
            NotificationPermissions.TemplatesRead,
            NotificationPermissions.LogsRead
        },
        IsCustom = false
    };

    public static readonly RoleRegistration User = new()
    {
        RoleId = "roles.notification.user",
        Description = "Manage own preferences and channel bindings",
        PermissionIds = new List<string>
        {
            NotificationPermissions.BindingsCreate,
            NotificationPermissions.BindingsRead,
            NotificationPermissions.BindingsUpdate,
            NotificationPermissions.BindingsDelete,
            NotificationPermissions.BindingsVerify,
            NotificationPermissions.BindingsListUser,
            NotificationPermissions.PreferencesRead,
            NotificationPermissions.PreferencesUpdate,
            NotificationPermissions.LogsReadUser
        },
        IsCustom = false
    };

    public static readonly RoleRegistration[] All = new[]
    {
        Admin, Manager, Sender, User
    };
}
