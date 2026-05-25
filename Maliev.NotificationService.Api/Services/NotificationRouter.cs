using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.NotificationService.Api.Metrics;
using Maliev.NotificationService.Api.Providers;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;

namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Routes notifications to channel providers based on user preferences and defaults.
/// Implements preference resolution, channel selection, and fallback logic.
/// </summary>
public class NotificationRouter : INotificationRouter
{
    private readonly NotificationDbContext _dbContext;
    private readonly IEnumerable<IChannelProvider> _channelProviders;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<NotificationRouter> _logger;

    // Default channel mappings by user type (when no preference exists)
    private static readonly Dictionary<string, string[]> DefaultChannelsByUserType = new()
    {
        ["customer"] = new[] { "email" },
        ["staff"] = new[] { "email", "slack" },
        ["administrator"] = new[] { "email", "sms" }
    };

    public NotificationRouter(
        NotificationDbContext dbContext,
        IEnumerable<IChannelProvider> channelProviders,
        ITemplateRenderer templateRenderer,
        IEncryptionService encryptionService,
        ILogger<NotificationRouter> logger)
    {
        _dbContext = dbContext;
        _channelProviders = channelProviders;
        _templateRenderer = templateRenderer;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<RoutingResult> RouteAsync(
        NotificationEvent notificationEvent,
        NotificationEventPayloadTargetUsersItem targetUser,
        CancellationToken cancellationToken = default)
    {
        var payload = notificationEvent.Payload;
        try
        {
            if (IsDirectEmailTarget(targetUser))
            {
                return await RouteDirectEmailAsync(notificationEvent, targetUser, cancellationToken);
            }

            // Check if user opted out of this notification category
            var preference = await _dbContext.UserNotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == targetUser.UserId, cancellationToken);

            if (preference != null)
            {
                if (preference.OptOutCategories.Contains(payload.NotificationType, StringComparer.OrdinalIgnoreCase))
                {
                    _logger.LogInformation(
                        "User opted out of notification category: UserId={UserId}, Category={Category}",
                        targetUser.UserId,
                        payload.NotificationType);

                    return RoutingResult.Skipped($"User opted out of category: {payload.NotificationType}");
                }
            }

            // Resolve channels (preference or default)
            var channels = await ResolveChannelsAsync(targetUser.UserId, targetUser.UserType, cancellationToken);
            var primaryChannel = channels.FirstOrDefault();

            if (primaryChannel == null)
            {
                return RoutingResult.Failed($"No channel found for user: {targetUser.UserId}");
            }

            _logger.LogInformation(
                "Routing notification to primary channel: UserId={UserId}, Channel={Channel}, EventId={EventId}",
                targetUser.UserId,
                primaryChannel,
                notificationEvent.MessageId);

            // Look up channel binding for actual recipient identifier
            var channelBinding = await _dbContext.ChannelBindings
                .FirstOrDefaultAsync(
                    b => b.UserId == targetUser.UserId
                        && b.ChannelType == primaryChannel
                        && b.IsValid,
                    cancellationToken);

            if (channelBinding == null)
            {
                _logger.LogWarning(
                    "No valid channel binding found: UserId={UserId}, Channel={Channel}",
                    targetUser.UserId,
                    primaryChannel);

                return RoutingResult.Failed($"No valid channel binding for {primaryChannel}");
            }

            // Get the channel provider
            var provider = ResolveChannelProvider(primaryChannel);
            if (provider == null)
            {
                return RoutingResult.Failed($"No provider registered for channel: {primaryChannel}");
            }

            // Render message using template (if templateId provided)
            var renderedNotification = await RenderNotificationAsync(notificationEvent, primaryChannel, cancellationToken);
            var providerMetadata = BuildProviderMetadata(payload, renderedNotification);

            // Decrypt channel identifier before sending to provider
            var decryptedIdentifier = _encryptionService.Decrypt(channelBinding.ChannelIdentifier);

            // Attempt delivery on primary channel
            var stopwatch = Stopwatch.StartNew();
            var deliveryResult = await provider.SendAsync(
                decryptedIdentifier,
                renderedNotification.Message,
                providerMetadata,
                cancellationToken);
            stopwatch.Stop();

            // Record delivery latency
            NotificationMetrics.DeliveryLatency.Record(
                stopwatch.ElapsedMilliseconds,
                new KeyValuePair<string, object?>("channel", primaryChannel),
                new KeyValuePair<string, object?>("status", deliveryResult.Success ? "success" : "failed"));

            if (deliveryResult.Success)
            {
                // Increment successful delivery counter
                NotificationMetrics.NotificationsSent.Add(1,
                    new KeyValuePair<string, object?>("channel", primaryChannel),
                    new KeyValuePair<string, object?>("priority", payload.Priority ?? "critical"));

                return RoutingResult.Successful(primaryChannel, deliveryResult);
            }

            // Increment failed delivery counter
            NotificationMetrics.NotificationsFailed.Add(1,
                new KeyValuePair<string, object?>("channel", primaryChannel),
                new KeyValuePair<string, object?>("error_type", deliveryResult.FailureType?.ToString() ?? "unknown"));

            // Check if failure is permanent (invalid recipient) - invalidate binding
            if (!deliveryResult.Success &&
                deliveryResult.FailureType == DeliveryFailureType.InvalidRecipient)
            {
                await InvalidateChannelBindingAsync(channelBinding, deliveryResult.ErrorMessage ?? "Invalid recipient", cancellationToken);
            }

            // Primary channel failed, try fallback channels
            if (channels.Count > 1)
            {
                _logger.LogWarning(
                    "Primary channel failed, attempting fallback: EventId={EventId}, PrimaryChannel={PrimaryChannel}",
                    notificationEvent.MessageId,
                    primaryChannel);

                // Increment fallback triggered counter
                NotificationMetrics.FallbackTriggered.Add(1,
                    new KeyValuePair<string, object?>("primary_channel", primaryChannel),
                    new KeyValuePair<string, object?>("reason", deliveryResult.FailureType?.ToString() ?? "unknown"));

                foreach (var fallbackChannel in channels.Skip(1))
                {
                    var fallbackBinding = await _dbContext.ChannelBindings
                        .FirstOrDefaultAsync(
                            b => b.UserId == targetUser.UserId
                                && b.ChannelType == fallbackChannel
                                && b.IsValid,
                            cancellationToken);

                    if (fallbackBinding == null)
                    {
                        _logger.LogWarning(
                            "No valid channel binding for fallback: UserId={UserId}, Channel={Channel}",
                            targetUser.UserId,
                            fallbackChannel);
                        continue;
                    }

                    var fallbackProvider = ResolveChannelProvider(fallbackChannel);
                    if (fallbackProvider == null)
                    {
                        _logger.LogWarning("No provider for fallback channel: {Channel}", fallbackChannel);
                        continue;
                    }

                    // Decrypt fallback channel identifier before sending
                    var decryptedFallbackIdentifier = _encryptionService.Decrypt(fallbackBinding.ChannelIdentifier);

                    var fallbackStopwatch = Stopwatch.StartNew();
                    var fallbackResult = await fallbackProvider.SendAsync(
                        decryptedFallbackIdentifier,
                        renderedNotification.Message,
                        providerMetadata,
                        cancellationToken);
                    fallbackStopwatch.Stop();

                    // Record fallback delivery latency
                    NotificationMetrics.DeliveryLatency.Record(
                        fallbackStopwatch.ElapsedMilliseconds,
                        new KeyValuePair<string, object?>("channel", fallbackChannel),
                        new KeyValuePair<string, object?>("status", fallbackResult.Success ? "success" : "failed"));

                    if (fallbackResult.Success)
                    {
                        // Increment successful delivery counter for fallback
                        NotificationMetrics.NotificationsSent.Add(1,
                            new KeyValuePair<string, object?>("channel", fallbackChannel),
                            new KeyValuePair<string, object?>("priority", payload.Priority ?? "critical"));

                        return RoutingResult.Successful(fallbackChannel, fallbackResult, fallbackChannel);
                    }

                    // Increment failed delivery counter for fallback
                    NotificationMetrics.NotificationsFailed.Add(1,
                        new KeyValuePair<string, object?>("channel", fallbackChannel),
                        new KeyValuePair<string, object?>("error_type", fallbackResult.FailureType?.ToString() ?? "unknown"));

                    // Check if fallback failure is permanent - invalidate binding
                    if (!fallbackResult.Success &&
                        fallbackResult.FailureType == DeliveryFailureType.InvalidRecipient)
                    {
                        await InvalidateChannelBindingAsync(fallbackBinding, fallbackResult.ErrorMessage ?? "Invalid recipient", cancellationToken);
                    }
                }
            }

            // All channels failed
            return RoutingResult.Failed(
                $"All channels failed: {deliveryResult.ErrorMessage}",
                deliveryResult.IsRetryable,
                primaryChannel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing notification: EventId={EventId}", notificationEvent.MessageId);
            var isRetryable = ex is not TemplateRenderingException;
            return RoutingResult.Failed($"Routing error: {ex.Message}", isRetryable: isRetryable);
        }
    }

    private async Task<RoutingResult> RouteDirectEmailAsync(
        NotificationEvent notificationEvent,
        NotificationEventPayloadTargetUsersItem targetUser,
        CancellationToken cancellationToken)
    {
        var payload = notificationEvent.Payload;
        var recipientEmail = GetParameterValue(payload.Parameters, "recipientEmail");

        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            return RoutingResult.Failed("Direct email target is missing recipientEmail.", selectedChannel: "email");
        }

        var provider = ResolveChannelProvider("email");
        if (provider == null)
        {
            return RoutingResult.Failed("No provider registered for channel: email", selectedChannel: "email");
        }

        _logger.LogInformation(
            "Routing notification to direct email target: UserId={UserId}, EventId={EventId}",
            targetUser.UserId,
            notificationEvent.MessageId);

        var renderedNotification = await RenderNotificationAsync(notificationEvent, "email", cancellationToken);
        var metadata = BuildProviderMetadata(payload, renderedNotification);

        var stopwatch = Stopwatch.StartNew();
        var deliveryResult = await provider.SendAsync(
            recipientEmail,
            renderedNotification.Message,
            metadata,
            cancellationToken);
        stopwatch.Stop();

        NotificationMetrics.DeliveryLatency.Record(
            stopwatch.ElapsedMilliseconds,
            new KeyValuePair<string, object?>("channel", "email"),
            new KeyValuePair<string, object?>("status", deliveryResult.Success ? "success" : "failed"));

        if (deliveryResult.Success)
        {
            NotificationMetrics.NotificationsSent.Add(1,
                new KeyValuePair<string, object?>("channel", "email"),
                new KeyValuePair<string, object?>("priority", payload.Priority ?? "critical"));

            return RoutingResult.Successful("email", deliveryResult);
        }

        NotificationMetrics.NotificationsFailed.Add(1,
            new KeyValuePair<string, object?>("channel", "email"),
            new KeyValuePair<string, object?>("error_type", deliveryResult.FailureType?.ToString() ?? "unknown"));

        return RoutingResult.Failed(
            deliveryResult.ErrorMessage ?? "Direct email delivery failed.",
            deliveryResult.IsRetryable,
            "email");
    }

