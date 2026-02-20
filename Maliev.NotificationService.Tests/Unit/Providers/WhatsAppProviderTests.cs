using Maliev.NotificationService.Api.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Unit.Providers;

public class WhatsAppProviderTests
{
    private readonly WhatsAppProvider _provider;

    public WhatsAppProviderTests()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Twilio:AccountSid"]).Returns((string?)null);
        config.Setup(c => c["Twilio:AuthToken"]).Returns((string?)null);
        config.Setup(c => c["Twilio:WhatsAppNumber"]).Returns((string?)null);
        var logger = new Mock<ILogger<WhatsAppProvider>>();
        _provider = new WhatsAppProvider(logger.Object, config.Object);
    }

    [Fact]
    public void ChannelType_IsCorrect()
    {
        Assert.Equal("whatsapp", _provider.ChannelType);
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
        var result = await _provider.ValidateRecipientAsync("not-a-phone", CancellationToken.None);
        Assert.False(result.IsValid);
        Assert.Contains("Invalid", result.ErrorMessage);
    }

    [Theory]
    [InlineData("+66812345678")]
    [InlineData("+15551234567")]
    public async Task ValidateRecipientAsync_ValidE164_ReturnsValid(string phone)
    {
        var result = await _provider.ValidateRecipientAsync(phone, CancellationToken.None);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task SendAsync_InvalidPhone_ReturnsFailed()
    {
        var result = await _provider.SendAsync("invalid", "Hello", null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(DeliveryFailureType.InvalidRecipient, result.FailureType);
    }

    [Fact]
    public async Task SendAsync_ValidPhone_NotConfigured_ReturnsSimulatedSuccess()
    {
        var result = await _provider.SendAsync("+66812345678", "Hello", null, CancellationToken.None);
        Assert.True(result.Success);
        Assert.NotNull(result.MessageId);
        Assert.StartsWith("wamid_", result.MessageId);
        Assert.Contains("simulated", result.ProviderResponse, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetHealthAsync_NotConfigured_ReturnsFalse()
    {
        var result = await _provider.GetHealthAsync(CancellationToken.None);
        Assert.False(result);
    }
}
