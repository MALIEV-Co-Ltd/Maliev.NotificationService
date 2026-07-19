namespace Maliev.NotificationService.Application.Authorization;

/// <summary>
/// Provides access to predefined roles for the Notification Service.
/// </summary>
public static class NotificationPredefinedRoles
{
    public const string Admin = "roles.notification.admin";
    public const string Operator = "roles.notification.operator";
    public const string Viewer = "roles.notification.viewer";

    public static readonly IReadOnlyList<(string RoleId, string Description, string[] Permissions)> All = new List<(string, string, string[])>
    {
        (
            Admin,
            "Notification Administrator with full access",
            new[]
            {
                NotificationPermissions.NotificationSend,
                NotificationPermissions.NotificationRead,
                NotificationPermissions.TemplateCreate,
                NotificationPermissions.TemplateRead,
                NotificationPermissions.TemplateUpdate,
                NotificationPermissions.TemplateDelete,
                NotificationPermissions.TemplateManage,
                NotificationPermissions.ChannelConfigure,
                NotificationPermissions.BindingCreate,
                NotificationPermissions.BindingRead,
                NotificationPermissions.BindingUpdate,
                NotificationPermissions.BindingDelete,
                NotificationPermissions.BindingListUser,
                NotificationPermissions.LogRead,
                NotificationPermissions.PreferenceRead,
                NotificationPermissions.PreferenceUpdate,
                NotificationPermissions.PreferenceDelete,
                NotificationPermissions.PreferenceReadAny,
            }
        ),
        (
            Operator,
            "Notification Operator with template and binding access",
            new[]
            {
                NotificationPermissions.NotificationSend,
                NotificationPermissions.NotificationRead,
                NotificationPermissions.TemplateCreate,
                NotificationPermissions.TemplateRead,
                NotificationPermissions.TemplateUpdate,
                NotificationPermissions.TemplateManage,
                NotificationPermissions.ChannelConfigure,
                NotificationPermissions.BindingCreate,
                NotificationPermissions.BindingRead,
                NotificationPermissions.BindingUpdate,
                NotificationPermissions.BindingDelete,
                NotificationPermissions.BindingListUser,
                NotificationPermissions.LogRead,
                NotificationPermissions.PreferenceRead,
                NotificationPermissions.PreferenceUpdate,
            }
        ),
        (
            Viewer,
            "Notification Viewer with read-only access",
            new[]
            {
                NotificationPermissions.NotificationRead,
                NotificationPermissions.TemplateRead,
                NotificationPermissions.BindingRead,
                NotificationPermissions.BindingListUser,
                NotificationPermissions.LogRead,
                NotificationPermissions.PreferenceRead,
            }
        ),
    };
}
