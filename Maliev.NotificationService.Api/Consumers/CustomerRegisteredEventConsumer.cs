using Maliev.MessagingContracts.Contracts.Customers;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;

namespace Maliev.NotificationService.Api.Consumers
{
    public class CustomerRegisteredEventConsumer : IConsumer<CustomerRegisteredEvent>
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<CustomerRegisteredEventConsumer> _logger;

        public CustomerRegisteredEventConsumer(
            IPublishEndpoint publishEndpoint,
            ILogger<CustomerRegisteredEventConsumer> logger)
        {
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<CustomerRegisteredEvent> context)
        {
            var payload = context.Message.Payload;
            _logger.LogInformation(
                "[NotificationService] Received CustomerRegisteredEvent for Customer {CustomerId}, Method: {Method}",
                payload.CustomerId,
                payload.RegistrationMethod);

            if (!string.Equals(payload.RegistrationMethod, "Google", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "[NotificationService] Skipping welcome email for Customer {CustomerId} — registration method is {Method} (verification email sent separately by AuthService)",
                    payload.CustomerId,
                    payload.RegistrationMethod);
                return;
            }

            var notificationEvent = new NotificationEvent(
                MessageId: Guid.NewGuid(),
                MessageName: "CustomerRegistered",
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
                            payload.CustomerId.ToString(),
                            "direct-email")
                    },
                    TemplateId: "customer-welcome-google",
                    Parameters: new Dictionary<string, object>
                    {
                        ["firstName"] = payload.FirstName,
                        ["recipientEmail"] = payload.Email
                    },
                    Metadata: new NotificationEventPayloadMetadata(
                        Language: "en",
                        Source: "NotificationService")
                )
            );

            await _publishEndpoint.Publish(notificationEvent, context.CancellationToken);

            _logger.LogInformation(
                "[NotificationService] Published NotificationEvent for CustomerRegistered, Template: customer-welcome-google");
        }
    }
}
