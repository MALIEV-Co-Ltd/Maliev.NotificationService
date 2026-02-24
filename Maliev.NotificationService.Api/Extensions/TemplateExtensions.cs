using Maliev.NotificationService.Api.Models.Enums;
using Maliev.NotificationService.Api.Models.Requests;
using Maliev.NotificationService.Api.Models.Responses;
using Maliev.NotificationService.Data.Entities;

namespace Maliev.NotificationService.Api.Extensions;

/// <summary>
/// Extension methods for converting NotificationTemplate entities to/from DTOs
/// </summary>
public static class TemplateExtensions
{
    /// <summary>
    /// Converts NotificationTemplate entity to TemplateResponse DTO
    /// </summary>
    public static TemplateResponse ToResponse(this NotificationTemplate entity)
    {
        return new TemplateResponse
        {
            Id = entity.Id,
            TemplateKey = entity.TemplateKey,
            Version = entity.Version,
            Language = entity.Language,
            ChannelType = Enum.Parse<ChannelType>(entity.ChannelType, ignoreCase: true),
            ContentTemplate = entity.ContentTemplate,
            Parameters = entity.Parameters,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    /// <summary>
    /// Converts CreateTemplateRequest DTO to NotificationTemplate entity
    /// </summary>
    public static NotificationTemplate ToEntity(this CreateTemplateRequest request)
    {
        return new NotificationTemplate
        {
            TemplateKey = request.TemplateKey,
            Version = request.Version,
            Language = request.Language,
            ChannelType = request.ChannelType.ToString().ToLowerInvariant(),
            ContentTemplate = request.ContentTemplate,
            Parameters = request.Parameters
        };
    }

    /// <summary>
    /// Updates an existing NotificationTemplate entity from UpdateTemplateRequest DTO
    /// </summary>
    public static void ToEntity(this UpdateTemplateRequest request, NotificationTemplate entity)
    {
        entity.ContentTemplate = request.ContentTemplate;
        entity.Parameters = request.Parameters;
    }
}
