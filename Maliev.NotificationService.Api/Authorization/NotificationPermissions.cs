namespace Maliev.NotificationService.Api.Authorization;

/// <summary>
/// Defines permission constants for the Notification Service.
/// Note: Constants include "Permission:" prefix for integration with ServiceDefaults policy provider.
/// </summary>
public static class NotificationPermissions
{
    // Template Operations
    public const string TemplatesCreate = "Permission:notification.templates.create";
    public const string TemplatesRead = "Permission:notification.templates.read";
    public const string TemplatesUpdate = "Permission:notification.templates.update";
    public const string TemplatesDelete = "Permission:notification.templates.delete";
    public const string TemplatesPublish = "Permission:notification.templates.publish";
    public const string TemplatesTest = "Permission:notification.templates.test";

    // Notification Operations
    public const string NotificationsSend = "Permission:notification.notifications.send";
    public const string NotificationsRead = "Permission:notification.notifications.read";
    public const string NotificationsRetry = "Permission:notification.notifications.retry";
    public const string NotificationsCancel = "Permission:notification.notifications.cancel";
    public const string NotificationsBulk = "Permission:notification.notifications.bulk";

    // Channel Binding Operations
    public const string BindingsCreate = "Permission:notification.bindings.create";
    public const string BindingsRead = "Permission:notification.bindings.read";
    public const string BindingsUpdate = "Permission:notification.bindings.update";
    public const string BindingsDelete = "Permission:notification.bindings.delete";
    public const string BindingsVerify = "Permission:notification.bindings.verify";
    public const string BindingsListUser = "Permission:notification.bindings.list-user";

    // Preference Operations
    public const string PreferencesRead = "Permission:notification.preferences.read";
    public const string PreferencesUpdate = "Permission:notification.preferences.update";
    public const string PreferencesDelete = "Permission:notification.preferences.delete";
    public const string PreferencesReadAny = "Permission:notification.preferences.read-any";

    // Delivery Log Operations
    public const string LogsRead = "Permission:notification.logs.read";
    public const string LogsReadUser = "Permission:notification.logs.read-user";
    public const string LogsExport = "Permission:notification.logs.export";
    public const string LogsPurge = "Permission:notification.logs.purge";

    // System Operations
    public const string SystemConfigure = "Permission:notification.system.configure";
    public const string SystemViewStats = "Permission:notification.system.view-stats";
    public const string SystemManageChannels = "Permission:notification.system.manage-channels";

    /// <summary>
    /// Gets all defined permissions.
    /// </summary>
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