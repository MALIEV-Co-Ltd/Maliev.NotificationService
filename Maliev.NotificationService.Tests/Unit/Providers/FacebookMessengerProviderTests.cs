using Maliev.NotificationService.Api.Providers;
using Maliev.NotificationService.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Unit.Providers;

public class FacebookMessengerProviderTests
{
    private readonly FacebookMessengerProvider _provider;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly ExternalProvidersOptions _options;
    private readonly MockHttpMessageHandler _httpMessageHandler;

    public FacebookMessengerProviderTests()
    {
        _options = new ExternalProvidersOptions
        {
            Facebook = new FacebookOptions { PageAccessToken = "test-token" }
        };
        var optionsMock = new Mock<IOptions<ExternalProvidersOptions>>();
        optionsMock.Setup(x => x.Value).Returns(_options);

        var logger = new Mock<ILogger<FacebookMessengerProvider>>();

        _httpMessageHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(_httpMessageHandler)
        {
            BaseAddress = new Uri("https://graph.facebook.com/v18.0/")
        };

        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpClientFactoryMock.Setup(x => x.CreateClient("Facebook")).Returns(httpClient);

        _provider = new FacebookMessengerProvider(logger.Object, _httpClientFactoryMock.Object, optionsMock.Object);
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
        // Arrange
        _httpMessageHandler.Response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"message_id\": \"fb_mid.test123\"}")
        };

        // Act
        var result = await _provider.SendAsync("1234567890123456", "Hello", null, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("fb_mid.test123", result.MessageId);
        Assert.Contains("me/messages?access_token=test-token", _httpMessageHandler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task SendAsync_NotConfigured_ReturnsSimulatedSuccess()
    {
        // Arrange
        var emptyOptions = new ExternalProvidersOptions();
        var optionsMock = new Mock<IOptions<ExternalProvidersOptions>>();
        optionsMock.Setup(x => x.Value).Returns(emptyOptions);

        var logger = new Mock<ILogger<FacebookMessengerProvider>>();
        var provider = new FacebookMessengerProvider(logger.Object, _httpClientFactoryMock.Object, optionsMock.Object);

        // Act
        var result = await provider.SendAsync("1234567890123456", "Hello", null, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.StartsWith("fb_mid.", result.MessageId);
        Assert.Contains("simulated", result.ProviderResponse, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_ApiError_ReturnsFailed()
    {
        // Arrange
        _httpMessageHandler.Response = new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\": \"Invalid parameter\"}")
        };

        // Act
        var result = await _provider.SendAsync("1234567890123456", "Hello", null, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(DeliveryFailureType.InvalidRecipient, result.FailureType);
    }

    [Fact]
    public async Task GetHealthAsync_TokenValid_ReturnsTrue()
    {
        // Arrange
        _httpMessageHandler.Response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

        // Act
        var result = await _provider.GetHealthAsync(CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Contains("me?fields=id&access_token=test-token", _httpMessageHandler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetHealthAsync_TokenInvalid_ReturnsFalse()
    {
        // Arrange
        _httpMessageHandler.Response = new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);

        // Act
        var result = await _provider.GetHealthAsync(CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public HttpResponseMessage Response { get; set; } = new(System.Net.HttpStatusCode.OK);
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Response);
        }
    }
}