    /// <summary>
    /// Resolves channels for a user (preference or default based on user type).
    /// Returns primary channel followed by fallback channels.
    /// </summary>
    private async Task<List<string>> ResolveChannelsAsync(
        string userId,
        string userType,
        CancellationToken cancellationToken)
    {
        // Try to get user preferences
        var preference = await _dbContext.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (preference != null)
        {
            // User has configured preferences
            var channels = new List<string> { preference.PrimaryChannelType };
            channels.AddRange(preference.FallbackChannelTypes);

            _logger.LogDebug(
                "Using user preferences: UserId={UserId}, Channels={Channels}",
                userId,
                string.Join(", ", channels));

            return channels;
        }

        // No preferences - use defaults based on user type
        var defaultChannels = GetDefaultChannels(userType).ToList();

        _logger.LogDebug(
            "Using default channels for user type: UserId={UserId}, UserType={UserType}, Channels={Channels}",
            userId,
            userType,
            string.Join(", ", defaultChannels));

        return defaultChannels;
    }

    /// <summary>
    /// Resolves a channel provider by channel type.
    /// </summary>
    public IChannelProvider? ResolveChannelProvider(string channelType)
    {
        return _channelProviders.FirstOrDefault(p =>
            p.ChannelType.Equals(channelType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets default channels for a user type.
    /// </summary>
    private static string[] GetDefaultChannels(string userType)
    {
        return DefaultChannelsByUserType.TryGetValue(userType.ToLowerInvariant(), out var channels)
            ? channels
            : new[] { "email" }; // Fallback to email if user type unknown
    }

    /// <summary>
    /// Renders the notification message using templates.
    /// Falls back to simple parameter substitution if template not found.
    /// </summary>
    private async Task<RenderedNotification> RenderNotificationAsync(
        NotificationEvent notificationEvent,
        string channelType,
        CancellationToken cancellationToken)
    {
        var payload = notificationEvent.Payload;
        // If no templateId provided, use simple parameter substitution
        if (string.IsNullOrEmpty(payload.TemplateId))
        {
            _logger.LogDebug("No templateId provided, using simple rendering for EventId={EventId}",
                notificationEvent.MessageId);
            return new RenderedNotification(RenderSimpleMessage(notificationEvent), payload.NotificationType);
        }

        // Get language from metadata or default to "en"
        var language = payload.Metadata?.Language ?? "en";

        // Normalize channel type to lowercase for comparison
        var channelTypeLower = channelType.ToLowerInvariant();

        // Query template from database
        var template = await _dbContext.NotificationTemplates
            .Where(t => t.TemplateKey == payload.TemplateId
                        && t.Language == language
                        && t.ChannelType == channelTypeLower)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(cancellationToken);

        if (template == null)
        {
            _logger.LogWarning(
                "Template not found: TemplateId={TemplateId}, Language={Language}, Channel={Channel}, falling back to simple rendering",
                payload.TemplateId,
                language,
                channelType);
            return new RenderedNotification(RenderSimpleMessage(notificationEvent), payload.NotificationType);
        }

        try
        {
            // Convert parameters to object dictionary for renderer
            var parameters = ExtractParameterDictionary(payload.Parameters);

            // Render template with parameters
            var renderedMessage = _templateRenderer.Render(
                template.ContentTemplate,
                template.Parameters,
                parameters);
            var renderedSubject = string.IsNullOrWhiteSpace(template.SubjectTemplate)
                ? payload.NotificationType
                : _templateRenderer.Render(template.SubjectTemplate, template.Parameters, parameters);

            _logger.LogDebug(
                "Successfully rendered template: TemplateId={TemplateId}, Language={Language}, Channel={Channel}",
                payload.TemplateId,
                language,
                channelType);

            return new RenderedNotification(renderedMessage, renderedSubject);
        }
        catch (TemplateRenderingException ex)
        {
            _logger.LogError(ex,
                "Template rendering failed: TemplateId={TemplateId}, Error={Error}",
                payload.TemplateId,
                ex.Message);
            throw; // Re-throw to fail the notification delivery
        }
    }

    private static Dictionary<string, string> BuildProviderMetadata(
        NotificationEventPayload payload,
        RenderedNotification renderedNotification)
    {
        var metadata = new Dictionary<string, string>
        {
            ["subject"] = renderedNotification.Subject ?? payload.NotificationType
        };
        var recipientName = GetParameterValue(payload.Parameters, "recipientName");

        if (!string.IsNullOrWhiteSpace(recipientName))
        {
            metadata["recipientName"] = recipientName;
        }

        return metadata;
    }

    private static Dictionary<string, object> ExtractParameterDictionary(object? parameters)
    {
        var result = new Dictionary<string, object>();

        if (parameters is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in jsonElement.EnumerateObject())
            {
                result[prop.Name] = prop.Value.ToString();
            }
        }
        else if (parameters is IDictionary<string, string> stringDictionary)
        {
            foreach (var (key, value) in stringDictionary)
            {
                result[key] = value;
            }
        }
        else if (parameters is IDictionary<string, object> objectDictionary)
        {
            foreach (var (key, value) in objectDictionary)
            {
                result[key] = value;
            }
        }
        else if (parameters is IReadOnlyDictionary<string, string> readOnlyStringDictionary)
        {
            foreach (var (key, value) in readOnlyStringDictionary)
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static string? GetParameterValue(object? parameters, string key)
    {
        if (parameters is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
        {
            return jsonElement.TryGetProperty(key, out var property) ? property.ToString() : null;
        }

        if (parameters is IDictionary<string, string> stringDictionary &&
            stringDictionary.TryGetValue(key, out var stringValue))
        {
            return stringValue;
        }

        if (parameters is IDictionary<string, object> objectDictionary &&
            objectDictionary.TryGetValue(key, out var objectValue))
        {
            return objectValue?.ToString();
        }

        if (parameters is IReadOnlyDictionary<string, string> readOnlyStringDictionary &&
            readOnlyStringDictionary.TryGetValue(key, out var readOnlyStringValue))
        {
            return readOnlyStringValue;
        }

        return null;
    }

    private static bool IsDirectEmailTarget(NotificationEventPayloadTargetUsersItem targetUser) =>
        string.Equals(targetUser.UserType, "direct-email", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Simple message rendering without templates (fallback).
    /// </summary>
    private static string RenderSimpleMessage(NotificationEvent notificationEvent)
    {
        var payload = notificationEvent.Payload;
        var message = $"Notification: {payload.NotificationType}\n";

        if (payload.Parameters is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in jsonElement.EnumerateObject())
            {
                message += $"{prop.Name}: {prop.Value}\n";
            }
        }

        return message;
    }

    private sealed record RenderedNotification(string Message, string? Subject);

    /// <summary>
    /// Invalidates a channel binding when a permanent failure occurs (e.g., invalid recipient).
    /// Marks the binding as invalid and records the reason and timestamp.
    /// </summary>
    private async Task InvalidateChannelBindingAsync(
        Domain.Entities.ChannelBinding channelBinding,
        string invalidatedReason,
        CancellationToken cancellationToken)
    {
        try
        {
            channelBinding.IsValid = false;
            channelBinding.InvalidatedAt = DateTimeOffset.UtcNow;
            channelBinding.InvalidatedReason = invalidatedReason;
            channelBinding.UpdatedAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Channel binding invalidated: BindingId={BindingId}, UserId={UserId}, ChannelType={ChannelType}, Reason={Reason}",
                channelBinding.Id,
                channelBinding.UserId,
                channelBinding.ChannelType,
                invalidatedReason);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to invalidate channel binding: BindingId={BindingId}",
                channelBinding.Id);
            // Don't throw - binding invalidation failure shouldn't stop notification processing
        }
    }
}
