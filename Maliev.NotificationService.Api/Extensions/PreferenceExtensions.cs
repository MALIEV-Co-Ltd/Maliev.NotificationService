using System.Text.Json;
using Maliev.NotificationService.Data.Entities;
using Maliev.NotificationService.Api.Models.Requests;
using Maliev.NotificationService.Api.Models.Responses;

namespace Maliev.NotificationService.Api.Extensions;

/// <summary>
/// Extension methods for mapping UserNotificationPreference to/from request/response models
/// </summary>
public static class PreferenceExtensions
{
    /// <summary>
    /// Converts UserNotificationPreference entity to PreferenceResponse
    /// </summary>
        public static PreferenceResponse ToResponse(this UserNotificationPreference preference)
        {
            return new PreferenceResponse
            {
                UserId = preference.UserId,
                PrimaryChannelType = preference.PrimaryChannelType,
                FallbackChannelTypes = preference.FallbackChannelTypes,
                OptOutCategories = preference.OptOutCategories,
                CreatedAt = preference.CreatedAt,
                UpdatedAt = preference.UpdatedAt
            };
        }
    
        /// <summary>
        /// Converts CreatePreferenceRequest to UserNotificationPreference entity
        /// </summary>
        public static UserNotificationPreference ToEntity(this CreatePreferenceRequest request)
        {
            return new UserNotificationPreference
            {
                UserId = request.UserId,
                PrimaryChannelType = request.PrimaryChannelType.ToLowerInvariant(),
                FallbackChannelTypes = request.FallbackChannelTypes.Select(c => c.ToLowerInvariant()).ToList(),
                OptOutCategories = request.OptOutCategories
            };
        }
    
        /// <summary>
        /// Updates UserNotificationPreference entity from UpdatePreferenceRequest
        /// </summary>
        public static void ApplyUpdate(this UserNotificationPreference preference, UpdatePreferenceRequest request)
        {
            if (request.PrimaryChannelType != null)
            {
                preference.PrimaryChannelType = request.PrimaryChannelType.ToLowerInvariant();
            }
    
            if (request.FallbackChannelTypes != null)
            {
                preference.FallbackChannelTypes = request.FallbackChannelTypes.Select(c => c.ToLowerInvariant()).ToList();
            }
    
            if (request.OptOutCategories != null)
            {
                preference.OptOutCategories = request.OptOutCategories;
            }
        }
    }
