using Maliev.NotificationService.Api.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Unit.Providers;

public class FacebookMessengerProviderTests
{
    private readonly FacebookMessengerProvider _provider;

    public FacebookMessengerProviderTests()
    {
        var config = new Mock<IConfiguration>();
        var logger = new Mock<ILogger<FacebookMessengerProvider>>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        _provider = new FacebookMessengerProvider(logger.Object, config.Object, httpClientFactory.Object);
    }

    [Fact]
    public void ChannelType_IsCorrect()
    {
        Assert.Equal("facebook", _provider.ChannelType);
    }

    [Fact]
    public async Task ValidateRecipientAsync_EmptyPsid_ReturnsInvalid()
    {
        var result = await _provider.ValidateRecipientAsync("", CancellationToken.None);
        Assert.False(result.IsValid);
        Assert.Contains("required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateRecipientAsync_WhitespacePsid_ReturnsInvalid()
    {
        var result = await _provider.ValidateRecipientAsync("   ", CancellationToken.None);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateRecipientAsync_NonNumericPsid_ReturnsInvalid()
    {
        var result = await _provider.ValidateRecipientAsync("abc123def456", CancellationToken.None);
        Assert.False(result.IsValid);
        Assert.Contains("numeric", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateRecipientAsync_TooShortNumeric_ReturnsInvalid()
    {
        // Regex requires 10-20 digits; 5 digits is too short
        var result = await _provider.ValidateRecipientAsync("12345", CancellationToken.None);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateRecipientAsync_ValidPsid_ReturnsValid()
    {
        var result = await _provider.ValidateRecipientAsync("1234567890123456", CancellationToken.None);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task SendAsync_InvalidPsid_ReturnsFailed()
    {
        var result = await _provider.SendAsync("not-numeric", "Hello", null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(DeliveryFailureType.InvalidRecipient, result.FailureType);
    }

    [Fact]
    public async Task SendAsync_ValidPsid_ReturnsSuccess()
    {
        var result = await _provider.SendAsync("1234567890123456", "Hello", null, CancellationToken.None);
        Assert.True(result.Success);
        Assert.NotNull(result.MessageId);
        Assert.StartsWith("fb_mid.", result.MessageId);
    }

    [Fact]
    public async Task GetHealthAsync_AlwaysReturnsTrue()
    {
        var result = await _provider.GetHealthAsync(CancellationToken.None);
        Assert.True(result);
    }
}
