using Moq;
using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Maliev.NotificationService.Api.Providers;
using System.Net;

namespace Maliev.NotificationService.Api.Tests.Integration;

public class EmailProviderTests
{
    private readonly Mock<ILogger<EmailProvider>> _mockLogger;

    public EmailProviderTests()
    {
        _mockLogger = new Mock<ILogger<EmailProvider>>();
    }

    [Fact]
    public async Task SendAsync_InvalidEmail_ReturnsFailed()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        // Act
        var result = await provider.SendAsync("invalid-email", "message", null, default);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(DeliveryFailureType.InvalidRecipient, result.FailureType);
    }

    [Fact]
    public async Task SendAsync_NoApiKey_SimulatesSuccess()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        // Act
        var result = await provider.SendAsync("test@example.com", "Hello", null, default);

        // Assert
        Assert.True(result.Success);
        Assert.StartsWith("email_simulated_", result.MessageId);
    }

    [Fact]
    public async Task GetHealthAsync_NotConfigured_ReturnsFalse()
    {
        var configuration = new ConfigurationBuilder().Build();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        var result = await provider.GetHealthAsync(CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public void ChannelType_IsEmail()
    {
        var configuration = new ConfigurationBuilder().Build();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        Assert.Equal("email", provider.ChannelType);
    }

    [Fact]
    public async Task ValidateRecipientAsync_EmptyEmail_ReturnsInvalid()
    {
        var configuration = new ConfigurationBuilder().Build();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        var result = await provider.ValidateRecipientAsync("", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateRecipientAsync_InvalidFormat_ReturnsInvalid()
    {
        var configuration = new ConfigurationBuilder().Build();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        var result = await provider.ValidateRecipientAsync("not-an-email", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("Invalid", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateRecipientAsync_ValidEmail_ReturnsValid()
    {
        var configuration = new ConfigurationBuilder().Build();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        var result = await provider.ValidateRecipientAsync("user@example.com", CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
