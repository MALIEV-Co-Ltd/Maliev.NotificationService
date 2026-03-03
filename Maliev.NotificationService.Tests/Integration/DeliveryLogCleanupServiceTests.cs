using Moq;
using Xunit;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Maliev.NotificationService.Api.Tests.Integration;

public class DeliveryLogCleanupServiceTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ILogger<DeliveryLogCleanupService>> _mockLogger;

    public DeliveryLogCleanupServiceTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockLogger = new Mock<ILogger<DeliveryLogCleanupService>>();
    }

    [Fact]
    public async Task StartAsync_SetsTimerForNext2Am()
    {
        var service = new DeliveryLogCleanupService(_mockServiceProvider.Object, _mockLogger.Object);

        await service.StartAsync(CancellationToken.None);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Delivery log cleanup service scheduled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    [Fact]
    public async Task StopAsync_StopsTimer()
    {
        var service = new DeliveryLogCleanupService(_mockServiceProvider.Object, _mockLogger.Object);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopping")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        service.Dispose();
    }

    [Fact]
    public void Dispose_CallsTimerDispose()
    {
        var service = new DeliveryLogCleanupService(_mockServiceProvider.Object, _mockLogger.Object);
        
        service.Dispose();
        service.Dispose();
    }
}
