using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Api.Extensions;
using Maliev.NotificationService.Api.Models.Enums;
using Maliev.NotificationService.Api.Models.Requests;
using Maliev.NotificationService.Api.Models.Responses;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Unit.Extensions;

public class TemplateExtensionsTests
{
    [Fact]
    public void ToResponse_WithValidEntity_MapsCorrectly()
    {
        // Arrange
        var entity = new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            TemplateKey = "order-confirmed",
            Version = 1,
            Language = "en",
            ChannelType = "email",
            ContentTemplate = "Hello {{name}}, order #{{orderId}} confirmed!",
            Parameters = new[] { "name", "orderId" },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var response = entity.ToResponse();

        // Assert
        Assert.NotNull(response);
        Assert.Equal(entity.Id, response.Id);
        Assert.Equal(entity.TemplateKey, response.TemplateKey);
        Assert.Equal(entity.Version, response.Version);
        Assert.Equal(entity.Language, response.Language);
        Assert.Equal(ChannelType.Email, response.ChannelType);
        Assert.Equal(entity.ContentTemplate, response.ContentTemplate);
        Assert.Equal(entity.Parameters, response.Parameters);
        Assert.Equal(entity.CreatedAt, response.CreatedAt);
        Assert.Equal(entity.UpdatedAt, response.UpdatedAt);
    }

    [Fact]
    public void ToEntity_FromCreateRequest_MapsCorrectly()
    {
        // Arrange
        var request = new CreateTemplateRequest
        {
            TemplateKey = "payment-failed",
            Version = 1,
            Language = "th",
            ChannelType = ChannelType.Sms,
            ContentTemplate = "การชำระเงินของคุณ {{amount}} บาท ล้มเหลว",
            Parameters = new[] { "amount" }
        };

        // Act
        var entity = request.ToEntity();

        // Assert
        Assert.NotNull(entity);
        Assert.NotEqual(Guid.Empty, entity.Id); // BaseEntity auto-generates GUID
        Assert.Equal(request.TemplateKey, entity.TemplateKey);
        Assert.Equal(request.Version, entity.Version);
        Assert.Equal(request.Language, entity.Language);
        Assert.Equal("sms", entity.ChannelType);
        Assert.Equal(request.ContentTemplate, entity.ContentTemplate);
        Assert.Equal(request.Parameters, entity.Parameters);
    }

    [Fact]
    public void ToEntity_FromUpdateRequest_UpdatesExistingEntity()
    {
        // Arrange
        var existingEntity = new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            TemplateKey = "system-outage",
            Version = 1,
            Language = "en",
            ChannelType = "email",
            ContentTemplate = "Old template",
            Parameters = new[] { "oldParam" },
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var updateRequest = new UpdateTemplateRequest
        {
            ContentTemplate = "System outage: {{reason}}. ETA: {{eta}}",
            Parameters = new[] { "reason", "eta" }
        };

        // Act
        updateRequest.ToEntity(existingEntity);

        // Assert
        Assert.Equal(updateRequest.ContentTemplate, existingEntity.ContentTemplate);
        Assert.Equal(updateRequest.Parameters, existingEntity.Parameters);
        // Other properties should remain unchanged
        Assert.Equal("system-outage", existingEntity.TemplateKey);
        Assert.Equal(1, existingEntity.Version);
    }

    [Fact]
    public void ToResponse_WithEmptyParameters_ReturnsEmptyArray()
    {
        // Arrange
        var entity = new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            TemplateKey = "simple-message",
            Version = 1,
            Language = "en",
            ChannelType = "email",
            ContentTemplate = "Static message with no parameters",
            Parameters = Array.Empty<string>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var response = entity.ToResponse();

        // Assert
        Assert.NotNull(response.Parameters);
        Assert.Empty(response.Parameters);
    }

    [Fact]
    public void ToEntity_WithMultipleParameters_PreservesOrder()
    {
        // Arrange
        var request = new CreateTemplateRequest
        {
            TemplateKey = "multi-param",
            Version = 1,
            Language = "en",
            ChannelType = ChannelType.Email,
            ContentTemplate = "{{param1}} {{param2}} {{param3}}",
            Parameters = new[] { "param1", "param2", "param3" }
        };

        // Act
        var entity = request.ToEntity();

        // Assert
        Assert.Equal(3, entity.Parameters.Length);
        Assert.Equal("param1", entity.Parameters[0]);
        Assert.Equal("param2", entity.Parameters[1]);
        Assert.Equal("param3", entity.Parameters[2]);
    }
}
