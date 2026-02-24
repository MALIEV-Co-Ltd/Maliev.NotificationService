namespace Maliev.NotificationService.Api.Authorization;

/// <summary>
/// Predefined roles for the Notification Service.
/// </summary>
public static class NotificationPredefinedRoles
{
    /// <summary>Role for systems or users that only need to send notifications.</summary>
    public const string Sender = "roles.notification.sender";

    /// <summary>Role for administrators managing notification infrastructure.</summary>
    public const string Admin = "roles.notification.admin";

    /// <summary>Role for users managing their own notification preferences.</summary>
    public const string User = "roles.notification.user";

    /// <summary>
    /// Collection of role definitions for registration.
    /// </summary>
    public static readonly IReadOnlyList<(string RoleId, string Description, string[] Permissions)> All = new List<(string, string, string[])>
    {
        (Sender, "Allows sending notifications via all channels", new[] { NotificationPermissions.Send }),
        (Admin, "Full administrative access to notifications, templates, and channels",
            NotificationPermissions.All.Keys.ToArray()),
        (User, "Standard user role for self-service preferences and bindings", new[]
        {
            NotificationPermissions.PreferencesRead,
            NotificationPermissions.PreferencesUpdate,
            NotificationPermissions.BindingsRead,
            NotificationPermissions.BindingsUpdate
        })
    };
}
