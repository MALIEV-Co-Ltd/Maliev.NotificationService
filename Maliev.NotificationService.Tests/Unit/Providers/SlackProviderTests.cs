using Maliev.NotificationService.Api.Providers;
using Maliev.NotificationService.Api.Tests.TestHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Unit.Providers;

public class SlackProviderTests
{
    private SlackProvider CreateProvider(string? botToken = null, IHttpClientFactory? httpClientFactory = null)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Slack:BotToken"]).Returns(botToken);
        config.Setup(c => c["ApiBaseAddresses:Slack"]).Returns((string?)null);
        var logger = new Mock<ILogger<SlackProvider>>();
        var factory = httpClientFactory ?? new Mock<IHttpClientFactory>().Object;
        return new SlackProvider(logger.Object, config.Object, factory);
    }

    [Fact]
    public void ChannelType_IsCorrect()
    {
        var provider = CreateProvider();
        Assert.Equal("slack", provider.ChannelType);
    }

    [Fact]
    public async Task ValidateRecipientAsync_EmptyId_ReturnsInvalid()
    {
        var provider = CreateProvider();
        var result = await provider.ValidateRecipientAsync("", CancellationToken.None);
        Assert.False(result.IsValid);
        Assert.Contains("required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateRecipientAsync_WhitespaceId_ReturnsInvalid()
    {
        var provider = CreateProvider();
        var result = await provider.ValidateRecipientAsync("   ", CancellationToken.None);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateRecipientAsync_InvalidFormat_ReturnsInvalid()
    {
        var provider = CreateProvider();
        var result = await provider.ValidateRecipientAsync("invalid_format", CancellationToken.None);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("U1234567890")]
    [InlineData("C1234567890")]
    [InlineData("#general")]
    [InlineData("#random-channel")]
    public async Task ValidateRecipientAsync_ValidFormats_ReturnsValid(string id)
    {
        var provider = CreateProvider();
        var result = await provider.ValidateRecipientAsync(id, CancellationToken.None);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task SendAsync_InvalidId_ReturnsFailed()
    {
        var provider = CreateProvider();
        var result = await provider.SendAsync("invalid_format", "Hello", null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(DeliveryFailureType.InvalidRecipient, result.FailureType);
    }

    [Fact]
    public async Task SendAsync_ValidId_NotConfigured_ReturnsSimulatedSuccess()
    {
        var provider = CreateProvider(botToken: null);
        var result = await provider.SendAsync("U1234567890", "Hello", null, CancellationToken.None);
        Assert.True(result.Success);
        Assert.NotNull(result.MessageId);
        Assert.Contains("simulated", result.ProviderResponse, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetHealthAsync_NotConfigured_ReturnsFalse()
    {
        var provider = CreateProvider(botToken: null);
        var result = await provider.GetHealthAsync(CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task SendAsync_WithApiKey_SuccessResponse_ReturnsSuccess()
    {
        var mockHandler = MockHttpMessageHandler.CreateSuccess("{\"ok\":true,\"ts\":\"1234567890.123456\"}");
        var httpClient = new HttpClient(mockHandler);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var provider = CreateProvider(botToken: "xoxb-test-token", httpClientFactory: mockFactory.Object);
        var result = await provider.SendAsync("U1234567890", "Hello", null, CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal("1234567890.123456", result.MessageId);
    }

    [Fact]
    public async Task SendAsync_WithApiKey_SuccessResponseNoTs_GeneratesMessageId()
    {
        var mockHandler = MockHttpMessageHandler.CreateSuccess("{\"ok\":true}");
        var httpClient = new HttpClient(mockHandler);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var provider = CreateProvider(botToken: "xoxb-test-token", httpClientFactory: mockFactory.Object);
        var result = await provider.SendAsync("U1234567890", "Hello", null, CancellationToken.None);
        Assert.True(result.Success);
        Assert.NotNull(result.MessageId);
    }

    [Fact]
    public async Task SendAsync_WithApiKey_ErrorResponse_ReturnsFailed()
    {
        var mockHandler = MockHttpMessageHandler.CreateSuccess("{\"ok\":false,\"error\":\"channel_not_found\"}");
        var httpClient = new HttpClient(mockHandler);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var provider = CreateProvider(botToken: "xoxb-test-token", httpClientFactory: mockFactory.Object);
        var result = await provider.SendAsync("U1234567890", "Hello", null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(DeliveryFailureType.InvalidRecipient, result.FailureType);
    }

    [Fact]
    public async Task SendAsync_WithApiKey_AuthErrorResponse_ReturnsAuthFailed()
    {
        var mockHandler = MockHttpMessageHandler.CreateSuccess("{\"ok\":false,\"error\":\"invalid_auth\"}");
        var httpClient = new HttpClient(mockHandler);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var provider = CreateProvider(botToken: "xoxb-test-token", httpClientFactory: mockFactory.Object);
        var result = await provider.SendAsync("U1234567890", "Hello", null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(DeliveryFailureType.AuthenticationFailed, result.FailureType);
    }

    [Fact]
    public async Task GetHealthAsync_WithApiKey_SuccessResponse_ReturnsTrue()
    {
        var mockHandler = MockHttpMessageHandler.CreateSuccess("{\"ok\":true}");
        var httpClient = new HttpClient(mockHandler);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var provider = CreateProvider(botToken: "xoxb-test-token", httpClientFactory: mockFactory.Object);
        var result = await provider.GetHealthAsync(CancellationToken.None);
        Assert.True(result);
    }

    [Fact]
    public async Task GetHealthAsync_WithApiKey_FailureResponse_ReturnsFalse()
    {
        var mockHandler = MockHttpMessageHandler.CreateSuccess("{\"ok\":false}");
        var httpClient = new HttpClient(mockHandler);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var provider = CreateProvider(botToken: "xoxb-test-token", httpClientFactory: mockFactory.Object);
        var result = await provider.GetHealthAsync(CancellationToken.None);
        Assert.False(result);
    }
}
