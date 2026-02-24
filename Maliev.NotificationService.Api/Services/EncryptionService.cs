using System.Security.Cryptography;
using System.Text;

namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Encryption service using AES-256-GCM for channel identifier encryption at rest.
/// Encryption key is retrieved from configuration (Google Secret Manager in production).
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private readonly ILogger<EncryptionService> _logger;

    // AES-256-GCM parameters
    private const int KeySize = 32; // 256 bits
    private const int NonceSize = 12; // 96 bits (recommended for GCM)
    private const int TagSize = 16; // 128 bits

    public EncryptionService(IConfiguration configuration, ILogger<EncryptionService> logger)
    {
        _logger = logger;

        // Retrieve encryption key from configuration
        // In production, this should come from Google Secret Manager
        var keyBase64 = configuration["Encryption:DataProtectionKey"];

        if (string.IsNullOrEmpty(keyBase64))
        {
            throw new InvalidOperationException(
                "Encryption key is missing. Please provide 'Encryption:DataProtectionKey' in configuration. " +
                "For local development, you can use a stable Base64-encoded 32-byte key.");
        }
        else
        {
            try
            {
                _key = Convert.FromBase64String(keyBase64);

                if (_key.Length != KeySize)
                {
                    throw new InvalidOperationException(
                        $"Encryption key must be {KeySize} bytes (256 bits). Got {_key.Length} bytes.");
                }

                _logger.LogInformation("Encryption service initialized with configured key");
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException(
                    "Invalid encryption key format in configuration. Must be Base64-encoded 32-byte key.", ex);
            }
        }
    }

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return plaintext;
        }

        try
        {
            using var aesGcm = new AesGcm(_key, TagSize);

            // Generate random nonce (must be unique for each encryption)
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var ciphertext = new byte[plaintextBytes.Length];
            var tag = new byte[TagSize];

            // Encrypt with authenticated encryption
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            // Combine nonce + ciphertext + tag and encode as Base64
            var combined = new byte[NonceSize + ciphertext.Length + TagSize];
            Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
            Buffer.BlockCopy(ciphertext, 0, combined, NonceSize, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, combined, NonceSize + ciphertext.Length, TagSize);

            return Convert.ToBase64String(combined);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting data");
            throw new InvalidOperationException("Failed to encrypt data", ex);
        }
    }

    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            return ciphertext;
        }

        try
        {
            var combined = Convert.FromBase64String(ciphertext);

            if (combined.Length < NonceSize + TagSize)
            {
                throw new InvalidOperationException("Invalid ciphertext: too short");
            }

            using var aesGcm = new AesGcm(_key, TagSize);

            // Extract nonce, ciphertext, and tag
            var nonce = new byte[NonceSize];
            var ciphertextBytes = new byte[combined.Length - NonceSize - TagSize];
            var tag = new byte[TagSize];

            Buffer.BlockCopy(combined, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(combined, NonceSize, ciphertextBytes, 0, ciphertextBytes.Length);
            Buffer.BlockCopy(combined, NonceSize + ciphertextBytes.Length, tag, 0, TagSize);

            // Decrypt and verify authentication tag
            var plaintext = new byte[ciphertextBytes.Length];
            aesGcm.Decrypt(nonce, ciphertextBytes, tag, plaintext);

            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Decryption failed: data may be tampered or key mismatch");
            throw new InvalidOperationException("Failed to decrypt data: authentication failed", ex);
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Invalid ciphertext format");
            throw new InvalidOperationException("Failed to decrypt data: invalid format", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting data");
            throw new InvalidOperationException("Failed to decrypt data", ex);
        }
    }
}
