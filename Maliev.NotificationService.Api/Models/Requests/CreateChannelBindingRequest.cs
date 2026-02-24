using System.ComponentModel.DataAnnotations;

namespace Maliev.NotificationService.Api.Models.Requests;

/// <summary>
/// Request model for creating a channel binding
/// </summary>
public class CreateChannelBindingRequest
{
    /// <summary>
    /// User identifier
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Channel type (email, line, whatsapp, sms, slack, facebook, instagram)
    /// </summary>
    [Required]
    [MaxLength(50)]
    [RegularExpression("^(email|line|whatsapp|sms|slack|facebook|instagram)$",
        ErrorMessage = "Invalid channel type. Must be one of: email, line, whatsapp, sms, slack, facebook, instagram")]
    public string ChannelType { get; set; } = string.Empty;

    /// <summary>
    /// Channel-specific identifier (email address, phone number, LINE ID, etc.)
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string ChannelIdentifier { get; set; } = string.Empty;
}
