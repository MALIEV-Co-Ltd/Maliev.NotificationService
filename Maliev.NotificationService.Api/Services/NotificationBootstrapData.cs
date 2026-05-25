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
            Version = 2,
            Language = "en",
            ChannelType = "email",
            SubjectTemplate = "New website contact: {{subject}}",
            ContentTemplate =
                "A new website contact request is waiting in Maliev.Intranet.\n\n" +
                "Reference: #{{contactId}}\n" +
                "Name: {{fullName}}\n" +
                "Email: {{email}}\n" +
                "Phone: {{phoneNumber}}\n" +
                "Company: {{company}}\n" +
                "Type: {{contactType}}\n" +
                "Priority: {{priority}}\n" +
                "Attachments: {{attachmentCount}} {{attachmentNames}}\n\n" +
                "Subject: {{subject}}\n\n" +
                "Open this request in Maliev.Intranet to review the message, attachments, lifecycle status, and follow-up actions. Do not reply from this notification email.",
            Parameters =
            [
                "contactId",
                "fullName",
                "email",
                "phoneNumber",
                "company",
                "contactType",
                "priority",
                "attachmentCount",
                "attachmentNames",
                "subject"
            ]
        };
    }

    /// <summary>
    /// Creates the email template sent back to the customer after a website contact submission.
    /// </summary>
    /// <returns>The default customer copy email template.</returns>
    public static NotificationTemplate CreateContactMessageCustomerCopyEmailTemplate()
    {
        return new NotificationTemplate
        {
            TemplateKey = "contact-message-customer-copy",
            DisplayName = "Website contact customer copy",
            Version = 1,
            Language = "en",
            ChannelType = "email",
            SubjectTemplate = "MALIEV received your message #{{contactId}}",
            ContentTemplate =
                "Hello {{recipientName}},\n\n" +
                "We received your message to MALIEV. Keep this email as your copy of the request.\n\n" +
                "Reference: #{{contactId}}\n" +
                "Subject: {{subject}}\n" +
                "Attachments: {{attachmentCount}} {{attachmentNames}}\n\n" +
                "Your message:\n{{message}}\n\n" +
                "Our team will review it in Maliev.Intranet and respond from there.",
            Parameters =
            [
                "recipientEmail",
                "recipientName",
                "contactId",
                "subject",
                "attachmentCount",
                "attachmentNames",
                "message"
            ]
        };
    }

    /// <summary>
    /// Creates the email template employees use when replying to contact requests from Maliev.Intranet.
    /// </summary>
    /// <returns>The default employee reply email template.</returns>
    public static NotificationTemplate CreateContactMessageEmployeeReplyEmailTemplate()
    {
        return new NotificationTemplate
        {
            TemplateKey = "contact-message-employee-reply",
            DisplayName = "Website contact employee reply",
            Version = 1,
            Language = "en",
            ChannelType = "email",
            SubjectTemplate = "MALIEV response for request #{{contactId}}",
            ContentTemplate =
                "Hello {{recipientName}},\n\n" +
                "{{replyMessage}}\n\n" +
                "Reference: #{{contactId}}\n" +
                "Subject: {{subject}}\n\n" +
                "Best regards,\nMALIEV",
            Parameters =
            [
                "recipientEmail",
                "recipientName",
                "contactId",
                "subject",
                "replyMessage"
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
