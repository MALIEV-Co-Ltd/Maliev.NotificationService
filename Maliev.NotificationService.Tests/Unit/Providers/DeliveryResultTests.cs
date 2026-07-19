using Maliev.NotificationService.Api.Providers;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Unit.Providers;

public class DeliveryResultTests
{
    [Fact]
    public void RateLimited_NoRetryAfter_UsesDefault60Seconds()
    {
        var result = DeliveryResult.RateLimited();

        Assert.False(result.Success);
        Assert.Equal(DeliveryFailureType.RateLimitExceeded, result.FailureType);
        Assert.True(result.IsRetryable);
        Assert.Equal(TimeSpan.FromSeconds(60), result.RetryAfter);
        Assert.Contains("Rate limit", result.ErrorMessage);
    }

    [Fact]
    public void RateLimited_WithCustomRetryAfter_UsesProvidedValue()
    {
        var retryAfter = TimeSpan.FromSeconds(120);
        var result = DeliveryResult.RateLimited(retryAfter);

        Assert.False(result.Success);
        Assert.Equal(DeliveryFailureType.RateLimitExceeded, result.FailureType);
        Assert.Equal(retryAfter, result.RetryAfter);
        Assert.True(result.IsRetryable);
    }

    [Fact]
    public void Successful_SetsCorrectProperties()
    {
        var result = DeliveryResult.Successful("msg-123", "OK");

        Assert.True(result.Success);
        Assert.Equal("msg-123", result.MessageId);
        Assert.Equal("OK", result.ProviderResponse);
        Assert.False(result.IsRetryable);
    }

    [Fact]
    public void Failed_SetsCorrectProperties()
    {
        var result = DeliveryResult.Failed(DeliveryFailureType.InvalidRecipient, "Bad recipient", isRetryable: false);

        Assert.False(result.Success);
        Assert.Equal(DeliveryFailureType.InvalidRecipient, result.FailureType);
        Assert.Equal("Bad recipient", result.ErrorMessage);
        Assert.False(result.IsRetryable);
    }
}
