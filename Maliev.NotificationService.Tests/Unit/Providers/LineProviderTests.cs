using Maliev.NotificationService.Api.Providers;
using Maliev.NotificationService.Api.Tests.TestHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Unit.Providers;

public class LineProviderTests
{
    private LineProvider CreateProvider(string? token = null, IHttpClientFactory? httpClientFactory = null)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["LINE:ChannelAccessToken"]).Returns(token);
        config.Setup(c => c["ApiBaseAddresses:LINE"]).Returns((string?)null);
        var logger = new Mock<ILogger<LineProvider>>();
        var factory = httpClientFactory ?? new Mock<IHttpClientFactory>().Object;
        return new LineProvider(logger.Object, config.Object, factory);
    }

    private static string ValidLineId => "U" + new string('a', 32);

    [Fact]
    public void ChannelType_IsCorrect()
    {
        var provider = CreateProvider();
        Assert.Equal("line", provider.ChannelType);
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
        var result = await provider.ValidateRecipientAsync("not-a-line-id", CancellationToken.None);
        Assert.False(result.IsValid);
        Assert.Contains("Invalid", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateRecipientAsync_ValidLineUserId_ReturnsValid()
    {
        var provider = CreateProvider();
        var result = await provider.ValidateRecipientAsync(ValidLineId, CancellationToken.None);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task SendAsync_InvalidId_ReturnsFailed()
    {
        var provider = CreateProvider();
        var result = await provider.SendAsync("not-valid", "Hello", null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(DeliveryFailureType.InvalidRecipient, result.FailureType);
    }

    [Fact]
    public async Task SendAsync_ValidId_NotConfigured_ReturnsSimulatedSuccess()
    {
        var provider = CreateProvider(token: null);
        var result = await provider.SendAsync(ValidLineId, "Hello", null, CancellationToken.None);
        Assert.True(result.Success);
        Assert.NotNull(result.MessageId);
        Assert.StartsWith("line_", result.MessageId);
        Assert.Contains("simulated", result.ProviderResponse, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetHealthAsync_NotConfigured_ReturnsFalse()
    {
        var provider = CreateProvider(token: null);
        var result = await provider.GetHealthAsync(CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task SendAsync_WithToken_SuccessResponse_ReturnsSuccess()
    {
        var mockHandler = MockHttpMessageHandler.CreateSuccess("{}");
        var httpClient = new HttpClient(mockHandler);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var provider = CreateProvider(token: "test-token", httpClientFactory: mockFactory.Object);
        var result = await provider.SendAsync(ValidLineId, "Hello", null, CancellationToken.None);
        Assert.True(result.Success);
        Assert.NotNull(result.MessageId);
        Assert.StartsWith("line_", result.MessageId);
    }

    [Fact]
    public async Task SendAsync_WithToken_ErrorResponse_ReturnsFailed()
    {
        var mockHandler = MockHttpMessageHandler.CreateFailure(HttpStatusCode.BadRequest, "bad request");
        var httpClient = new HttpClient(mockHandler);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var provider = CreateProvider(token: "test-token", httpClientFactory: mockFactory.Object);
        var result = await provider.SendAsync(ValidLineId, "Hello", null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(DeliveryFailureType.InvalidRecipient, result.FailureType);
    }

    [Fact]
    public async Task SendAsync_WithToken_ServerError_ReturnsTransient()
    {
        var mockHandler = MockHttpMessageHandler.CreateFailure(HttpStatusCode.InternalServerError, "server error");
        var httpClient = new HttpClient(mockHandler);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var provider = CreateProvider(token: "test-token", httpClientFactory: mockFactory.Object);
        var result = await provider.SendAsync(ValidLineId, "Hello", null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(DeliveryFailureType.Transient, result.FailureType);
        Assert.True(result.IsRetryable);
    }

    [Fact]
    public async Task GetHealthAsync_WithToken_SuccessResponse_ReturnsTrue()
    {
        var mockHandler = MockHttpMessageHandler.CreateSuccess("{}");
        var httpClient = new HttpClient(mockHandler);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var provider = CreateProvider(token: "test-token", httpClientFactory: mockFactory.Object);
        var result = await provider.GetHealthAsync(CancellationToken.None);
        Assert.True(result);
    }

    [Fact]
    public async Task GetHealthAsync_WithToken_FailureResponse_ReturnsFalse()
    {
        var mockHandler = MockHttpMessageHandler.CreateFailure(HttpStatusCode.Unauthorized, "unauthorized");
        var httpClient = new HttpClient(mockHandler);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var provider = CreateProvider(token: "test-token", httpClientFactory: mockFactory.Object);
        var result = await provider.GetHealthAsync(CancellationToken.None);
        Assert.False(result);
    }
}
