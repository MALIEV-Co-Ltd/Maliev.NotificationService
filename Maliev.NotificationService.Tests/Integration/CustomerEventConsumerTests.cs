using Maliev.MessagingContracts.Contracts.Customers;
using Maliev.MessagingContracts.Generated;
using Maliev.NotificationService.Api.Consumers;
using Maliev.NotificationService.Data;
using Maliev.NotificationService.Tests.Testing;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Integration;

[Collection("Integration")]
public class CustomerEventConsumerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public CustomerEventConsumerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _factory.CleanDatabaseAsync();
    }

    #region CustomerCreatedEventConsumer Tests

    [Fact]
    public async Task CustomerCreated_WithEmailAndMobile_CreatesPreferenceAndBindings()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CustomerCreatedEventConsumer>>();

        var consumer = new CustomerCreatedEventConsumer(context, logger);

        var customerId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var evt = CreateCustomerCreatedEvent(customerId, principalId, "test@example.com", "+15551234567");

        var mockContext = new Mock<ConsumeContext<CustomerCreatedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);

        await consumer.Consume(mockContext.Object);

        var expectedUserId = principalId.ToString();
        var pref = await context.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == expectedUserId);
        Assert.NotNull(pref);
        Assert.Equal("email", pref.PrimaryChannelType);
        Assert.Contains("sms", pref.FallbackChannelTypes);

        var emailBinding = await context.ChannelBindings
            .FirstOrDefaultAsync(b => b.UserId == expectedUserId && b.ChannelType == "email");
        Assert.NotNull(emailBinding);

        var smsBinding = await context.ChannelBindings
            .FirstOrDefaultAsync(b => b.UserId == expectedUserId && b.ChannelType == "sms");
        Assert.NotNull(smsBinding);
    }

    [Fact]
    public async Task CustomerCreated_WithEmailOnly_CreatesPreferenceAndEmailBinding()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CustomerCreatedEventConsumer>>();

        var consumer = new CustomerCreatedEventConsumer(context, logger);

        var customerId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var evt = CreateCustomerCreatedEvent(customerId, principalId, "test@example.com", null);

        var mockContext = new Mock<ConsumeContext<CustomerCreatedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);

        await consumer.Consume(mockContext.Object);

        var expectedUserId = principalId.ToString();
        var pref = await context.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == expectedUserId);
        Assert.NotNull(pref);

        var emailBinding = await context.ChannelBindings
            .FirstOrDefaultAsync(b => b.UserId == expectedUserId && b.ChannelType == "email");
        Assert.NotNull(emailBinding);

        var smsBinding = await context.ChannelBindings
            .FirstOrDefaultAsync(b => b.UserId == expectedUserId && b.ChannelType == "sms");
        Assert.Null(smsBinding);
    }

    [Fact]
    public async Task CustomerCreated_WithEmptyPrincipalId_UsesCustomerIdAsIdentifier()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CustomerCreatedEventConsumer>>();

        var consumer = new CustomerCreatedEventConsumer(context, logger);

        var customerId = Guid.NewGuid();
        var evt = CreateCustomerCreatedEvent(customerId, Guid.Empty, "user@example.com", null);

        var mockContext = new Mock<ConsumeContext<CustomerCreatedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);

        await consumer.Consume(mockContext.Object);

        // When PrincipalId is Guid.Empty, should use customerId as userId
        var pref = await context.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == customerId.ToString());
        Assert.NotNull(pref);
    }

    [Fact]
    public async Task CustomerCreated_PreferenceAlreadyExists_DoesNotDuplicate()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CustomerCreatedEventConsumer>>();

        var consumer = new CustomerCreatedEventConsumer(context, logger);

        var customerId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var userId = principalId.ToString();

        // Pre-create preference
        context.UserNotificationPreferences.Add(new Data.Entities.UserNotificationPreference
        {
            UserId = userId,
            PrimaryChannelType = "sms",
            FallbackChannelTypes = new List<string>(),
            OptOutCategories = new List<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var evt = CreateCustomerCreatedEvent(customerId, principalId, "test@example.com", null);
        var mockContext = new Mock<ConsumeContext<CustomerCreatedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);

        await consumer.Consume(mockContext.Object);

        // Preference should not be changed (still sms primary)
        var prefs = await context.UserNotificationPreferences
            .Where(p => p.UserId == userId).ToListAsync();
        Assert.Single(prefs);
        Assert.Equal("sms", prefs[0].PrimaryChannelType);
    }

    [Fact]
    public async Task CustomerCreated_EmailBindingAlreadyExists_DoesNotDuplicate()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CustomerCreatedEventConsumer>>();

        var consumer = new CustomerCreatedEventConsumer(context, logger);

        var customerId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var userId = principalId.ToString();

        // Pre-create email binding
        context.ChannelBindings.Add(new Data.Entities.ChannelBinding
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChannelType = "email",
            ChannelIdentifier = "old@example.com",
            IsValid = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var evt = CreateCustomerCreatedEvent(customerId, principalId, "new@example.com", null);
        var mockContext = new Mock<ConsumeContext<CustomerCreatedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);

        await consumer.Consume(mockContext.Object);

        // Should still only be one email binding (not a new one)
        var bindings = await context.ChannelBindings
            .Where(b => b.UserId == userId && b.ChannelType == "email").ToListAsync();
        Assert.Single(bindings);
    }

    #endregion

    #region CustomerUpdatedEventConsumer Tests

    [Fact]
    public async Task CustomerUpdated_EmailChanged_UpdatesEmailBinding()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CustomerUpdatedEventConsumer>>();

        var consumer = new CustomerUpdatedEventConsumer(context, logger);

        var customerId = Guid.NewGuid();
        var userId = customerId.ToString();

        context.ChannelBindings.Add(new Data.Entities.ChannelBinding
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChannelType = "email",
            ChannelIdentifier = "old@example.com",
            IsValid = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var updatedFields = JsonDocument.Parse("{\"email\":\"new@example.com\"}").RootElement;
        var evt = CreateCustomerUpdatedEvent(customerId, updatedFields);
        var mockContext = new Mock<ConsumeContext<CustomerUpdatedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);

        await consumer.Consume(mockContext.Object);

        var binding = await context.ChannelBindings
            .FirstOrDefaultAsync(b => b.UserId == userId && b.ChannelType == "email");
        Assert.NotNull(binding);
        Assert.Equal("new@example.com", binding.ChannelIdentifier);
        Assert.False(binding.IsValid);
    }

    [Fact]
    public async Task CustomerUpdated_MobileChanged_UpdatesSmsBinding()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CustomerUpdatedEventConsumer>>();

        var consumer = new CustomerUpdatedEventConsumer(context, logger);

        var customerId = Guid.NewGuid();
        var userId = customerId.ToString();

        context.ChannelBindings.Add(new Data.Entities.ChannelBinding
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChannelType = "sms",
            ChannelIdentifier = "+11111111111",
            IsValid = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var updatedFields = JsonDocument.Parse("{\"mobile\":\"+15559876543\"}").RootElement;
        var evt = CreateCustomerUpdatedEvent(customerId, updatedFields);
        var mockContext = new Mock<ConsumeContext<CustomerUpdatedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);

        await consumer.Consume(mockContext.Object);

        var binding = await context.ChannelBindings
            .FirstOrDefaultAsync(b => b.UserId == userId && b.ChannelType == "sms");
        Assert.NotNull(binding);
        Assert.Equal("+15559876543", binding.ChannelIdentifier);
        Assert.False(binding.IsValid);
    }

    [Fact]
    public async Task CustomerUpdated_InvalidCastUpdatedFields_ReturnsEarly()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CustomerUpdatedEventConsumer>>();

        var consumer = new CustomerUpdatedEventConsumer(context, logger);

        var customerId = Guid.NewGuid();
        // Use a non-JsonElement type for UpdatedFields to trigger InvalidCastException
        var payload = new CustomerUpdatedEventPayload(
            CustomerId: customerId,
            UpdatedFields: "not-a-json-element",
            UpdatedBy: "test",
            ActorType: "Customer",
            UpdatedAt: DateTimeOffset.UtcNow
        );

        var evt = new CustomerUpdatedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: "CustomerUpdatedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "TestService",
            ConsumedBy: new[] { "NotificationService" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: payload
        );

        var mockContext = new Mock<ConsumeContext<CustomerUpdatedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);

        // Should not throw - just returns early
        await consumer.Consume(mockContext.Object);
    }

    [Fact]
    public async Task CustomerUpdated_NoExistingBinding_DoesNotCreateNew()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CustomerUpdatedEventConsumer>>();

        var consumer = new CustomerUpdatedEventConsumer(context, logger);

        var customerId = Guid.NewGuid();
        var updatedFields = JsonDocument.Parse("{\"email\":\"new@example.com\"}").RootElement;
        var evt = CreateCustomerUpdatedEvent(customerId, updatedFields);
        var mockContext = new Mock<ConsumeContext<CustomerUpdatedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);

        await consumer.Consume(mockContext.Object);

        var count = await context.ChannelBindings.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CustomerUpdated_SameEmailValue_DoesNotMarkInvalid()
    {
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CustomerUpdatedEventConsumer>>();

        var consumer = new CustomerUpdatedEventConsumer(context, logger);

        var customerId = Guid.NewGuid();
        var userId = customerId.ToString();
        var sameEmail = "same@example.com";

        context.ChannelBindings.Add(new Data.Entities.ChannelBinding
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChannelType = "email",
            ChannelIdentifier = sameEmail,
            IsValid = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var updatedFields = JsonDocument.Parse($"{{\"email\":\"{sameEmail}\"}}").RootElement;
        var evt = CreateCustomerUpdatedEvent(customerId, updatedFields);
        var mockContext = new Mock<ConsumeContext<CustomerUpdatedEvent>>();
        mockContext.Setup(m => m.Message).Returns(evt);

        await consumer.Consume(mockContext.Object);

        var binding = await context.ChannelBindings
            .FirstOrDefaultAsync(b => b.UserId == userId && b.ChannelType == "email");
        Assert.NotNull(binding);
        Assert.True(binding.IsValid); // Not invalidated since value didn't change
    }

    #endregion

    private static CustomerCreatedEvent CreateCustomerCreatedEvent(
        Guid customerId, Guid principalId, string email, string? mobile)
    {
        return new CustomerCreatedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: "CustomerCreatedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "TestService",
            ConsumedBy: new[] { "NotificationService" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new CustomerCreatedEventPayload(
                CustomerId: customerId,
                PrincipalId: principalId,
                FirstName: "Test",
                LastName: "User",
                Email: email,
                Mobile: mobile,
                Extension: null,
                Landline: null,
                Segment: "Retail",
                Tier: "Bronze",
                CompanyId: null,
                CreatedAt: DateTimeOffset.UtcNow
            )
        );
    }

    private static CustomerUpdatedEvent CreateCustomerUpdatedEvent(
        Guid customerId, JsonElement updatedFields)
    {
        return new CustomerUpdatedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: "CustomerUpdatedEvent",
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "TestService",
            ConsumedBy: new[] { "NotificationService" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new CustomerUpdatedEventPayload(
                CustomerId: customerId,
                UpdatedFields: updatedFields,
                UpdatedBy: "test",
                ActorType: "Customer",
                UpdatedAt: DateTimeOffset.UtcNow
            )
        );
    }
}
