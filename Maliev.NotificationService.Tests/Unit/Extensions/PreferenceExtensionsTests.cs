using System.Text.Json;
using Maliev.NotificationService.Data.Entities;
using Maliev.NotificationService.Api.Extensions;
using Maliev.NotificationService.Api.Models.Requests;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Unit.Extensions;

/// <summary>
/// Unit tests for PreferenceExtensions mapping methods
/// </summary>
public class PreferenceExtensionsTests
{
    [Fact]
    public void ToResponse_ValidEntity_ReturnsCorrectResponse()
    {
        // Arrange
        var entity = new UserNotificationPreference
        {
            UserId = "user123",
            PrimaryChannelType = "email",
            FallbackChannelTypes = JsonSerializer.Serialize(new List<string> { "sms", "slack" }),
            OptOutCategories = JsonSerializer.Serialize(new List<string> { "marketing" }),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var response = entity.ToResponse();

        // Assert
        Assert.Equal("user123", response.UserId);
        Assert.Equal("email", response.PrimaryChannelType);
        Assert.Equal(2, response.FallbackChannelTypes.Count);
        Assert.Contains("sms", response.FallbackChannelTypes);
        Assert.Contains("slack", response.FallbackChannelTypes);
        Assert.Single(response.OptOutCategories);
        Assert.Contains("marketing", response.OptOutCategories);
        Assert.Equal(entity.CreatedAt, response.CreatedAt);
        Assert.Equal(entity.UpdatedAt, response.UpdatedAt);
    }

    [Fact]
    public void ToResponse_EmptyJSONArrays_ReturnsEmptyLists()
    {
        // Arrange
        var entity = new UserNotificationPreference
        {
            UserId = "user123",
            PrimaryChannelType = "email",
            FallbackChannelTypes = "[]",
            OptOutCategories = "[]",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var response = entity.ToResponse();

        // Assert
        Assert.Empty(response.FallbackChannelTypes);
        Assert.Empty(response.OptOutCategories);
    }

    [Fact]
    public void ToEntity_ValidRequest_ReturnsCorrectEntity()
    {
        // Arrange
        var request = new CreatePreferenceRequest
        {
            UserId = "user456",
            PrimaryChannelType = "LINE",
            FallbackChannelTypes = new List<string> { "EMAIL", "WhatsApp" },
            OptOutCategories = new List<string> { "promotions", "newsletters" }
        };

        // Act
        var entity = request.ToEntity();

        // Assert
        Assert.Equal("user456", entity.UserId);
        Assert.Equal("line", entity.PrimaryChannelType); // Should be lowercase

        var fallbackChannels = JsonSerializer.Deserialize<List<string>>(entity.FallbackChannelTypes);
        Assert.NotNull(fallbackChannels);
        Assert.Equal(2, fallbackChannels.Count);
        Assert.Contains("email", fallbackChannels); // Should be lowercase
        Assert.Contains("whatsapp", fallbackChannels); // Should be lowercase

        var optOutCategories = JsonSerializer.Deserialize<List<string>>(entity.OptOutCategories);
        Assert.NotNull(optOutCategories);
        Assert.Equal(2, optOutCategories.Count);
        Assert.Contains("promotions", optOutCategories);
        Assert.Contains("newsletters", optOutCategories);
    }

    [Fact]
    public void ApplyUpdate_UpdatePrimaryChannel_UpdatesField()
    {
        // Arrange
        var entity = new UserNotificationPreference
        {
            UserId = "user789",
            PrimaryChannelType = "email",
            FallbackChannelTypes = "[]",
            OptOutCategories = "[]"
        };

        var updateRequest = new UpdatePreferenceRequest
        {
            PrimaryChannelType = "WhatsApp"
        };

        // Act
        entity.ApplyUpdate(updateRequest);

        // Assert
        Assert.Equal("whatsapp", entity.PrimaryChannelType); // Should be lowercase
    }

    [Fact]
    public void ApplyUpdate_UpdateFallbackChannels_UpdatesField()
    {
        // Arrange
        var entity = new UserNotificationPreference
        {
            UserId = "user789",
            PrimaryChannelType = "email",
            FallbackChannelTypes = JsonSerializer.Serialize(new List<string> { "sms" }),
            OptOutCategories = "[]"
        };

        var updateRequest = new UpdatePreferenceRequest
        {
            FallbackChannelTypes = new List<string> { "LINE", "Slack" }
        };

        // Act
        entity.ApplyUpdate(updateRequest);

        // Assert
        var fallbackChannels = JsonSerializer.Deserialize<List<string>>(entity.FallbackChannelTypes);
        Assert.NotNull(fallbackChannels);
        Assert.Equal(2, fallbackChannels.Count);
        Assert.Contains("line", fallbackChannels); // Should be lowercase
        Assert.Contains("slack", fallbackChannels); // Should be lowercase
    }

    [Fact]
    public void ApplyUpdate_UpdateOptOutCategories_UpdatesField()
    {
        // Arrange
        var entity = new UserNotificationPreference
        {
            UserId = "user789",
            PrimaryChannelType = "email",
            FallbackChannelTypes = "[]",
            OptOutCategories = JsonSerializer.Serialize(new List<string> { "marketing" })
        };

        var updateRequest = new UpdatePreferenceRequest
        {
            OptOutCategories = new List<string> { "promotions", "surveys" }
        };

        // Act
        entity.ApplyUpdate(updateRequest);

        // Assert
        var optOutCategories = JsonSerializer.Deserialize<List<string>>(entity.OptOutCategories);
        Assert.NotNull(optOutCategories);
        Assert.Equal(2, optOutCategories.Count);
        Assert.Contains("promotions", optOutCategories);
        Assert.Contains("surveys", optOutCategories);
    }

    [Fact]
    public void ApplyUpdate_NullFields_DoesNotUpdateEntity()
    {
        // Arrange
        var originalPrimaryChannel = "email";
        var originalFallbackChannels = JsonSerializer.Serialize(new List<string> { "sms" });
        var originalOptOutCategories = JsonSerializer.Serialize(new List<string> { "marketing" });

        var entity = new UserNotificationPreference
        {
            UserId = "user789",
            PrimaryChannelType = originalPrimaryChannel,
            FallbackChannelTypes = originalFallbackChannels,
            OptOutCategories = originalOptOutCategories
        };

        var updateRequest = new UpdatePreferenceRequest
        {
            // All fields are null
        };

        // Act
        entity.ApplyUpdate(updateRequest);

        // Assert
        Assert.Equal(originalPrimaryChannel, entity.PrimaryChannelType);
        Assert.Equal(originalFallbackChannels, entity.FallbackChannelTypes);
        Assert.Equal(originalOptOutCategories, entity.OptOutCategories);
    }
}

