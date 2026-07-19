namespace Maliev.NotificationService.Api.Consumers;

internal static class NotificationConsumerRouting
{
    public static bool IsRoutedToNotificationService(IEnumerable<string>? consumedBy)
    {
        return consumedBy?.Any(IsNotificationServiceAlias) == true;
    }

    private static bool IsNotificationServiceAlias(string consumer)
    {
        return string.Equals(consumer, "NotificationService", StringComparison.OrdinalIgnoreCase)
            || string.Equals(consumer, "Notification", StringComparison.OrdinalIgnoreCase);
    }
}
