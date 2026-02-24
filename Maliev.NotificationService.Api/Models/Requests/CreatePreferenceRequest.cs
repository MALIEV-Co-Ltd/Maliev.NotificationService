using System.ComponentModel.DataAnnotations;

namespace Maliev.NotificationService.Api.Models.Requests;

/// <summary>
/// Request model for creating user notification preferences
/// </summary>
public class CreatePreferenceRequest
{
    /// <summary>
    /// User identifier
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Primary notification channel (email, line, whatsapp, sms, slack, facebook, instagram)
    /// </summary>
    [Required]
    [MaxLength(50)]
    [RegularExpression("^(email|line|whatsapp|sms|slack|facebook|instagram)$",
        ErrorMessage = "Invalid channel type. Must be one of: email, line, whatsapp, sms, slack, facebook, instagram")]
    public string PrimaryChannelType { get; set; } = string.Empty;

    /// <summary>
    /// Ordered list of fallback channels
    /// </summary>
    public List<string> FallbackChannelTypes { get; set; } = new();

    /// <summary>
    /// Notification categories to opt out of (e.g., "marketing", "promotions")
    /// </summary>
    public List<string> OptOutCategories { get; set; } = new();
}
