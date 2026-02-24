using System.ComponentModel.DataAnnotations;

namespace Maliev.NotificationService.Api.Models.Requests;

/// <summary>
/// Request model for updating user notification preferences
/// </summary>
public class UpdatePreferenceRequest
{
    /// <summary>
    /// Primary notification channel (email, line, whatsapp, sms, slack, facebook, instagram)
    /// </summary>
    [MaxLength(50)]
    [RegularExpression("^(email|line|whatsapp|sms|slack|facebook|instagram)$",
        ErrorMessage = "Invalid channel type. Must be one of: email, line, whatsapp, sms, slack, facebook, instagram")]
    public string? PrimaryChannelType { get; set; }

    /// <summary>
    /// Ordered list of fallback channels
    /// </summary>
    public List<string>? FallbackChannelTypes { get; set; }

    /// <summary>
    /// Notification categories to opt out of (e.g., "marketing", "promotions")
    /// </summary>
    public List<string>? OptOutCategories { get; set; }
}
