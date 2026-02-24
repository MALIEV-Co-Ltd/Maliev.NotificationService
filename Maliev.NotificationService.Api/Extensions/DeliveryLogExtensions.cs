using Maliev.NotificationService.Api.Models.Responses;
using Maliev.NotificationService.Data.Entities;

namespace Maliev.NotificationService.Api.Extensions;

/// <summary>
/// Extension methods for mapping DeliveryLog to DeliveryLogResponse
/// </summary>
public static class DeliveryLogExtensions
{
    /// <summary>
    /// Converts DeliveryLog entity to DeliveryLogResponse
    /// </summary>
    public static DeliveryLogResponse ToResponse(this DeliveryLog log)
    {
        return new DeliveryLogResponse
        {
            Id = log.Id,
            EventId = log.EventId,
            UserId = log.UserId,
            ChannelType = log.ChannelType,
            RecipientIdentifier = log.RecipientIdentifier,
            Status = log.Status,
            MessageContent = log.MessageContent,
            ProviderMessageId = log.ProviderMessageId,
            AttemptNumber = log.AttemptNumber,
            DeliveredAt = log.DeliveredAt,
            CreatedAt = log.CreatedAt
        };
    }
}
