using Maliev.MessagingContracts.Contracts.Auth;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;

namespace Maliev.NotificationService.Api.Consumers
{
    public class VerificationEmailRequestedEventConsumer : IConsumer<VerificationEmailRequestedEvent>
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<VerificationEmailRequestedEventConsumer> _logger;

        public VerificationEmailRequestedEventConsumer(
            IPublishEndpoint publishEndpoint,
            ILogger<VerificationEmailRequestedEventConsumer> logger)
        {
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<VerificationEmailRequestedEvent> context)
        {
            var payload = context.Message.Payload;
            _logger.LogInformation(
                "[NotificationService] Received VerificationEmailRequestedEvent for Principal {PrincipalId}",
                payload.PrincipalId);

            var verificationUrl = $"https://www.maliev.com/auth/verify-email?token={payload.VerificationToken}";

            var notificationEvent = new NotificationEvent(
                MessageId: Guid.NewGuid(),
                MessageName: "VerificationEmailRequested",
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
                    TemplateId: "customer-welcome-email",
                    Parameters: new Dictionary<string, object>
                    {
                        ["firstName"] = payload.FirstName,
                        ["verificationUrl"] = verificationUrl,
                        ["recipientEmail"] = payload.Email
                    },
                    Metadata: new NotificationEventPayloadMetadata(
                        Language: "en",
                        Source: "NotificationService")
                )
            );

            await _publishEndpoint.Publish(notificationEvent, context.CancellationToken);

            _logger.LogInformation(
                "[NotificationService] Published NotificationEvent for VerificationEmailRequested, Principal: {PrincipalId}",
                payload.PrincipalId);
        }
    }
}
