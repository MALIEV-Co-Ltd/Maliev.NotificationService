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
}
