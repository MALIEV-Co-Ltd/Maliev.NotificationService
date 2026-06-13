using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Api.Consumers;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.NotificationService.Tests.Unit.Consumers;

public sealed class PaymentTerminalEventConsumerTests
{
    [Fact]
    public async Task Consume_PaymentFailedEvent_WithoutPayload_ShouldIgnoreEvent()
    {
        var logger = new Mock<ILogger<PaymentFailedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new PaymentFailedEventConsumer(logger.Object, null!, publishEndpoint.Object);
        var context = new Mock<ConsumeContext<PaymentFailedEvent>>();
        context.Setup(c => c.Message).Returns(new PaymentFailedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(PaymentFailedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "PaymentService",
            ConsumedBy: ["NotificationService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: null!));
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(context.Object);

        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_PaymentCancelledEvent_WithoutPayload_ShouldIgnoreEvent()
    {
        var logger = new Mock<ILogger<PaymentCancelledEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new PaymentCancelledEventConsumer(logger.Object, null!, publishEndpoint.Object);
        var context = new Mock<ConsumeContext<PaymentCancelledEvent>>();
        context.Setup(c => c.Message).Returns(new PaymentCancelledEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(PaymentCancelledEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "PaymentService",
            ConsumedBy: ["NotificationService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: null!));
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(context.Object);

        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_PaymentExpiredEvent_WithoutPayload_ShouldIgnoreEvent()
    {
        var logger = new Mock<ILogger<PaymentExpiredEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new PaymentExpiredEventConsumer(logger.Object, null!, publishEndpoint.Object);
        var context = new Mock<ConsumeContext<PaymentExpiredEvent>>();
        context.Setup(c => c.Message).Returns(new PaymentExpiredEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(PaymentExpiredEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "PaymentService",
            ConsumedBy: ["NotificationService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: null!));
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(context.Object);

        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_PaymentPendingEvent_WithoutPayload_ShouldIgnoreEvent()
    {
        var logger = new Mock<ILogger<PaymentPendingEventConsumer>>();
        var consumer = new PaymentPendingEventConsumer(logger.Object, null!);
        var context = new Mock<ConsumeContext<PaymentPendingEvent>>();
        context.Setup(c => c.Message).Returns(new PaymentPendingEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(PaymentPendingEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "PaymentService",
            ConsumedBy: ["NotificationService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: null!));
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(context.Object);
    }
}
