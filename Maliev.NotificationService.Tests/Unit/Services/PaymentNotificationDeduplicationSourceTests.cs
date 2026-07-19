namespace Maliev.NotificationService.Tests.Unit.Services;

public class PaymentNotificationDeduplicationSourceTests
{
    [Fact]
    public void PaymentCompletedConsumer_DeduplicatesByPaymentRecipientIdentifier()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Maliev.NotificationService.Api",
            "Consumers",
            "PaymentCompletedEventConsumer.cs"));

        Assert.Contains("RecipientIdentifier", source, StringComparison.Ordinal);
        Assert.Contains("payment-{payload.PaymentId}", source, StringComparison.Ordinal);
        Assert.Contains("paymentRecipientIdentifier", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Maliev.NotificationService.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate Maliev.NotificationService repository root.");
    }
}
