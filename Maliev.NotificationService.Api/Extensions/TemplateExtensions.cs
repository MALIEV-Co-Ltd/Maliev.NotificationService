using Maliev.NotificationService.Api.Models.Enums;
using Maliev.NotificationService.Api.Models.Requests;
using Maliev.NotificationService.Api.Models.Responses;
using Maliev.NotificationService.Domain.Entities;

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
            DisplayName = string.IsNullOrWhiteSpace(entity.DisplayName) ? entity.TemplateKey : entity.DisplayName,
            Version = entity.Version,
            Language = entity.Language,
            ChannelType = Enum.Parse<ChannelType>(entity.ChannelType, ignoreCase: true),
            SubjectTemplate = entity.SubjectTemplate,
            ContentTemplate = entity.ContentTemplate,
            IsActive = entity.IsActive,
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
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? request.TemplateKey
                : request.DisplayName.Trim(),
            Version = request.Version,
            Language = request.Language,
            ChannelType = request.ChannelType.ToString().ToLowerInvariant(),
            SubjectTemplate = request.SubjectTemplate,
            ContentTemplate = request.ContentTemplate,
            IsActive = request.IsActive,
            Parameters = request.Parameters
        };
    }

    /// <summary>
    /// Updates an existing NotificationTemplate entity from UpdateTemplateRequest DTO
    /// </summary>
    public static void ToEntity(this UpdateTemplateRequest request, NotificationTemplate entity)
    {
        if (request.DisplayName is not null)
        {
            entity.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? entity.TemplateKey
                : request.DisplayName.Trim();
        }

        if (request.SubjectTemplate is not null)
        {
            entity.SubjectTemplate = request.SubjectTemplate;
        }

        if (request.IsActive is bool isActive)
        {
            entity.IsActive = isActive;
        }

        entity.ContentTemplate = request.ContentTemplate;
        entity.Parameters = request.Parameters;
    }
}
