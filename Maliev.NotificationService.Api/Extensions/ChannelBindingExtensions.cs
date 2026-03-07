using Maliev.NotificationService.Api.Models.Requests;
using Maliev.NotificationService.Api.Models.Responses;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Domain.Entities;

namespace Maliev.NotificationService.Api.Extensions;

/// <summary>
/// Extension methods for mapping ChannelBinding to/from request/response models.
/// Handles encryption/decryption of sensitive channel identifiers.
/// </summary>
public static class ChannelBindingExtensions
{
    /// <summary>
    /// Converts ChannelBinding entity to ChannelBindingResponse with obfuscated identifier.
    /// Decrypts the stored encrypted identifier before obfuscation.
    /// </summary>
    /// <param name="binding">The channel binding entity</param>
    /// <param name="encryptionService">Encryption service for decrypting channel identifier</param>
    /// <returns>Channel binding response with obfuscated (decrypted) identifier</returns>
    public static ChannelBindingResponse ToResponse(this ChannelBinding binding, IEncryptionService encryptionService)
    {
        // Decrypt the channel identifier before obfuscating for display
        var decryptedIdentifier = encryptionService.Decrypt(binding.ChannelIdentifier);

        return new ChannelBindingResponse
        {
            Id = binding.Id,
            UserId = binding.UserId,
            ChannelType = binding.ChannelType,
            ChannelIdentifier = ObfuscateIdentifier(decryptedIdentifier),
            IsValid = binding.IsValid,
            InvalidatedAt = binding.InvalidatedAt,
            InvalidatedReason = binding.InvalidatedReason,
            CreatedAt = binding.CreatedAt,
            UpdatedAt = binding.UpdatedAt
        };
    }

    /// <summary>
    /// Converts CreateChannelBindingRequest to ChannelBinding entity.
    /// Encrypts the channel identifier before storage.
    /// </summary>
    /// <param name="request">The channel binding creation request</param>
    /// <param name="encryptionService">Encryption service for encrypting channel identifier</param>
    /// <returns>Channel binding entity with encrypted identifier</returns>
    public static ChannelBinding ToEntity(this CreateChannelBindingRequest request, IEncryptionService encryptionService)
    {
        // Encrypt the channel identifier before storing in database
        var encryptedIdentifier = encryptionService.Encrypt(request.ChannelIdentifier);

        return new ChannelBinding
        {
            UserId = request.UserId,
            ChannelType = request.ChannelType.ToLowerInvariant(),
            ChannelIdentifier = encryptedIdentifier,
            IsValid = true
        };
    }

    /// <summary>
    /// Updates ChannelBinding entity from UpdateChannelBindingRequest.
    /// Encrypts new channel identifier if provided.
    /// </summary>
    /// <param name="binding">The channel binding entity to update</param>
    /// <param name="request">Update request with new values</param>
    /// <param name="encryptionService">Encryption service for encrypting new channel identifier</param>
    public static void ApplyUpdate(this ChannelBinding binding, UpdateChannelBindingRequest request, IEncryptionService encryptionService)
    {
        if (request.ChannelIdentifier != null)
        {
            // Encrypt the new channel identifier before storing
            binding.ChannelIdentifier = encryptionService.Encrypt(request.ChannelIdentifier);
        }

        if (request.IsValid.HasValue)
        {
            binding.IsValid = request.IsValid.Value;

            if (!request.IsValid.Value)
            {
                binding.InvalidatedAt = DateTimeOffset.UtcNow;
                binding.InvalidatedReason = request.InvalidatedReason ?? "User requested";
            }
            else
            {
                // Re-validating the binding
                binding.InvalidatedAt = null;
                binding.InvalidatedReason = null;
            }
        }
    }

    /// <summary>
    /// Obfuscates channel identifier for security
    /// Examples:
    /// - Email: "user@example.com" → "u***@example.com"
    /// - Phone: "+66123456789" → "+661****6789"
    /// - LINE ID: "U1234567890" → "U1***7890"
    /// </summary>
    private static string ObfuscateIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return string.Empty;
        }

        // Email obfuscation
        if (identifier.Contains('@'))
        {
            var parts = identifier.Split('@');
            if (parts.Length == 2 && parts[0].Length > 0)
            {
                var localPart = parts[0];
                var obfuscatedLocal = localPart.Length > 1
                    ? $"{localPart[0]}***{localPart[^1]}"
                    : $"{localPart[0]}***";
                return $"{obfuscatedLocal}@{parts[1]}";
            }
        }

        // Phone number obfuscation (keep first 3 and last 4 digits)
        if (identifier.StartsWith('+') && identifier.Length > 7)
        {
            var prefix = identifier[..4]; // +661
            var suffix = identifier[^4..]; // 6789
            var middleLength = identifier.Length - 8;
            return $"{prefix}{new string('*', middleLength)}{suffix}";
        }

        // Generic obfuscation (keep first 2 and last 4 characters)
        if (identifier.Length > 6)
        {
            var prefix = identifier[..2];
            var suffix = identifier[^4..];
            return $"{prefix}***{suffix}";
        }

        // Too short to obfuscate meaningfully
        return $"{identifier[0]}***";
    }
}
