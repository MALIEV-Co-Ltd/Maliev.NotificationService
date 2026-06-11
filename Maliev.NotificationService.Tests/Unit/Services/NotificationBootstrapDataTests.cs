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
