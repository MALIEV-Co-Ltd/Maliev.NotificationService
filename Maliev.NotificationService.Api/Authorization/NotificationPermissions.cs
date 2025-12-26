namespace Maliev.NotificationService.Api.Authorization;

public static class NotificationPermissions
{
    // Template Operations
    public const string TemplatesCreate = "notification.templates.create";
    public const string TemplatesRead = "notification.templates.read";
    public const string TemplatesUpdate = "notification.templates.update";
    public const string TemplatesDelete = "notification.templates.delete";
    public const string TemplatesPublish = "notification.templates.publish";
    public const string TemplatesTest = "notification.templates.test";

    // Notification Operations
    public const string NotificationsSend = "notification.notifications.send";
    public const string NotificationsRead = "notification.notifications.read";
    public const string NotificationsRetry = "notification.notifications.retry";
    public const string NotificationsCancel = "notification.notifications.cancel";
    public const string NotificationsBulk = "notification.notifications.bulk";

    // Channel Binding Operations
    public const string BindingsCreate = "notification.bindings.create";
    public const string BindingsRead = "notification.bindings.read";
    public const string BindingsUpdate = "notification.bindings.update";
    public const string BindingsDelete = "notification.bindings.delete";
    public const string BindingsVerify = "notification.bindings.verify";
    public const string BindingsListUser = "notification.bindings.list-user";

    // Preference Operations
    public const string PreferencesRead = "notification.preferences.read";
    public const string PreferencesUpdate = "notification.preferences.update";
    public const string PreferencesDelete = "notification.preferences.delete";
    public const string PreferencesReadAny = "notification.preferences.read-any";

    // Delivery Log Operations
    public const string LogsRead = "notification.logs.read";
    public const string LogsReadUser = "notification.logs.read-user";
    public const string LogsExport = "notification.logs.export";
    public const string LogsPurge = "notification.logs.purge";

    // System Operations
    public const string SystemConfigure = "notification.system.configure";
    public const string SystemViewStats = "notification.system.view-stats";
    public const string SystemManageChannels = "notification.system.manage-channels";

    public static readonly string[] All = new[]
    {
        TemplatesCreate, TemplatesRead, TemplatesUpdate, TemplatesDelete, TemplatesPublish, TemplatesTest,
        NotificationsSend, NotificationsRead, NotificationsRetry, NotificationsCancel, NotificationsBulk,
        BindingsCreate, BindingsRead, BindingsUpdate, BindingsDelete, BindingsVerify, BindingsListUser,
        PreferencesRead, PreferencesUpdate, PreferencesDelete, PreferencesReadAny,
        LogsRead, LogsReadUser, LogsExport, LogsPurge,
        SystemConfigure, SystemViewStats, SystemManageChannels
    };
}
