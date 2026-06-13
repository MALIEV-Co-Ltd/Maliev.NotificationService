using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Api.Consumers;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.NotificationService.Tests.Unit.Consumers;

public sealed class PaymentCompletedEventConsumerTests
{
    [Fact]
    public async Task Consume_PaymentCompletedEvent_WithoutPayload_ShouldIgnoreEvent()
    {
        var logger = new Mock<ILogger<PaymentCompletedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new PaymentCompletedEventConsumer(
            logger.Object,
            null!,
            publishEndpoint.Object);
        var evt = new PaymentCompletedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(PaymentCompletedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "PaymentService",
            ConsumedBy: ["NotificationService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: null!);
        var context = new Mock<ConsumeContext<PaymentCompletedEvent>>();
        context.Setup(c => c.Message).Returns(evt);
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(context.Object);

        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
