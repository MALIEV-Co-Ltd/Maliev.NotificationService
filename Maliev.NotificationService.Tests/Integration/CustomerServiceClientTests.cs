using Moq;
using Xunit;
using Maliev.NotificationService.Api.Services.External;
using Maliev.NotificationService.Api.Tests.TestHelpers;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Maliev.NotificationService.Api.Tests.Integration;

public class CustomerServiceClientTests
{
    private readonly Mock<ILogger<CustomerServiceClient>> _mockLogger;

    public CustomerServiceClientTests()
    {
        _mockLogger = new Mock<ILogger<CustomerServiceClient>>();
    }

    [Fact]
    public async Task GetCustomerByIdAsync_Success_ReturnsCustomer()
    {
        var expectedCustomer = new CustomerDto
        {
            CustomerId = Guid.NewGuid(),
            Email = "test@example.com"
        };

        var mockHandler = new MockHttpMessageHandler((request, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedCustomer)
            };
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://test.com/")
        };

        var client = new CustomerServiceClient(httpClient, _mockLogger.Object);

        var result = await client.GetCustomerByIdAsync(expectedCustomer.CustomerId);

        Assert.NotNull(result);
        Assert.Equal(expectedCustomer.CustomerId, result.CustomerId);
        Assert.Equal(expectedCustomer.Email, result.Email);
    }

    [Fact]
    public async Task GetCustomerByIdAsync_NotFound_ReturnsNull()
    {
        var mockHandler = new MockHttpMessageHandler((request, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.NotFound);
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://test.com/")
        };

        var client = new CustomerServiceClient(httpClient, _mockLogger.Object);

        var result = await client.GetCustomerByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCustomerByIdAsync_ServerError_LogsWarningAndReturnsNull()
    {
        var mockHandler = new MockHttpMessageHandler((request, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://test.com/")
        };

        var client = new CustomerServiceClient(httpClient, _mockLogger.Object);

        var result = await client.GetCustomerByIdAsync(Guid.NewGuid());

        Assert.Null(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to fetch customer")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCustomerByIdAsync_HttpException_LogsErrorAndReturnsNull()
    {
        var mockHandler = new MockHttpMessageHandler((request, ct) =>
        {
            throw new HttpRequestException("Connection failed");
        });

        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://test.com/")
        };

        var client = new CustomerServiceClient(httpClient, _mockLogger.Object);

        var result = await client.GetCustomerByIdAsync(Guid.NewGuid());

        Assert.Null(result);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error fetching customer")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
