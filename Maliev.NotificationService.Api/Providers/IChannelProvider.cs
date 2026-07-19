namespace Maliev.NotificationService.Api.Providers;

public interface IChannelProvider
{
    string ChannelType { get; }

    Task<DeliveryResult> SendAsync(string recipientId, string message, Dictionary<string, string>? metadata, CancellationToken ct);

    Task<ValidationResult> ValidateRecipientAsync(string recipientId, CancellationToken ct);

    Task<bool> GetHealthAsync(CancellationToken ct);
}
