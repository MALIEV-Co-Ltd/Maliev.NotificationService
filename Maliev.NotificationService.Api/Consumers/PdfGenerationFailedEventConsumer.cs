using Maliev.MessagingContracts;
using Maliev.MessagingContracts.Contracts.Pdf;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.NotificationService.Api.Consumers;

/// <summary>
/// Consumes PDF generation failures and alerts operations without notifying customers directly.
/// </summary>
public class PdfGenerationFailedEventConsumer : IConsumer<PdfGenerationFailedEvent>
{
    private readonly NotificationDbContext _dbContext;
    private readonly ILogger<PdfGenerationFailedEventConsumer> _logger;
    private readonly IPublishEndpoint _publishEndpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfGenerationFailedEventConsumer"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="dbContext">Notification database context.</param>
    /// <param name="publishEndpoint">Publish endpoint for notification events.</param>
    public PdfGenerationFailedEventConsumer(
        ILogger<PdfGenerationFailedEventConsumer> logger,
        NotificationDbContext dbContext,
        IPublishEndpoint publishEndpoint)
    {
        _logger = logger;
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<PdfGenerationFailedEvent> context)
    {
        var payload = context.Message.Payload;
        var eventId = context.Message.MessageId.ToString();
        var alreadyReceived = await _dbContext.DeliveryLogs
            .AsNoTracking()
            .AnyAsync(
                log => log.EventId == eventId && log.UserId == NotificationBootstrapData.OperationsInboxUserId,
                context.CancellationToken);

        if (alreadyReceived)
        {
            _logger.LogInformation(
                "Skipping duplicate PDF generation failure notification for event {MessageId}, request {RequestId}",
                context.Message.MessageId,
                payload.RequestId);
            return;
        }

        await _publishEndpoint.Publish(new NotificationEvent(
            MessageId: Guid.NewGuid(),
            MessageName: "PdfGenerationFailedOperationsNotification",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "NotificationService",
            ConsumedBy: ["NotificationService"],
            CorrelationId: context.Message.CorrelationId,
            CausationId: context.Message.MessageId,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new NotificationEventPayload(
                NotificationType: "PdfGenerationFailedOperations",
                Priority: "High",
                TargetUsers:
                [
                    new NotificationEventPayloadTargetUsersItem(
                        NotificationBootstrapData.OperationsInboxUserId,
                        "staff")
                ],
                TemplateId: "operations-pdf-generation-failed",
                Parameters: new Dictionary<string, object>
                {
                    ["requestId"] = payload.RequestId,
                    ["referenceId"] = payload.ReferenceId,
                    ["documentType"] = payload.DocumentType,
                    ["errorMessage"] = payload.ErrorMessage,
                    ["failedAt"] = payload.FailedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
                },
                Metadata: new NotificationEventPayloadMetadata(
                    Language: "en",
                    Source: "PdfService")
            )
        ), context.CancellationToken);

        _dbContext.DeliveryLogs.Add(new DeliveryLog
        {
            EventId = eventId,
            UserId = NotificationBootstrapData.OperationsInboxUserId,
            ChannelType = "rabbitmq-event",
            RecipientIdentifier = $"pdf-{payload.RequestId}",
            Status = "received",
            MessageContent = $"PDF generation failed: {payload.DocumentType} {payload.ReferenceId}, Request {payload.RequestId}, Error: {payload.ErrorMessage}",
            AttemptNumber = 1,
            DeliveredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Published operations notification for PDF generation failure {RequestId}, document {DocumentType} {ReferenceId}",
            payload.RequestId,
            payload.DocumentType,
            payload.ReferenceId);
    }
}
