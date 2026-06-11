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
    /// Stable notification user id for employee operations notifications.
    /// </summary>
    public const string OperationsInboxUserId = "maliev-operations-inbox";

    /// <summary>
    /// Configuration key for the website contact inbox email recipient.
    /// </summary>
    public const string ContactInboxEmailConfigurationKey = "Notification:ContactInbox:Email";

    /// <summary>
    /// Configuration key for the employee operations inbox email recipient.
    /// </summary>
    public const string OperationsInboxEmailConfigurationKey = "Notification:OperationsInbox:Email";

    /// <summary>
    /// Public fallback inbox address used when no deployment-specific recipient is configured.
    /// </summary>
    public const string DefaultContactInboxEmail = "info@maliev.com";

    /// <summary>
    /// Public fallback operations address used when no deployment-specific recipient is configured.
    /// </summary>
    public const string DefaultOperationsInboxEmail = "operations@maliev.com";

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
    /// Creates the email template sent to operations when a customer payment is received.
    /// </summary>
    /// <returns>The default operations payment received email template.</returns>
    public static NotificationTemplate CreateOperationsPaymentReceivedEmailTemplate()
    {
        return new NotificationTemplate
        {
            TemplateKey = "operations-payment-received",
            DisplayName = "Operations payment received",
            Version = 1,
            Language = "en",
            ChannelType = "email",
            SubjectTemplate = "Payment received for {{orderId}}",
            ContentTemplate =
                "A customer payment has been received and the order is ready for production queue review.\n\n" +
                "Order: {{orderId}}\n" +
                "Amount: {{amount}}\n" +
                "Payment ID: {{paymentId}}\n" +
                "Customer ID: {{customerId}}\n\n" +
                "Open Maliev.Intranet to confirm material locks, production planning, job tickets, and employee assignments.",
            Parameters =
            [
                "orderId",
                "amount",
                "paymentId",
                "customerId"
            ]
        };
    }

    /// <summary>
    /// Creates the email template sent to operations when a production job is ready for QC intake.
    /// </summary>
    /// <returns>The default operations job completed QC-ready email template.</returns>
    public static NotificationTemplate CreateOperationsJobCompletedQcReadyEmailTemplate()
    {
        return new NotificationTemplate
        {
            TemplateKey = "operations-job-completed-qc-ready",
            DisplayName = "Operations job completed QC ready",
            Version = 1,
            Language = "en",
            ChannelType = "email",
            SubjectTemplate = "Job {{jobId}} is ready for QC intake",
            ContentTemplate =
                "A production job has been completed and is ready for QC intake.\n\n" +
                "Job: {{jobId}}\n" +
                "Order: {{orderId}}\n" +
                "Technology: {{technology}}\n" +
                "Machine: {{assignedMachineId}}\n" +
                "Completed at: {{completedAt}}\n" +
                "Completed by: {{changedBy}}\n\n" +
                "Open Maliev.Intranet to run QC, release accepted parts, or route issues back to production.",
            Parameters =
            [
                "jobId",
                "orderId",
                "technology",
                "assignedMachineId",
                "completedAt",
                "changedBy"
            ]
        };
    }

    /// <summary>
    /// Creates the email template sent to customers when production has completed successfully.
    /// </summary>
    /// <returns>The default customer order completed email template.</returns>
    public static NotificationTemplate CreateCustomerOrderCompletedEmailTemplate()
    {
        return new NotificationTemplate
        {
            TemplateKey = "order-completed",
            DisplayName = "Customer order completed",
            Version = 1,
            Language = "en",
            ChannelType = "email",
            SubjectTemplate = "Your MALIEV order {{orderId}} is complete",
            ContentTemplate =
                "Hello {{name}},\n\n" +
                "Production for your MALIEV order {{orderId}} is complete. The order is moving to quality control and shipping preparation.\n\n" +
                "Completed at: {{completedAt}}\n\n" +
                "We will send another update when the shipment is released.",
            Parameters =
            [
                "name",
                "orderId",
                "completedAt"
            ]
        };
    }

    /// <summary>
    /// Creates the email template sent to customers when production completion reports a failure.
    /// </summary>
    /// <returns>The default customer order completion failed email template.</returns>
    public static NotificationTemplate CreateCustomerOrderCompletionFailedEmailTemplate()
    {
        return new NotificationTemplate
        {
            TemplateKey = "order-completion-failed",
            DisplayName = "Customer order completion issue",
            Version = 1,
            Language = "en",
            ChannelType = "email",
            SubjectTemplate = "Update on MALIEV order {{orderId}}",
            ContentTemplate =
                "Hello {{name}},\n\n" +
                "We found a production issue while completing MALIEV order {{orderId}}. Our team is reviewing the order and will follow up with the next useful action.\n\n" +
                "Updated at: {{completedAt}}\n\n" +
                "You do not need to take action until our team contacts you.",
            Parameters =
            [
                "name",
                "orderId",
                "completedAt"
            ]
        };
    }

    /// <summary>
    /// Creates the email template for Google SSO customer welcome.
    /// </summary>
    /// <returns>The customer welcome Google email template.</returns>
    public static NotificationTemplate CreateCustomerWelcomeGoogleEmailTemplate()
    {
        return new NotificationTemplate
        {
            TemplateKey = "customer-welcome-google",
            DisplayName = "Customer welcome (Google SSO)",
            Version = 1,
            Language = "en",
            ChannelType = "email",
            SubjectTemplate = "Welcome to MALIEV, {{firstName}}!",
            ContentTemplate =
                "Hello {{firstName}},\n\n" +
                "Welcome to MALIEV! Your account has been created successfully using Google SSO.\n\n" +
                "You can now explore our platform, request quotes, and manage your orders.\n\n" +
                "Best regards,\nThe MALIEV Team",
            Parameters =
            [
                "firstName",
                "recipientEmail"
            ]
        };
    }

    /// <summary>
    /// Creates the email template for email/password customer registration (with verification).
    /// </summary>
    /// <returns>The customer welcome email template.</returns>
    public static NotificationTemplate CreateCustomerWelcomeEmailEmailTemplate()
    {
        return new NotificationTemplate
        {
            TemplateKey = "customer-welcome-email",
            DisplayName = "Customer welcome (email verification)",
            Version = 1,
            Language = "en",
            ChannelType = "email",
            SubjectTemplate = "Welcome to MALIEV, {{firstName}}! Verify your email",
            ContentTemplate =
                "Hello {{firstName}},\n\n" +
                "Welcome to MALIEV! Please verify your email address by clicking the link below:\n\n" +
                "{{verificationUrl}}\n\n" +
                "This link will expire shortly. If you did not create this account, please ignore this email.\n\n" +
                "Best regards,\nThe MALIEV Team",
            Parameters =
            [
                "firstName",
                "verificationUrl",
                "recipientEmail"
            ]
        };
    }

    /// <summary>
    /// Creates the email template for email verified confirmation.
    /// </summary>
    /// <returns>The email verified confirmation template.</returns>
    public static NotificationTemplate CreateCustomerEmailVerifiedEmailTemplate()
    {
        return new NotificationTemplate
        {
            TemplateKey = "customer-email-verified",
            DisplayName = "Email verified confirmation",
            Version = 1,
            Language = "en",
            ChannelType = "email",
            SubjectTemplate = "Your email has been verified",
            ContentTemplate =
                "Hello {{firstName}},\n\n" +
                "Your email address has been successfully verified.\n\n" +
                "You now have full access to your MALIEV account.\n\n" +
                "Best regards,\nThe MALIEV Team",
            Parameters =
            [
                "firstName",
                "recipientEmail"
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
    /// Resolves the operations inbox email recipient from configuration or the public fallback address.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The operations inbox email recipient.</returns>
    public static string ResolveOperationsInboxEmail(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration[OperationsInboxEmailConfigurationKey] ?? DefaultOperationsInboxEmail;
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
    /// Creates the default operations inbox email channel binding.
    /// </summary>
    /// <param name="encryptedEmail">Encrypted email address for the channel binding.</param>
    /// <returns>The default operations inbox email binding.</returns>
    public static ChannelBinding CreateOperationsInboxEmailBinding(string encryptedEmail)
    {
        return new ChannelBinding
        {
            UserId = OperationsInboxUserId,
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

    /// <summary>
    /// Creates the default notification preference for the operations inbox user.
    /// </summary>
    /// <returns>The default operations inbox preference.</returns>
    public static UserNotificationPreference CreateOperationsInboxPreference()
    {
        return new UserNotificationPreference
        {
            UserId = OperationsInboxUserId,
            PrimaryChannelType = "email",
            FallbackChannelTypes = [],
            OptOutCategories = [],
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
