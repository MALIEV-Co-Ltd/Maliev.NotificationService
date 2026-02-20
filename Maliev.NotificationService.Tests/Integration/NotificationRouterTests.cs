using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Maliev.MessagingContracts.Generated;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Data.Entities;
using Maliev.NotificationService.Tests.Testing;
using Maliev.NotificationService.Api.Tests.Integration;

namespace Maliev.NotificationService.Tests.Integration;

[Collection("Integration")]
public class NotificationRouterTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public NotificationRouterTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _factory.CleanDatabaseAsync();
    }

    [Fact]
    public async Task RouteAsync_UserOptedOut_ReturnsSkipped()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Maliev.NotificationService.Data.NotificationDbContext>();
        var router = scope.ServiceProvider.GetRequiredService<INotificationRouter>();

        var userId = "opt-out-user";
        var category = "Marketing";

        var preference = new UserNotificationPreference
        {
            UserId = userId,
            PrimaryChannelType = "email",
            OptOutCategories = new List<string> { category },
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.UserNotificationPreferences.Add(preference);
        await context.SaveChangesAsync();

        var notificationEvent = new NotificationEvent(
            MessageId: Guid.NewGuid(),
            MessageName: "NotificationEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "TestService",
            ConsumedBy: new[] { "NotificationService" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new NotificationEventPayload(
                NotificationType: category,
                Priority: "low",
                TargetUsers: new[] { new NotificationEventPayloadTargetUsersItem(userId, "customer") },
                TemplateId: string.Empty,
                Parameters: new Dictionary<string, string>(),
                Metadata: new NotificationEventPayloadMetadata("en", "test")
            )
        );

        // Act
        var result = await router.RouteAsync(notificationEvent, notificationEvent.Payload.TargetUsers[0]);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(category, notificationEvent.Payload.NotificationType);
        Assert.Contains("opted out", result.SkipReason);
    }

    [Fact]
    public async Task RouteAsync_SuccessfulDelivery_ReturnsSuccessful()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Maliev.NotificationService.Data.NotificationDbContext>();
        var router = scope.ServiceProvider.GetRequiredService<INotificationRouter>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

        var userId = "success-user";
        var channelType = "email";
        var identifier = "test@example.com";

        // Add channel binding
        var binding = new ChannelBinding
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChannelType = channelType,
            ChannelIdentifier = encryptionService.Encrypt(identifier),
            IsValid = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.ChannelBindings.Add(binding);

        // Add dummy template to avoid fallback to simple rendering
        var template = new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            TemplateKey = "test-template",
            ChannelType = channelType,
            Language = "en",
            ContentTemplate = "Hello {{name}}",
            Parameters = new[] { "name" },
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.NotificationTemplates.Add(template);
        await context.SaveChangesAsync();

        var notificationEvent = new NotificationEvent(
            MessageId: Guid.NewGuid(),
            MessageName: "NotificationEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "TestService",
            ConsumedBy: new[] { "NotificationService" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new NotificationEventPayload(
                NotificationType: "Test",
                Priority: "high",
                TargetUsers: new[] { new NotificationEventPayloadTargetUsersItem(userId, "customer") },
                TemplateId: "test-template",
                Parameters: new Dictionary<string, string> { { "name", "User" } },
                Metadata: new NotificationEventPayloadMetadata("en", "test")
            )
        );

        // Act
        var result = await router.RouteAsync(notificationEvent, notificationEvent.Payload.TargetUsers[0]);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(channelType, result.SelectedChannel);
    }

    [Fact]
    public async Task RouteAsync_NoValidChannelBinding_ReturnsFailed()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var router = scope.ServiceProvider.GetRequiredService<INotificationRouter>();

        var userId = "user-no-binding";
        var notificationEvent = CreateTestEvent(userId);

        // Act
        var result = await router.RouteAsync(notificationEvent, notificationEvent.Payload.TargetUsers[0]);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No valid channel binding", result.ErrorMessage);
    }

    [Fact]
    public async Task RouteAsync_TemplateNotFound_FallsBackToSimpleRendering()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Maliev.NotificationService.Data.NotificationDbContext>();
        var router = scope.ServiceProvider.GetRequiredService<INotificationRouter>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

        var userId = "template-fallback-user";
        var identifier = "test@example.com";

        context.ChannelBindings.Add(new ChannelBinding
        {
            UserId = userId,
            ChannelType = "email",
            ChannelIdentifier = encryptionService.Encrypt(identifier),
            IsValid = true
        });
        await context.SaveChangesAsync();

        var notificationEvent = CreateTestEvent(userId, templateId: "non-existent-template");

        // Act
        var result = await router.RouteAsync(notificationEvent, notificationEvent.Payload.TargetUsers[0]);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("email", result.SelectedChannel);
        // Should succeed with simple rendering
    }

    [Fact]
    public async Task RouteAsync_NoProviderForChannel_ReturnsFailed()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Maliev.NotificationService.Data.NotificationDbContext>();
        var router = scope.ServiceProvider.GetRequiredService<INotificationRouter>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

        var userId = "no-provider-user";

        // Set preference to a channel type with no provider registered
        context.UserNotificationPreferences.Add(new UserNotificationPreference
        {
            UserId = userId,
            PrimaryChannelType = "unknown-channel",
            FallbackChannelTypes = new List<string>(),
            OptOutCategories = new List<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        // Add a binding for "unknown-channel" so it passes the binding check
        context.ChannelBindings.Add(new ChannelBinding
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChannelType = "unknown-channel",
            ChannelIdentifier = encryptionService.Encrypt("some-identifier"),
            IsValid = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var result = await router.RouteAsync(CreateTestEvent(userId), CreateTestEvent(userId).Payload.TargetUsers[0]);

        Assert.False(result.Success);
        Assert.Contains("No provider registered", result.ErrorMessage);
    }

    [Fact]
    public async Task RouteAsync_PrimaryInvalidEmail_FallbackSmsSucceeds()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Maliev.NotificationService.Data.NotificationDbContext>();
        var router = scope.ServiceProvider.GetRequiredService<INotificationRouter>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

        var userId = "fallback-test-user";

        // User preference: primary=email (invalid), fallback=sms (valid)
        context.UserNotificationPreferences.Add(new UserNotificationPreference
        {
            UserId = userId,
            PrimaryChannelType = "email",
            FallbackChannelTypes = new List<string> { "sms" },
            OptOutCategories = new List<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        // Primary binding with invalid email → EmailProvider.ValidateRecipientAsync fails
        context.ChannelBindings.Add(new ChannelBinding
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChannelType = "email",
            ChannelIdentifier = encryptionService.Encrypt("not-valid-email-address"),
            IsValid = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        // Fallback SMS binding with valid phone (simulated mode)
        context.ChannelBindings.Add(new ChannelBinding
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChannelType = "sms",
            ChannelIdentifier = encryptionService.Encrypt("+15551234567"),
            IsValid = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var result = await router.RouteAsync(CreateTestEvent(userId), CreateTestEvent(userId).Payload.TargetUsers[0]);

        Assert.True(result.Success);
        Assert.Equal("sms", result.SelectedChannel);
    }

    [Fact]
    public async Task RouteAsync_AllChannelsFail_ReturnsFailed()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Maliev.NotificationService.Data.NotificationDbContext>();
        var router = scope.ServiceProvider.GetRequiredService<INotificationRouter>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

        var userId = "all-fail-user";

        context.UserNotificationPreferences.Add(new UserNotificationPreference
        {
            UserId = userId,
            PrimaryChannelType = "email",
            FallbackChannelTypes = new List<string> { "sms" },
            OptOutCategories = new List<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        // Both bindings with invalid identifiers
        context.ChannelBindings.Add(new ChannelBinding
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChannelType = "email",
            ChannelIdentifier = encryptionService.Encrypt("not-valid-email"),
            IsValid = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        context.ChannelBindings.Add(new ChannelBinding
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChannelType = "sms",
            ChannelIdentifier = encryptionService.Encrypt("not-valid-phone"),
            IsValid = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var result = await router.RouteAsync(CreateTestEvent(userId), CreateTestEvent(userId).Payload.TargetUsers[0]);

        Assert.False(result.Success);
        Assert.Contains("All channels failed", result.ErrorMessage);
    }

    [Fact]
    public async Task RouteAsync_TemplateRenderingException_ReturnsFailedNotRetryable()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Maliev.NotificationService.Data.NotificationDbContext>();
        var router = scope.ServiceProvider.GetRequiredService<INotificationRouter>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

        var userId = "template-fail-user";
        var templateId = "template-missing-params";

        // Template that requires "name" parameter
        context.NotificationTemplates.Add(new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            TemplateKey = templateId,
            ChannelType = "email",
            Language = "en",
            ContentTemplate = "Hello {{name}}",
            Parameters = new[] { "name" }, // Required parameter
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        context.ChannelBindings.Add(new ChannelBinding
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChannelType = "email",
            ChannelIdentifier = encryptionService.Encrypt("test@example.com"),
            IsValid = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        // Event with no parameters → TemplateRenderingException (missing "name")
        var notificationEvent = new NotificationEvent(
            MessageId: Guid.NewGuid(),
            MessageName: "NotificationEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Test",
            ConsumedBy: new[] { "NotificationService" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new NotificationEventPayload(
                NotificationType: "Test",
                Priority: "critical",
                TargetUsers: new[] { new NotificationEventPayloadTargetUsersItem(userId, "customer") },
                TemplateId: templateId,
                Parameters: new Dictionary<string, string>(), // No "name" param!
                Metadata: new NotificationEventPayloadMetadata("en", "test")
            )
        );

        var result = await router.RouteAsync(notificationEvent, notificationEvent.Payload.TargetUsers[0]);

        Assert.False(result.Success);
        Assert.False(result.IsRetryable);
    }

    [Fact]
    public async Task RouteAsync_ResolveChannelProvider_UnknownChannel_ReturnsNull()
    {
        using var scope = _factory.Services.CreateScope();
        var router = (NotificationRouter)scope.ServiceProvider.GetRequiredService<INotificationRouter>();

        var provider = router.ResolveChannelProvider("non-existent-channel");
        Assert.Null(provider);
    }

    private NotificationEvent CreateTestEvent(string userId, string type = "Test", string templateId = "")
    {
        return new NotificationEvent(
            MessageId: Guid.NewGuid(),
            MessageName: "NotificationEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "Test",
            ConsumedBy: new[] { "NotificationService" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new NotificationEventPayload(
                NotificationType: type,
                Priority: "critical",
                TargetUsers: new[] { new NotificationEventPayloadTargetUsersItem(userId, "customer") },
                TemplateId: templateId,
                Parameters: new Dictionary<string, string>(),
                Metadata: new NotificationEventPayloadMetadata("en", "test")
            )
        );
    }
}
