using Maliev.NotificationService.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Provides default notification bootstrap data required for production workflows.
/// </summary>
public static class NotificationBootstrapData
{
    /// <summary>
    /// Stable notification user id for website contact form submissions.
    /// </summary>
    public const string ContactInboxUserId = "maliev-contact-inbox";

    /// <summary>
    /// Configuration key for the website contact inbox email recipient.
    /// </summary>
    public const string ContactInboxEmailConfigurationKey = "Notification:ContactInbox:Email";

    /// <summary>
    /// Public fallback inbox address used when no deployment-specific recipient is configured.
    /// </summary>
    public const string DefaultContactInboxEmail = "info@maliev.com";

    /// <summary>
    /// Creates the default email template used for website contact form submissions.
    /// </summary>
    /// <returns>The default contact message submitted email template.</returns>
    public static NotificationTemplate CreateContactMessageSubmittedEmailTemplate()
    {
        return new NotificationTemplate
        {
            TemplateKey = "contact-message-submitted",
            DisplayName = "Website contact message",
            Version = 1,
            Language = "en",
            ChannelType = "email",
            SubjectTemplate = "New website contact: {{subject}}",
            ContentTemplate =
                "A new website contact message was submitted.\n\n" +
                "Reference: #{{contactId}}\n" +
                "Name: {{fullName}}\n" +
                "Email: {{email}}\n" +
                "Phone: {{phoneNumber}}\n" +
                "Company: {{company}}\n" +
                "Country: {{countryId}}\n" +
                "Type: {{contactType}}\n" +
                "Priority: {{priority}}\n" +
                "Attachments: {{attachmentCount}} {{attachmentNames}}\n\n" +
                "Subject: {{subject}}\n\n" +
                "Message:\n{{message}}",
            Parameters =
            [
                "contactId",
                "fullName",
                "email",
                "phoneNumber",
                "company",
                "countryId",
                "contactType",
                "priority",
                "attachmentCount",
                "attachmentNames",
                "subject",
                "message"
            ]
        };
    }

    /// <summary>
    /// Resolves the contact inbox email recipient from configuration or the public fallback address.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The contact inbox email recipient.</returns>
    public static string ResolveContactInboxEmail(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration[ContactInboxEmailConfigurationKey] ?? DefaultContactInboxEmail;
    }

    /// <summary>
    /// Creates the default contact inbox email channel binding.
    /// </summary>
    /// <param name="encryptedEmail">Encrypted email address for the channel binding.</param>
    /// <returns>The default contact inbox email binding.</returns>
    public static ChannelBinding CreateContactInboxEmailBinding(string encryptedEmail)
    {
        return new ChannelBinding
        {
            UserId = ContactInboxUserId,
            ChannelType = "email",
            ChannelIdentifier = encryptedEmail,
            IsValid = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Creates the default notification preference for the contact inbox user.
    /// </summary>
    /// <returns>The default contact inbox preference.</returns>
    public static UserNotificationPreference CreateContactInboxPreference()
    {
        return new UserNotificationPreference
        {
            UserId = ContactInboxUserId,
            PrimaryChannelType = "email",
            FallbackChannelTypes = [],
            OptOutCategories = [],
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
