using Maliev.NotificationService.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Unit.Services;

public class RetryCleanupBackgroundServiceTests
{
    private static RetryCleanupBackgroundService CreateService(IRetryService? retryService = null)
    {
        var mockRetryService = retryService ?? new Mock<IRetryService>().Object;
        var services = new ServiceCollection();
        services.AddSingleton(mockRetryService);
        var provider = services.BuildServiceProvider();
        var logger = new Mock<ILogger<RetryCleanupBackgroundService>>();
        return new RetryCleanupBackgroundService(provider, logger.Object);
    }

    [Fact]
    public async Task StartAsync_DoesNotThrow()
    {
        var service = CreateService();

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    [Fact]
    public async Task StopAsync_WithoutStart_DoesNotThrow()
    {
        var service = CreateService();
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DoWorkAsync_InvokesCleanupStaleRetries()
    {
        var mockRetryService = new Mock<IRetryService>();
        mockRetryService.Setup(r => r.CleanupStaleRetriesAsync()).Returns(Task.CompletedTask);

        var service = CreateService(mockRetryService.Object);

        // Invoke private DoWorkAsync via reflection
        var method = typeof(RetryCleanupBackgroundService)
            .GetMethod("DoWorkAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        await (Task)method.Invoke(service, new object?[] { null })!;

        mockRetryService.Verify(r => r.CleanupStaleRetriesAsync(), Times.Once);
    }

    [Fact]
    public async Task DoWorkAsync_WhenRetryServiceThrows_DoesNotPropagate()
    {
        var mockRetryService = new Mock<IRetryService>();
        mockRetryService.Setup(r => r.CleanupStaleRetriesAsync())
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var service = CreateService(mockRetryService.Object);

        var method = typeof(RetryCleanupBackgroundService)
            .GetMethod("DoWorkAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Should not throw - exception is caught internally
        await (Task)method.Invoke(service, new object?[] { null })!;
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var service = CreateService();
        service.Dispose();
        service.Dispose(); // Second dispose should not throw
    }
}
