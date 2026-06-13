using Maliev.NotificationService.Api.Services;
using Microsoft.Extensions.Configuration;

namespace Maliev.NotificationService.Tests.Unit.Services;

public sealed class NotificationBootstrapDataTests
{
    [Fact]
    public void CreateContactMessageSubmittedEmailTemplate_DefinesContactInboxTemplate()
    {
        var template = NotificationBootstrapData.CreateContactMessageSubmittedEmailTemplate();

        Assert.Equal("contact-message-submitted", template.TemplateKey);
        Assert.Equal(2, template.Version);
        Assert.Equal("email", template.ChannelType);
        Assert.Equal("en", template.Language);
        Assert.Equal("New website contact: {{subject}}", template.SubjectTemplate);
        Assert.Contains("{{fullName}}", template.ContentTemplate, StringComparison.Ordinal);
        Assert.Contains("{{email}}", template.ContentTemplate, StringComparison.Ordinal);
        Assert.Contains("Open this request in Maliev.Intranet", template.ContentTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain("{{message}}", template.ContentTemplate, StringComparison.Ordinal);
        Assert.Contains("contactId", template.Parameters);
        Assert.Contains("fullName", template.Parameters);
        Assert.Contains("email", template.Parameters);
        Assert.Contains("subject", template.Parameters);
        Assert.DoesNotContain("message", template.Parameters);
        Assert.Contains("attachmentCount", template.Parameters);
        Assert.Contains("attachmentNames", template.Parameters);
    }

    [Fact]
    public void CreateContactMessageCustomerCopyEmailTemplate_IncludesCustomerRequestCopy()
    {
        var template = NotificationBootstrapData.CreateContactMessageCustomerCopyEmailTemplate();

        Assert.Equal("contact-message-customer-copy", template.TemplateKey);
        Assert.Equal("email", template.ChannelType);
        Assert.Equal("MALIEV received your message #{{contactId}}", template.SubjectTemplate);
        Assert.Contains("{{message}}", template.ContentTemplate, StringComparison.Ordinal);
        Assert.Contains("{{attachmentNames}}", template.ContentTemplate, StringComparison.Ordinal);
        Assert.Contains("message", template.Parameters);
        Assert.Contains("recipientEmail", template.Parameters);
        Assert.Contains("recipientName", template.Parameters);
    }

    [Fact]
    public void CreateContactMessageEmployeeReplyEmailTemplate_IncludesReplyBody()
    {
        var template = NotificationBootstrapData.CreateContactMessageEmployeeReplyEmailTemplate();

        Assert.Equal("contact-message-employee-reply", template.TemplateKey);
        Assert.Equal("email", template.ChannelType);
        Assert.Equal("MALIEV response for request #{{contactId}}", template.SubjectTemplate);
        Assert.Contains("{{replyMessage}}", template.ContentTemplate, StringComparison.Ordinal);
        Assert.Contains("replyMessage", template.Parameters);
        Assert.Contains("recipientEmail", template.Parameters);
        Assert.Contains("recipientName", template.Parameters);
    }

    [Fact]
    public void ResolveContactInboxEmail_MissingConfiguration_DefaultsToPublicInfoAddress()
    {
        var configuration = new ConfigurationBuilder().Build();

        var email = NotificationBootstrapData.ResolveContactInboxEmail(configuration);

        Assert.Equal("info@maliev.com", email);
    }

    [Fact]
    public void CreateContactInboxEmailBinding_EncryptsConfiguredRecipientForStableInboxUser()
    {
        var binding = NotificationBootstrapData.CreateContactInboxEmailBinding("encrypted-info-address");

        Assert.Equal("maliev-contact-inbox", binding.UserId);
        Assert.Equal("email", binding.ChannelType);
        Assert.Equal("encrypted-info-address", binding.ChannelIdentifier);
        Assert.True(binding.IsValid);
    }

    [Fact]
    public void CreateContactInboxPreference_RoutesInboxUserToEmailOnly()
    {
        var preference = NotificationBootstrapData.CreateContactInboxPreference();

        Assert.Equal("maliev-contact-inbox", preference.UserId);
        Assert.Equal("email", preference.PrimaryChannelType);
        Assert.Empty(preference.FallbackChannelTypes);
        Assert.Empty(preference.OptOutCategories);
    }

    [Fact]
    public void CreateOperationsPaymentReceivedEmailTemplate_DefinesPaidOrderOperationsTemplate()
    {
        var template = NotificationBootstrapData.CreateOperationsPaymentReceivedEmailTemplate();

        Assert.Equal("operations-payment-received", template.TemplateKey);
        Assert.Equal("email", template.ChannelType);
        Assert.Equal("Payment received for {{orderId}}", template.SubjectTemplate);
        Assert.Contains("production queue", template.ContentTemplate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orderId", template.Parameters);
        Assert.Contains("amount", template.Parameters);
        Assert.Contains("paymentId", template.Parameters);
        Assert.Contains("customerId", template.Parameters);
    }

    [Fact]
    public void CreateCustomerOrderConfirmedEmailTemplate_DefinesPaidOrderReceiptTemplate()
    {
        var template = NotificationBootstrapData.CreateCustomerOrderConfirmedEmailTemplate();

        Assert.Equal("order-confirmed", template.TemplateKey);
        Assert.Equal(2, template.Version);
        Assert.Equal("email", template.ChannelType);
        Assert.Equal("Your MALIEV order {{orderId}} is confirmed", template.SubjectTemplate);
        Assert.Contains("payment receipt", template.ContentTemplate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("production queue", template.ContentTemplate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orderId", template.Parameters);
        Assert.Contains("amount", template.Parameters);
        Assert.Contains("paymentId", template.Parameters);
    }

    [Fact]
    public void CreateOperationsJobCompletedQcReadyEmailTemplate_DefinesQcIntakeTemplate()
    {
        var template = NotificationBootstrapData.CreateOperationsJobCompletedQcReadyEmailTemplate();

        Assert.Equal("operations-job-completed-qc-ready", template.TemplateKey);
        Assert.Equal("email", template.ChannelType);
        Assert.Equal("Job {{jobId}} is ready for QC intake", template.SubjectTemplate);
        Assert.Contains("QC intake", template.ContentTemplate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("jobId", template.Parameters);
        Assert.Contains("orderId", template.Parameters);
        Assert.Contains("technology", template.Parameters);
        Assert.Contains("assignedMachineId", template.Parameters);
        Assert.Contains("completedAt", template.Parameters);
        Assert.Contains("changedBy", template.Parameters);
    }

    [Fact]
    public void CreateCustomerOrderCompletedEmailTemplate_DefinesUsefulCompletionTemplate()
    {
        var template = NotificationBootstrapData.CreateCustomerOrderCompletedEmailTemplate();

        Assert.Equal("order-completed", template.TemplateKey);
        Assert.Equal("email", template.ChannelType);
        Assert.Equal("Your MALIEV order {{orderId}} is complete", template.SubjectTemplate);
        Assert.Contains("quality control and shipping", template.ContentTemplate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orderId", template.Parameters);
        Assert.Contains("completedAt", template.Parameters);
    }

    [Fact]
    public void CreateCustomerOrderCompletionFailedEmailTemplate_DefinesUsefulFailureTemplate()
    {
        var template = NotificationBootstrapData.CreateCustomerOrderCompletionFailedEmailTemplate();

        Assert.Equal("order-completion-failed", template.TemplateKey);
        Assert.Equal("email", template.ChannelType);
        Assert.Equal("Update on MALIEV order {{orderId}}", template.SubjectTemplate);
        Assert.Contains("production issue", template.ContentTemplate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orderId", template.Parameters);
        Assert.Contains("completedAt", template.Parameters);
    }

    [Fact]
    public void CreateCustomerOrderShippedEmailTemplate_DefinesTrackingTemplate()
    {
        var template = NotificationBootstrapData.CreateCustomerOrderShippedEmailTemplate();

        Assert.Equal("order-shipped", template.TemplateKey);
        Assert.Equal("email", template.ChannelType);
        Assert.Equal("Your MALIEV order {{orderId}} has shipped", template.SubjectTemplate);
        Assert.Contains("{{trackingNumber}}", template.ContentTemplate, StringComparison.Ordinal);
        Assert.Contains("orderId", template.Parameters);
        Assert.Contains("carrier", template.Parameters);
        Assert.Contains("trackingNumber", template.Parameters);
        Assert.Contains("estimatedDeliveryDate", template.Parameters);
    }

    [Fact]
    public void CreateCustomerDeliveryCompletedEmailTemplate_DefinesDeliveredOrderTemplate()
    {
        var template = NotificationBootstrapData.CreateCustomerDeliveryCompletedEmailTemplate();

        Assert.Equal("delivery-completed", template.TemplateKey);
        Assert.Equal("email", template.ChannelType);
        Assert.Equal("Your MALIEV delivery {{deliveryNoteId}} is complete", template.SubjectTemplate);
        Assert.Contains("has been delivered", template.ContentTemplate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orderId", template.Parameters);
        Assert.Contains("deliveryNoteId", template.Parameters);
        Assert.Contains("completedAt", template.Parameters);
        Assert.Contains("receivedByName", template.Parameters);
    }

    [Fact]
    public void CreateOperationsInboxEmailBinding_EncryptsConfiguredRecipientForStableInboxUser()
    {
        var binding = NotificationBootstrapData.CreateOperationsInboxEmailBinding("encrypted-ops-address");

        Assert.Equal("maliev-operations-inbox", binding.UserId);
        Assert.Equal("email", binding.ChannelType);
        Assert.Equal("encrypted-ops-address", binding.ChannelIdentifier);
        Assert.True(binding.IsValid);
    }

    [Fact]
    public void CreateOperationsInboxPreference_RoutesInboxUserToEmailOnly()
    {
        var preference = NotificationBootstrapData.CreateOperationsInboxPreference();

        Assert.Equal("maliev-operations-inbox", preference.UserId);
        Assert.Equal("email", preference.PrimaryChannelType);
        Assert.Empty(preference.FallbackChannelTypes);
        Assert.Empty(preference.OptOutCategories);
    }
}
