using Maliev.NotificationService.Api.Providers;

namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Factory for resolving IChannelProvider by channel type.
/// Implements the Factory pattern to enable dynamic provider selection.
/// </summary>
public class ChannelProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChannelProviderFactory> _logger;
    private readonly Dictionary<string, Type> _providerTypes;

    public ChannelProviderFactory(
        IServiceProvider serviceProvider,
        ILogger<ChannelProviderFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Register all provider types
        _providerTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["email"] = typeof(EmailProvider),
            ["line"] = typeof(LineProvider),
            ["whatsapp"] = typeof(WhatsAppProvider),
            ["sms"] = typeof(SmsProvider),
            ["slack"] = typeof(SlackProvider),
            ["facebook"] = typeof(FacebookMessengerProvider),
            ["instagram"] = typeof(InstagramProvider)
        };
    }

    /// <summary>
    /// Gets the appropriate channel provider for the specified channel type.
    /// </summary>
    /// <param name="channelType">The channel type (email, line, whatsapp, etc.)</param>
    /// <returns>The channel provider instance</returns>
    /// <exception cref="InvalidOperationException">Thrown when no provider is registered for the channel type</exception>
    public IChannelProvider GetProvider(string channelType)
    {
        if (string.IsNullOrWhiteSpace(channelType))
        {
            throw new ArgumentException("Channel type cannot be null or empty", nameof(channelType));
        }

        if (!_providerTypes.TryGetValue(channelType, out var providerType))
        {
            _logger.LogError(
                "No provider registered for channel type: {ChannelType}. Available channels: {AvailableChannels}",
                channelType,
                string.Join(", ", _providerTypes.Keys));

            throw new InvalidOperationException(
                $"No provider registered for channel type: {channelType}. " +
                $"Available channels: {string.Join(", ", _providerTypes.Keys)}");
        }

        try
        {
            var provider = (IChannelProvider)_serviceProvider.GetRequiredService(providerType);

            _logger.LogDebug(
                "Resolved channel provider: ChannelType={ChannelType}, ProviderType={ProviderType}",
                channelType,
                providerType.Name);

            return provider;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to resolve channel provider: ChannelType={ChannelType}, ProviderType={ProviderType}",
                channelType,
                providerType.Name);

            throw new InvalidOperationException(
                $"Failed to resolve channel provider for type: {channelType}. " +
                $"Ensure the provider is registered in DI container.",
                ex);
        }
    }

    /// <summary>
    /// Gets all available channel types.
    /// </summary>
    /// <returns>Collection of available channel types</returns>
    public IEnumerable<string> GetAvailableChannelTypes()
    {
        return _providerTypes.Keys;
    }

    /// <summary>
    /// Checks if a provider is registered for the specified channel type.
    /// </summary>
    /// <param name="channelType">The channel type to check</param>
    /// <returns>True if provider is registered, false otherwise</returns>
    public bool IsChannelSupported(string channelType)
    {
        return !string.IsNullOrWhiteSpace(channelType) &&
               _providerTypes.ContainsKey(channelType);
    }
}
