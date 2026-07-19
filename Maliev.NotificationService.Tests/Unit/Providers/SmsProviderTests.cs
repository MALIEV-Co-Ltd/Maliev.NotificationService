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

    private SmsProvider CreateProviderWithConfig(string? accountSid = null, string? authToken = null, string? phoneNumber = null)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Twilio:AccountSid"]).Returns(accountSid);
        config.Setup(c => c["Twilio:AuthToken"]).Returns(authToken);
        config.Setup(c => c["Twilio:PhoneNumber"]).Returns(phoneNumber);
        var logger = new Mock<ILogger<SmsProvider>>();
        return new SmsProvider(logger.Object, config.Object);
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

    [Fact]
    public async Task SendAsync_WithMetadata_FormatsMessage()
    {
        var result = await _provider.SendAsync("+15551234567", "Hello", new Dictionary<string, string> { ["senderName"] = "Test" }, CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact]
    public void Constructor_WithConfig_SetsProperties()
    {
        var provider = CreateProviderWithConfig("test-sid", "test-token", "+15551234567");
        Assert.Equal("sms", provider.ChannelType);
    }

    [Theory]
    [InlineData("+1")]
    [InlineData("abc")]
    [InlineData("123")]
    public async Task ValidateRecipientAsync_VariousInvalidFormats_ReturnsInvalid(string phone)
    {
        var result = await _provider.ValidateRecipientAsync(phone, CancellationToken.None);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateRecipientAsync_NullPhone_ReturnsInvalid()
    {
        var result = await _provider.ValidateRecipientAsync(null!, CancellationToken.None);
        Assert.False(result.IsValid);
    }
}
