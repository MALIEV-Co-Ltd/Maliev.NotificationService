using Maliev.MessagingContracts.Contracts.Customers;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Api.Models.Enums;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.NotificationService.Api.Consumers;

/// <summary>
/// Consumes the CustomerCreatedEvent to auto-provision notification settings.
/// </summary>
public class CustomerCreatedEventConsumer : IConsumer<CustomerCreatedEvent>
{
    private readonly NotificationDbContext _context;
    private readonly ILogger<CustomerCreatedEventConsumer> _logger;

    public CustomerCreatedEventConsumer(NotificationDbContext context, ILogger<CustomerCreatedEventConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CustomerCreatedEvent> context)
    {
        var message = context.Message.Payload;
        var customerId = message.CustomerId;
        var principalId = message.PrincipalId; // This is a Guid in the contract

        // Use PrincipalId if available (for login users), otherwise CustomerId (for system messages)
        // The NotificationService uses PrincipalId for preferences.

        // If PrincipalId is Guid.Empty (default), treat as null/missing
        var userIdentifier = principalId != Guid.Empty ? principalId.ToString() : customerId.ToString();

        _logger.LogInformation("Provisioning notification settings for customer {CustomerId} (User: {UserIdentifier})", customerId, userIdentifier);

        // Check if preferences already exist
        var existingPref = await _context.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userIdentifier);

        if (existingPref == null)
        {
            var pref = new UserNotificationPreference
            {
                UserId = userIdentifier,
                PrimaryChannelType = ChannelType.Email.ToString().ToLowerInvariant(),
                FallbackChannelTypes = [ChannelType.Sms.ToString().ToLowerInvariant()],
                OptOutCategories = [], // No opt-outs by default
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.UserNotificationPreferences.Add(pref);
        }

        // Provision Email Binding
        if (!string.IsNullOrWhiteSpace(message.Email))
        {
            var existingEmail = await _context.ChannelBindings
                .FirstOrDefaultAsync(b => b.UserId == userIdentifier && b.ChannelType == ChannelType.Email.ToString().ToLowerInvariant());

            if (existingEmail == null)
            {
                _context.ChannelBindings.Add(new ChannelBinding
                {
                    Id = Guid.NewGuid(),
                    UserId = userIdentifier,
                    ChannelType = ChannelType.Email.ToString().ToLowerInvariant(),
                    ChannelIdentifier = message.Email, // Will be encrypted by EF Core interceptor
                    IsValid = true, // Auto-valid for now
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        // Provision SMS Binding
        if (!string.IsNullOrWhiteSpace(message.Mobile))
        {
            var existingSms = await _context.ChannelBindings
                .FirstOrDefaultAsync(b => b.UserId == userIdentifier && b.ChannelType == ChannelType.Sms.ToString().ToLowerInvariant());

            if (existingSms == null)
            {
                _context.ChannelBindings.Add(new ChannelBinding
                {
                    Id = Guid.NewGuid(),
                    UserId = userIdentifier,
                    ChannelType = ChannelType.Sms.ToString().ToLowerInvariant(),
                    ChannelIdentifier = message.Mobile, // Will be encrypted
                    IsValid = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully provisioned notification settings for {UserIdentifier}", userIdentifier);
    }
}
