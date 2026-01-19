using Moq;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Api.Tests.TestHelpers;
using System.Net;

namespace Maliev.NotificationService.Api.Tests.Integration;

public class AlertingServiceTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<AlertingService>> _mockLogger;

    public AlertingServiceTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<AlertingService>>();
    }

    [Fact]
    public async Task SendCriticalFailureAlertAsync_WebhookNotConfigured_LogsWarning()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var service = new AlertingService(_mockHttpClientFactory.Object, configuration, _mockLogger.Object);

        // Act
        await service.SendCriticalFailureAlertAsync("evt-1", "user-1", "Type", "Reason", 3);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("webhook URL not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendCriticalFailureAlertAsync_SuccessfulWebhook_LogsInformation()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Alerting:WebhookUrl"] = "http://fake-webhook.com"
            })
            .Build();

        var handler = MockHttpMessageHandler.CreateSuccess();
        var httpClient = new HttpClient(handler);
        _mockHttpClientFactory.Setup(f => f.CreateClient("Alerting")).Returns(httpClient);

        var service = new AlertingService(_mockHttpClientFactory.Object, configuration, _mockLogger.Object);

        // Act
        await service.SendCriticalFailureAlertAsync("evt-1", "user-1", "Type", "Reason", 3);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully sent critical failure alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendCriticalFailureAlertAsync_FailedWebhook_LogsWarning()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Alerting:WebhookUrl"] = "http://fake-webhook.com"
            })
            .Build();

        var handler = MockHttpMessageHandler.CreateFailure(HttpStatusCode.BadRequest);
        var httpClient = new HttpClient(handler);
        _mockHttpClientFactory.Setup(f => f.CreateClient("Alerting")).Returns(httpClient);

        var service = new AlertingService(_mockHttpClientFactory.Object, configuration, _mockLogger.Object);

        // Act
        await service.SendCriticalFailureAlertAsync("evt-1", "user-1", "Type", "Reason", 3);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send critical failure alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
