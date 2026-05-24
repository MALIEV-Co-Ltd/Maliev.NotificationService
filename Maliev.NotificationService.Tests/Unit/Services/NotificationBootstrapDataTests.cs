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
        Assert.Equal("email", template.ChannelType);
        Assert.Equal("en", template.Language);
        Assert.Equal("New website contact: {{subject}}", template.SubjectTemplate);
        Assert.Contains("{{fullName}}", template.ContentTemplate, StringComparison.Ordinal);
        Assert.Contains("{{email}}", template.ContentTemplate, StringComparison.Ordinal);
        Assert.Contains("{{message}}", template.ContentTemplate, StringComparison.Ordinal);
        Assert.Contains("contactId", template.Parameters);
        Assert.Contains("fullName", template.Parameters);
        Assert.Contains("email", template.Parameters);
        Assert.Contains("subject", template.Parameters);
        Assert.Contains("message", template.Parameters);
        Assert.Contains("attachmentCount", template.Parameters);
        Assert.Contains("attachmentNames", template.Parameters);
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
}
