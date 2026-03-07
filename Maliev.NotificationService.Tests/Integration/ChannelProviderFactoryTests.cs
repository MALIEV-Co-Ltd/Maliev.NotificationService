using Moq;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Api.Providers;
using Maliev.NotificationService.Infrastructure.Persistence;
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
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ChannelProviderFactory>();

        var provider = factory.GetProvider(channel);

        Assert.NotNull(provider);
        Assert.IsType(expectedType, provider);
        Assert.Equal(channel, provider.ChannelType, ignoreCase: true);
    }

    [Fact]
    public void GetProvider_InvalidChannel_ThrowsException()
    {
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ChannelProviderFactory>();

        Assert.Throws<InvalidOperationException>(() => factory.GetProvider("invalid"));
    }

    [Fact]
    public void IsChannelSupported_ValidChannel_ReturnsTrue()
    {
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ChannelProviderFactory>();

        Assert.True(factory.IsChannelSupported("email"));
        Assert.False(factory.IsChannelSupported("telepathy"));
    }

    [Fact]
    public void GetProvider_NullChannel_ThrowsArgumentException()
    {
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ChannelProviderFactory>();

        Assert.Throws<ArgumentException>(() => factory.GetProvider(null!));
    }

    [Fact]
    public void GetProvider_WhitespaceChannel_ThrowsArgumentException()
    {
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ChannelProviderFactory>();

        Assert.Throws<ArgumentException>(() => factory.GetProvider("   "));
    }

    [Fact]
    public void IsChannelSupported_Null_ReturnsFalse()
    {
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ChannelProviderFactory>();

        Assert.False(factory.IsChannelSupported(null!));
    }

    [Fact]
    public void GetAvailableChannelTypes_ReturnsAllChannels()
    {
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ChannelProviderFactory>();

        var channels = factory.GetAvailableChannelTypes().ToList();

        Assert.Contains("email", channels);
        Assert.Contains("line", channels);
        Assert.Contains("whatsapp", channels);
        Assert.Contains("sms", channels);
        Assert.Contains("slack", channels);
        Assert.Contains("facebook", channels);
        Assert.Contains("instagram", channels);
        Assert.Equal(7, channels.Count);
    }

    [Fact]
    public void GetProvider_CaseInsensitiveChannel_ReturnsProvider()
    {
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ChannelProviderFactory>();

        var provider = factory.GetProvider("EMAIL");

        Assert.NotNull(provider);
        Assert.Equal("email", provider.ChannelType);
    }
}
