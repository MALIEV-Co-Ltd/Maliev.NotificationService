using System.Text.Json;
using Maliev.MessagingContracts;
using Maliev.MessagingContracts.Contracts.Pdf;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Api.Consumers;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using Maliev.NotificationService.Tests.Testing;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Integration;

public class PdfGenerationFailedEventConsumerTests : IClassFixture<BaseIntegrationTestFactory<Program, NotificationDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, NotificationDbContext> _factory;

    public PdfGenerationFailedEventConsumerTests(BaseIntegrationTestFactory<Program, NotificationDbContext> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Consume_PdfGenerationFailedEvent_ShouldNotifyOperationsAndCreateDeliveryLog()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PdfGenerationFailedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new PdfGenerationFailedEventConsumer(logger, context, publishEndpoint.Object);
        var messageId = Guid.NewGuid();
        var evt = CreateEvent(messageId);
        var mockContext = CreateConsumeContext(evt);

        await consumer.Consume(mockContext.Object);

        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        Assert.Equal(NotificationBootstrapData.OperationsInboxUserId, logs[0].UserId);
        Assert.Equal("pdf-pdf-request-1", logs[0].RecipientIdentifier);
        Assert.Contains("DeliveryNote", logs[0].MessageContent, StringComparison.Ordinal);

        publishEndpoint.Verify(
            p => p.Publish(
                It.Is<NotificationEvent>(notificationEvent =>
                    notificationEvent.CausationId == messageId &&
                    notificationEvent.CorrelationId == evt.CorrelationId &&
                    notificationEvent.Payload.NotificationType == "PdfGenerationFailedOperations" &&
                    notificationEvent.Payload.Priority == "High" &&
                    notificationEvent.Payload.TemplateId == "operations-pdf-generation-failed" &&
                    notificationEvent.Payload.TargetUsers.Count == 1 &&
                    notificationEvent.Payload.TargetUsers[0].UserId == NotificationBootstrapData.OperationsInboxUserId &&
                    notificationEvent.Payload.TargetUsers[0].UserType == "staff" &&
                    HasParameter(notificationEvent.Payload.Parameters, "requestId", "pdf-request-1") &&
                    HasParameter(notificationEvent.Payload.Parameters, "referenceId", "DN-2026-0001") &&
                    HasParameter(notificationEvent.Payload.Parameters, "documentType", "DeliveryNote") &&
                    HasParameter(notificationEvent.Payload.Parameters, "errorMessage", "render failed")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_PdfGenerationFailedEvent_WhenAlreadyReceived_ShouldNotPublishDuplicateNotification()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PdfGenerationFailedEventConsumer>>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var consumer = new PdfGenerationFailedEventConsumer(logger, context, publishEndpoint.Object);
        var messageId = Guid.NewGuid();
        context.DeliveryLogs.Add(new DeliveryLog
        {
            EventId = messageId.ToString(),
            UserId = NotificationBootstrapData.OperationsInboxUserId,
            ChannelType = "rabbitmq-event",
            RecipientIdentifier = "pdf-pdf-request-1",
            Status = "received",
            MessageContent = "PDF generation failed",
            AttemptNumber = 1,
            DeliveredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        var evt = CreateEvent(messageId);
        var mockContext = CreateConsumeContext(evt);

        await consumer.Consume(mockContext.Object);

        var logs = await context.DeliveryLogs.Where(l => l.EventId == messageId.ToString()).ToListAsync();
        Assert.Single(logs);
        publishEndpoint.Verify(
            p => p.Publish(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static PdfGenerationFailedEvent CreateEvent(Guid messageId)
    {
        return new PdfGenerationFailedEvent(
            MessageId: messageId,
            MessageName: nameof(PdfGenerationFailedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "PdfService",
            ConsumedBy: ["NotificationService"],
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new PdfGenerationFailedEventPayload(
                RequestId: "pdf-request-1",
                ReferenceId: "DN-2026-0001",
                DocumentType: "DeliveryNote",
                ErrorMessage: "render failed",
                FailedAt: DateTimeOffset.UtcNow));
    }

    private static Mock<ConsumeContext<PdfGenerationFailedEvent>> CreateConsumeContext(PdfGenerationFailedEvent evt)
    {
        var mockContext = new Mock<ConsumeContext<PdfGenerationFailedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);
        mockContext.Setup(m => m.CancellationToken).Returns(CancellationToken.None);
        return mockContext;
    }

    private static bool HasParameter(object parameters, string key, string expectedValue)
    {
        if (parameters is IReadOnlyDictionary<string, object> dictionary &&
            dictionary.TryGetValue(key, out var value))
        {
            return string.Equals(value?.ToString(), expectedValue, StringComparison.Ordinal);
        }

        var json = JsonSerializer.Serialize(parameters);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(key, out var property) &&
            string.Equals(property.ToString(), expectedValue, StringComparison.Ordinal);
    }
}
