namespace Maliev.NotificationService.Api.Services;

/// <summary>
/// Service for encrypting and decrypting sensitive data at rest.
/// Uses AES-256-GCM for authenticated encryption.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts plaintext data using AES-256-GCM.
    /// </summary>
    /// <param name="plaintext">The plaintext string to encrypt</param>
    /// <returns>Base64-encoded encrypted data with nonce and tag</returns>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts ciphertext data using AES-256-GCM.
    /// </summary>
    /// <param name="ciphertext">Base64-encoded encrypted data with nonce and tag</param>
    /// <returns>Decrypted plaintext string</returns>
    string Decrypt(string ciphertext);
}
