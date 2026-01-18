using Moq;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Maliev.MessagingContracts.Generated;
using Maliev.NotificationService.Api.Consumers;
using Maliev.NotificationService.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Maliev.NotificationService.Tests.Testing;
using Microsoft.Extensions.Logging;

namespace Maliev.NotificationService.Api.Tests.Integration;

public class PaymentCompletedEventConsumerTests : IClassFixture<BaseIntegrationTestFactory<Program, NotificationDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, NotificationDbContext> _factory;

    public PaymentCompletedEventConsumerTests(BaseIntegrationTestFactory<Program, NotificationDbContext> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Consume_PaymentCompletedEvent_ShouldCreateDeliveryLog()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentCompletedEventConsumer>>();

        var consumer = new PaymentCompletedEventConsumer(logger, context);

        var messageId = Guid.NewGuid();
        var evt = new PaymentCompletedEvent(
            MessageId: messageId,
            MessageName: "PaymentCompletedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Payment",
            ConsumedBy: new[] { "Notification" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentCompletedEventPayload(
                OrderId: Guid.NewGuid(),
                OrderNumber: "ORD-123",
                PaymentId: Guid.NewGuid(),
                Amount: 100,
                Currency: "USD"
            )
        );

        var mockContext = new Mock<ConsumeContext<PaymentCompletedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);

        // Act
        await consumer.Consume(mockContext.Object);

        // Assert
        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal("received", logs[0].Status);
    }
}
