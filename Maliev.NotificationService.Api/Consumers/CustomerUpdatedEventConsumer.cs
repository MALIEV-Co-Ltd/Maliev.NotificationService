using Maliev.MessagingContracts.Contracts.Customers;
using Maliev.MessagingContracts.Generated;
using Maliev.NotificationService.Api.Models.Enums;
using Maliev.NotificationService.Data.Entities;
using Maliev.NotificationService.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Maliev.NotificationService.Api.Services.External;

namespace Maliev.NotificationService.Api.Consumers;

/// <summary>
/// Consumes the CustomerUpdatedEvent to update notification channel bindings.
/// </summary>
public class CustomerUpdatedEventConsumer : IConsumer<CustomerUpdatedEvent>
{
    private readonly NotificationDbContext _context;
    private readonly ICustomerServiceClient _customerServiceClient;
    private readonly ILogger<CustomerUpdatedEventConsumer> _logger;

    public CustomerUpdatedEventConsumer(
        NotificationDbContext context,
        ICustomerServiceClient customerServiceClient,
        ILogger<CustomerUpdatedEventConsumer> logger)
    {
        _context = context;
        _customerServiceClient = customerServiceClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CustomerUpdatedEvent> context)
    {
        var message = context.Message.Payload;
        var customerId = message.CustomerId.ToString();

        // Note: Ideally we would also check PrincipalId, but it's not in the event payload.
        // We try to find bindings by CustomerId (which is used as UserId if PrincipalId was missing).
        // If the user has a PrincipalId, we might miss this update unless we can map CustomerId -> PrincipalId.
        // For now, we attempt update based on CustomerId.

        _logger.LogInformation("Updating notification settings for customer {CustomerId}", message.CustomerId);

        JsonElement updatedFields;
        try
        {
            updatedFields = (JsonElement)message.UpdatedFields;
        }
        catch (InvalidCastException)
        {
            // Might be a different type in tests or if serialization changes
            _logger.LogWarning("Could not cast UpdatedFields to JsonElement");
            return;
        }

        // Try to find the PrincipalId from the Customer Service to ensure bindings are updated
        var customerDto = await _customerServiceClient.GetCustomerByIdAsync(message.CustomerId, context.CancellationToken);
        var principalId = customerDto?.PrincipalId.ToString();

        // Update Email Binding
        if (updatedFields.TryGetProperty("email", out var emailElement) && emailElement.ValueKind == JsonValueKind.String)
        {
            var newEmail = emailElement.GetString();
            if (!string.IsNullOrWhiteSpace(newEmail))
            {
                var emailBinding = await _context.ChannelBindings
                    .FirstOrDefaultAsync(b => (b.UserId == customerId || (principalId != null && b.UserId == principalId))
                                           && b.ChannelType == ChannelType.Email.ToString().ToLowerInvariant());

                if (emailBinding != null)
                {
                    if (emailBinding.ChannelIdentifier != newEmail)
                    {
                        emailBinding.ChannelIdentifier = newEmail;
                        emailBinding.IsValid = false; // Re-verification needed
                    }
                }
            }
        }

        // Update Mobile/SMS Binding
        // "mobile" is the property name in CustomerCreated, assuming same in UpdatedFields
        if (updatedFields.TryGetProperty("mobile", out var mobileElement) && mobileElement.ValueKind == JsonValueKind.String)
        {
            var newMobile = mobileElement.GetString();
            if (!string.IsNullOrWhiteSpace(newMobile))
            {
                var smsBinding = await _context.ChannelBindings
                    .FirstOrDefaultAsync(b => (b.UserId == customerId || (principalId != null && b.UserId == principalId))
                                           && b.ChannelType == ChannelType.Sms.ToString().ToLowerInvariant());

                if (smsBinding != null)
                {
                    if (smsBinding.ChannelIdentifier != newMobile)
                    {
                        smsBinding.ChannelIdentifier = newMobile;
                        smsBinding.IsValid = false;
                    }
                }
            }
        }

        await _context.SaveChangesAsync();
    }
}
