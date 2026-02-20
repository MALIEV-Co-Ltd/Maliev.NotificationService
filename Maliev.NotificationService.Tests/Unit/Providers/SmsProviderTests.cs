using Maliev.NotificationService.Api.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Unit.Providers;

public class SmsProviderTests
{
    private readonly SmsProvider _provider;

    public SmsProviderTests()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Twilio:AccountSid"]).Returns((string?)null);
        config.Setup(c => c["Twilio:AuthToken"]).Returns((string?)null);
        config.Setup(c => c["Twilio:PhoneNumber"]).Returns((string?)null);
        var logger = new Mock<ILogger<SmsProvider>>();
        _provider = new SmsProvider(logger.Object, config.Object);
    }

    [Fact]
    public void ChannelType_IsCorrect()
    {
        Assert.Equal("sms", _provider.ChannelType);
    }

    [Fact]
    public async Task ValidateRecipientAsync_EmptyPhone_ReturnsInvalid()
    {
        var result = await _provider.ValidateRecipientAsync("", CancellationToken.None);
        Assert.False(result.IsValid);
        Assert.Contains("required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateRecipientAsync_WhitespacePhone_ReturnsInvalid()
    {
        var result = await _provider.ValidateRecipientAsync("   ", CancellationToken.None);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateRecipientAsync_InvalidFormat_ReturnsInvalid()
    {
        var result = await _provider.ValidateRecipientAsync("123456789", CancellationToken.None);
        Assert.False(result.IsValid);
        Assert.Contains("Invalid", result.ErrorMessage);
    }

    [Theory]
    [InlineData("+15551234567")]
    [InlineData("+66812345678")]
    [InlineData("+447911123456")]
    public async Task ValidateRecipientAsync_ValidE164_ReturnsValid(string phone)
    {
        var result = await _provider.ValidateRecipientAsync(phone, CancellationToken.None);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task SendAsync_InvalidPhone_ReturnsFailedWithInvalidRecipient()
    {
        var result = await _provider.SendAsync("not-a-phone", "Hello", null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(DeliveryFailureType.InvalidRecipient, result.FailureType);
        Assert.False(result.IsRetryable);
    }

    [Fact]
    public async Task SendAsync_ValidPhone_NotConfigured_ReturnsSimulatedSuccess()
    {
        var result = await _provider.SendAsync("+15551234567", "Hello", null, CancellationToken.None);
        Assert.True(result.Success);
        Assert.NotNull(result.MessageId);
        Assert.StartsWith("SM", result.MessageId);
        Assert.Contains("simulated", result.ProviderResponse, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetHealthAsync_NotConfigured_ReturnsFalse()
    {
        var result = await _provider.GetHealthAsync(CancellationToken.None);
        Assert.False(result);
    }
}
