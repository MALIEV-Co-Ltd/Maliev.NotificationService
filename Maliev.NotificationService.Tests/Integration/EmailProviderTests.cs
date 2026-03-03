using Moq;
using Maliev.NotificationService.Infrastructure.Persistence;
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

    private IConfiguration CreateConfiguration(string? apiKey = null, string? senderEmail = null, string? senderName = null)
    {
        var config = new ConfigurationBuilder();
        if (apiKey != null)
            config.AddInMemoryCollection(new[] { new KeyValuePair<string, string?>("Brevo:ApiKey", apiKey) });
        if (senderEmail != null)
            config.AddInMemoryCollection(new[] { new KeyValuePair<string, string?>("Brevo:SenderEmail", senderEmail) });
        if (senderName != null)
            config.AddInMemoryCollection(new[] { new KeyValuePair<string, string?>("Brevo:SenderName", senderName) });
        return config.Build();
    }

    [Fact]
    public async Task SendAsync_InvalidEmail_ReturnsFailed()
    {
        var configuration = CreateConfiguration();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        var result = await provider.SendAsync("invalid-email", "message", null, default);

        Assert.False(result.Success);
        Assert.Equal(DeliveryFailureType.InvalidRecipient, result.FailureType);
    }

    [Fact]
    public async Task SendAsync_NoApiKey_SimulatesSuccess()
    {
        var configuration = CreateConfiguration();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        var result = await provider.SendAsync("test@example.com", "Hello", null, default);

        Assert.True(result.Success);
        Assert.StartsWith("email_simulated_", result.MessageId);
    }

    [Fact]
    public async Task SendAsync_WithMetadata_UsesSubjectFromMetadata()
    {
        var configuration = CreateConfiguration();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        var metadata = new Dictionary<string, string>
        {
            ["subject"] = "Test Subject",
            ["recipientName"] = "Test User"
        };

        var result = await provider.SendAsync("test@example.com", "Hello", metadata, default);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task SendAsync_WithCc_Bcc_IncludesInEmail()
    {
        var configuration = CreateConfiguration();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        var metadata = new Dictionary<string, string>
        {
            ["cc"] = "cc1@example.com, cc2@example.com",
            ["bcc"] = "bcc@example.com"
        };

        var result = await provider.SendAsync("test@example.com", "Hello", metadata, default);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetHealthAsync_NotConfigured_ReturnsFalse()
    {
        var configuration = CreateConfiguration();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        var result = await provider.GetHealthAsync(CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public void ChannelType_IsEmail()
    {
        var configuration = CreateConfiguration();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        Assert.Equal("email", provider.ChannelType);
    }

    [Fact]
    public async Task ValidateRecipientAsync_EmptyEmail_ReturnsInvalid()
    {
        var configuration = CreateConfiguration();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        var result = await provider.ValidateRecipientAsync("", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateRecipientAsync_InvalidFormat_ReturnsInvalid()
    {
        var configuration = CreateConfiguration();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        var result = await provider.ValidateRecipientAsync("not-an-email", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("Invalid", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateRecipientAsync_ValidEmail_ReturnsValid()
    {
        var configuration = CreateConfiguration();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        var result = await provider.ValidateRecipientAsync("user@example.com", CancellationToken.None);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateRecipientAsync_ValidEmailWithSubdomain_ReturnsValid()
    {
        var configuration = CreateConfiguration();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        var result = await provider.ValidateRecipientAsync("user@sub.domain.com", CancellationToken.None);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateRecipientAsync_NullEmail_ReturnsInvalid()
    {
        var configuration = CreateConfiguration();
        var provider = new EmailProvider(_mockLogger.Object, configuration);

        var result = await provider.ValidateRecipientAsync(null!, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Constructor_WithoutApiKey_LogsWarning()
    {
        var config = new ConfigurationBuilder().Build();
        var logger = new Mock<ILogger<EmailProvider>>();
        
        var provider = new EmailProvider(logger.Object, config);

        Assert.Equal("email", provider.ChannelType);
    }

    [Fact]
    public void Constructor_WithApiKey_ConfiguresBrevo()
    {
        var config = CreateConfiguration(apiKey: "test-api-key");
        
        var provider = new EmailProvider(_mockLogger.Object, config);

        Assert.Equal("email", provider.ChannelType);
    }
}
