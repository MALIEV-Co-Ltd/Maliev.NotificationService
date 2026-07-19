using Maliev.MessagingContracts.Contracts.Auth;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;

namespace Maliev.NotificationService.Api.Consumers
{
    public class EmailVerifiedEventConsumer : IConsumer<EmailVerifiedEvent>
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<EmailVerifiedEventConsumer> _logger;

        public EmailVerifiedEventConsumer(
            IPublishEndpoint publishEndpoint,
            ILogger<EmailVerifiedEventConsumer> logger)
        {
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<EmailVerifiedEvent> context)
        {
            var payload = context.Message.Payload;
            _logger.LogInformation(
                "[NotificationService] Received EmailVerifiedEvent for Principal {PrincipalId}",
                payload.PrincipalId);

            var notificationEvent = new NotificationEvent(
                MessageId: Guid.NewGuid(),
                MessageName: "EmailVerified",
                MessageType: MessageType.Event,
                MessageVersion: "1.0",
                PublishedBy: "NotificationService",
                ConsumedBy: new[] { "Email" },
                CorrelationId: context.Message.CorrelationId,
                CausationId: context.Message.MessageId,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: false,
                Payload: new NotificationEventPayload(
                    NotificationType: "Transactional",
                    Priority: "Normal",
                    TargetUsers: new[]
                    {
                        new NotificationEventPayloadTargetUsersItem(
                            payload.PrincipalId.ToString(),
                            "Customer")
                    },
                    TemplateId: "customer-email-verified",
                    Parameters: new Dictionary<string, object>
                    {
                        ["firstName"] = "Customer",
                        ["recipientEmail"] = payload.Email
                    },
                    Metadata: new NotificationEventPayloadMetadata(
                        Language: "en",
                        Source: "NotificationService")
                )
            );

            await _publishEndpoint.Publish(notificationEvent, context.CancellationToken);

            _logger.LogInformation(
                "[NotificationService] Published NotificationEvent for EmailVerified, Principal: {PrincipalId}",
                payload.PrincipalId);
        }
    }
}
