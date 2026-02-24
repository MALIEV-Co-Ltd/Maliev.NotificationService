using System.Net;

namespace Maliev.NotificationService.Api.Tests.TestHelpers;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
    {
        _sendAsync = sendAsync;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _sendAsync(request, cancellationToken);
    }

    public static MockHttpMessageHandler CreateSuccess(string responseContent = "{\"success\":true}")
    {
        return new MockHttpMessageHandler((request, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent)
            };
            return Task.FromResult(response);
        });
    }

    public static MockHttpMessageHandler CreateFailure(HttpStatusCode statusCode, string errorMessage = "Error")
    {
        return new MockHttpMessageHandler((request, ct) =>
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(errorMessage)
            };
            return Task.FromResult(response);
        });
    }
}
