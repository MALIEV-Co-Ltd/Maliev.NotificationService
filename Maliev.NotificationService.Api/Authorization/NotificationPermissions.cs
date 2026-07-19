namespace Maliev.NotificationService.Api.Authorization;

/// <summary>
/// Constants for Notification Service permissions.
/// Follows GCP-style naming: {service}.{resource}.{action}
/// </summary>
public static class NotificationPermissions
{
    // Notification Operations
    /// <summary>Permission to send notifications.</summary>
    public const string Send = "notification.notifications.send";
    /// <summary>Permission to view notification history.</summary>
    public const string Read = "notification.notifications.read";

    // Template Operations
    /// <summary>Permission to manage notification templates.</summary>
    public const string ManageTemplates = "notification.templates.manage";
    /// <summary>Permission to create templates.</summary>
    public const string TemplatesCreate = "notification.templates.create";
    /// <summary>Permission to read templates.</summary>
    public const string TemplatesRead = "notification.templates.read";
    /// <summary>Permission to update templates.</summary>
    public const string TemplatesUpdate = "notification.templates.update";
    /// <summary>Permission to delete templates.</summary>
    public const string TemplatesDelete = "notification.templates.delete";

    // Channel/Binding Operations
    /// <summary>Permission to configure notification channels.</summary>
    public const string ConfigureChannels = "notification.channels.configure";
    /// <summary>Permission to create bindings.</summary>
    public const string BindingsCreate = "notification.bindings.create";
    /// <summary>Permission to read bindings.</summary>
    public const string BindingsRead = "notification.bindings.read";
    /// <summary>Permission to list all bindings for a user.</summary>
    public const string BindingsListUser = "notification.bindings.list-user";
    /// <summary>Permission to update bindings.</summary>
    public const string BindingsUpdate = "notification.bindings.update";
    /// <summary>Permission to delete bindings.</summary>
    public const string BindingsDelete = "notification.bindings.delete";

    // Log Operations
    /// <summary>Permission to read delivery logs.</summary>
    public const string LogsRead = "notification.logs.read";

    // Preference Operations
    /// <summary>Permission to read user preferences.</summary>
    public const string PreferencesRead = "notification.preferences.read";
    /// <summary>Permission to read any user preferences.</summary>
    public const string PreferencesReadAny = "notification.preferences.read-any";
    /// <summary>Permission to update user preferences.</summary>
    public const string PreferencesUpdate = "notification.preferences.update";
    /// <summary>Permission to delete user preferences.</summary>
    public const string PreferencesDelete = "notification.preferences.delete";

    /// <summary>
    /// Collection of all permissions for easy registration.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        { Send, "Send multi-channel notifications" },
        { Read, "Read notification history and logs" },
        { ManageTemplates, "Manage notification content templates" },
        { TemplatesCreate, "Create notification templates" },
        { TemplatesRead, "Read notification templates" },
        { TemplatesUpdate, "Update notification templates" },
        { TemplatesDelete, "Delete notification templates" },
        { ConfigureChannels, "Configure communication channels and providers" },
        { BindingsCreate, "Create channel bindings" },
        { BindingsRead, "Read channel bindings" },
        { BindingsListUser, "List channel bindings for a user" },
        { BindingsUpdate, "Update channel bindings" },
        { BindingsDelete, "Delete channel bindings" },
        { LogsRead, "Read delivery logs" },
        { PreferencesRead, "Read user preferences" },
        { PreferencesReadAny, "Read any user preferences" },
        { PreferencesUpdate, "Update user preferences" },
        { PreferencesDelete, "Delete user preferences" }
    };
}
