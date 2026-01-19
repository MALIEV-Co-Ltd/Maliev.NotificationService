using Moq;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Api.Providers;
using Maliev.NotificationService.Data;
using Maliev.NotificationService.Tests.Testing;

namespace Maliev.NotificationService.Api.Tests.Integration;

public class ChannelProviderFactoryTests : IClassFixture<BaseIntegrationTestFactory<Program, NotificationDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, NotificationDbContext> _factory;

    public ChannelProviderFactoryTests(BaseIntegrationTestFactory<Program, NotificationDbContext> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("email", typeof(EmailProvider))]
    [InlineData("line", typeof(LineProvider))]
    [InlineData("whatsapp", typeof(WhatsAppProvider))]
    [InlineData("sms", typeof(SmsProvider))]
    [InlineData("slack", typeof(SlackProvider))]
    [InlineData("facebook", typeof(FacebookMessengerProvider))]
    [InlineData("instagram", typeof(InstagramProvider))]
    public void GetProvider_ValidChannel_ReturnsCorrectProvider(string channel, Type expectedType)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ChannelProviderFactory>();

        // Act
        var provider = factory.GetProvider(channel);

        // Assert
        Assert.NotNull(provider);
        Assert.IsType(expectedType, provider);
        Assert.Equal(channel, provider.ChannelType, ignoreCase: true);
    }

    [Fact]
    public void GetProvider_InvalidChannel_ThrowsException()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ChannelProviderFactory>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => factory.GetProvider("invalid"));
    }

    [Fact]
    public void IsChannelSupported_ValidChannel_ReturnsTrue()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ChannelProviderFactory>();

        // Act & Assert
        Assert.True(factory.IsChannelSupported("email"));
        Assert.False(factory.IsChannelSupported("telepathy"));
    }
}
