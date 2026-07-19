using System.ComponentModel.DataAnnotations;

namespace Maliev.NotificationService.Api.Models.Requests;

/// <summary>
/// Request model for updating a channel binding
/// </summary>
public class UpdateChannelBindingRequest
{
    /// <summary>
    /// Channel-specific identifier (email address, phone number, LINE ID, etc.)
    /// </summary>
    [MaxLength(500)]
    public string? ChannelIdentifier { get; set; }

    /// <summary>
    /// Whether the channel binding is valid
    /// </summary>
    public bool? IsValid { get; set; }

    /// <summary>
    /// Reason for invalidation (if IsValid is false)
    /// </summary>
    [MaxLength(1000)]
    public string? InvalidatedReason { get; set; }
}
