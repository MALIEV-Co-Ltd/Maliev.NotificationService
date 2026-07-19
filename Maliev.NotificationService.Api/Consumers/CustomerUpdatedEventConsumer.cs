using Maliev.MessagingContracts.Contracts.Customers;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Api.Models.Enums;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Api.Services.External;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Maliev.NotificationService.Api.Consumers;

/// <summary>
/// Consumes the CustomerUpdatedEvent to update notification channel bindings.
/// </summary>
public class CustomerUpdatedEventConsumer : IConsumer<CustomerUpdatedEvent>
{
    private readonly NotificationDbContext _context;
    private readonly ICustomerServiceClient _customerServiceClient;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<CustomerUpdatedEventConsumer> _logger;

    public CustomerUpdatedEventConsumer(
        NotificationDbContext context,
        ICustomerServiceClient customerServiceClient,
        IEncryptionService encryptionService,
        ILogger<CustomerUpdatedEventConsumer> logger)
    {
        _context = context;
        _customerServiceClient = customerServiceClient;
        _encryptionService = encryptionService;
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
                    var currentEmail = DecryptOrReturnStoredValue(emailBinding.ChannelIdentifier);
                    if (!string.Equals(currentEmail, newEmail, StringComparison.Ordinal))
                    {
                        emailBinding.ChannelIdentifier = _encryptionService.Encrypt(newEmail);
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
                    var currentMobile = DecryptOrReturnStoredValue(smsBinding.ChannelIdentifier);
                    if (!string.Equals(currentMobile, newMobile, StringComparison.Ordinal))
                    {
                        smsBinding.ChannelIdentifier = _encryptionService.Encrypt(newMobile);
                        smsBinding.IsValid = false;
                    }
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    private string DecryptOrReturnStoredValue(string storedValue)
    {
        try
        {
            return _encryptionService.Decrypt(storedValue);
        }
        catch (InvalidOperationException)
        {
            return storedValue;
        }
        catch (FormatException)
        {
            return storedValue;
        }
    }
}
