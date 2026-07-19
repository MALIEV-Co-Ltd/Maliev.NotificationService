using System.Net;
using System.Net.Http.Json;
using Maliev.MessagingContracts;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Api.Authorization;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Integration;

/// <summary>
/// Integration tests for the notification-event dispatch API.
/// </summary>
public sealed class NotificationEventsApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public NotificationEventsApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient(permissions: [NotificationPermissions.Send]);
    }

    [Fact]
    public async Task Dispatch_ValidNotificationEvent_CreatesDeliveryLog()
    {
        var userId = $"user-{Guid.NewGuid():N}";
        var eventId = Guid.NewGuid();

        await SeedEmailPreferenceAsync(userId, "customer@example.com");

        var notificationEvent = new NotificationEvent(
            MessageId: eventId,
            MessageName: nameof(NotificationEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "Maliev.IntranetBff",
            ConsumedBy: ["Maliev.NotificationService"],
            CorrelationId: eventId,
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new NotificationEventPayload(
                NotificationType: "Customer email",
                Priority: "standard",
                TargetUsers: [new NotificationEventPayloadTargetUsersItem(userId, "customer")],
                TemplateId: string.Empty,
                Parameters: new Dictionary<string, string>
                {
                    ["subject"] = "Customer email",
                    ["message"] = "Your quote is ready."
                },
                Metadata: new NotificationEventPayloadMetadata("en", "test")));

        var response = await _client.PostAsJsonAsync("/notification/v1/events", notificationEvent);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var log = await WaitForDeliveryLogAsync(eventId.ToString());
        Assert.Equal(userId, log.UserId);
        Assert.Equal("email", log.ChannelType);
        Assert.Equal("delivered", log.Status);
        Assert.False(string.IsNullOrWhiteSpace(log.ProviderMessageId));
    }

    private async Task SeedEmailPreferenceAsync(string userId, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

        context.UserNotificationPreferences.Add(new UserNotificationPreference
        {
            UserId = userId,
            PrimaryChannelType = "email",
            FallbackChannelTypes = ["sms"],
            OptOutCategories = [],
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        context.ChannelBindings.Add(new ChannelBinding
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChannelType = "email",
            ChannelIdentifier = encryption.Encrypt(email),
            IsValid = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private async Task<DeliveryLog> WaitForDeliveryLogAsync(string eventId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);

        while (DateTimeOffset.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var log = await context.DeliveryLogs.AsNoTracking()
                .FirstOrDefaultAsync(l => l.EventId == eventId);
            if (log is not null)
            {
                return log;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Delivery log for event {eventId} was not created.");
    }
}
