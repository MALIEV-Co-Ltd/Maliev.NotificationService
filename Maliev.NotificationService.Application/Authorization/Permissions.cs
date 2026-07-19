namespace Maliev.NotificationService.Application.Authorization;

/// <summary>
/// Defines the permissions for the Notification Service.
/// </summary>
public static class NotificationPermissions
{
    public const string NotificationSend = "notification.notifications.send";
    public const string NotificationRead = "notification.notifications.read";

    public const string TemplateCreate = "notification.templates.create";
    public const string TemplateRead = "notification.templates.read";
    public const string TemplateUpdate = "notification.templates.update";
    public const string TemplateDelete = "notification.templates.delete";
    public const string TemplateManage = "notification.templates.manage";

    public const string ChannelConfigure = "notification.channels.configure";

    public const string BindingCreate = "notification.bindings.create";
    public const string BindingRead = "notification.bindings.read";
    public const string BindingUpdate = "notification.bindings.update";
    public const string BindingDelete = "notification.bindings.delete";
    public const string BindingListUser = "notification.bindings.list-user";

    public const string LogRead = "notification.logs.read";

    public const string PreferenceRead = "notification.preferences.read";
    public const string PreferenceUpdate = "notification.preferences.update";
    public const string PreferenceDelete = "notification.preferences.delete";
    public const string PreferenceReadAny = "notification.preferences.read-any";

    public static readonly IReadOnlyDictionary<string, string> AllWithDescriptions = new Dictionary<string, string>
    {
        { NotificationSend, "Send notifications" },
        { NotificationRead, "Read notifications" },
        { TemplateCreate, "Create notification templates" },
        { TemplateRead, "Read notification templates" },
        { TemplateUpdate, "Update notification templates" },
        { TemplateDelete, "Delete notification templates" },
        { TemplateManage, "Manage notification templates" },
        { ChannelConfigure, "Configure notification channels" },
        { BindingCreate, "Create notification bindings" },
        { BindingRead, "Read notification bindings" },
        { BindingUpdate, "Update notification bindings" },
        { BindingDelete, "Delete notification bindings" },
        { BindingListUser, "List user notification bindings" },
        { LogRead, "Read notification logs" },
        { PreferenceRead, "Read own notification preferences" },
        { PreferenceUpdate, "Update own notification preferences" },
        { PreferenceDelete, "Delete own notification preferences" },
        { PreferenceReadAny, "Read any notification preferences" },
    };

    public static string[] All => AllWithDescriptions.Keys.ToArray();
}
