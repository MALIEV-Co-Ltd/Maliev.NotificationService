using System.Globalization;

namespace Maliev.NotificationService.Api.Consumers;

internal static class PaymentNotificationFormatting
{
    public static string FormatAmount(double amount, string currency)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{amount:0.00} {currency}");
    }
}
