namespace Maliev.NotificationService.Api.Providers;

public record DeliveryResult
{
    public bool Success { get; init; }
    public string? MessageId { get; init; }
    public DeliveryFailureType? FailureType { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsRetryable { get; init; }
    public TimeSpan? RetryAfter { get; init; }
    public string? ProviderResponse { get; init; }

    public static DeliveryResult Successful(string messageId, string? providerResponse = null) =>
        new()
        {
            Success = true,
            MessageId = messageId,
            ProviderResponse = providerResponse,
            IsRetryable = false
        };

    public static DeliveryResult Failed(DeliveryFailureType failureType, string errorMessage, bool isRetryable = true, TimeSpan? retryAfter = null) =>
        new()
        {
            Success = false,
            FailureType = failureType,
            ErrorMessage = errorMessage,
            IsRetryable = isRetryable,
            RetryAfter = retryAfter
        };

    public static DeliveryResult RateLimited(TimeSpan? retryAfter = null) =>
        new()
        {
            Success = false,
            FailureType = DeliveryFailureType.RateLimitExceeded,
            ErrorMessage = "Rate limit exceeded",
            IsRetryable = true,
            RetryAfter = retryAfter ?? TimeSpan.FromSeconds(60)
        };
}

public enum DeliveryFailureType
{
    Transient,
    RateLimitExceeded,
    InvalidRecipient,
    AuthenticationFailed,
    ProviderError
}
