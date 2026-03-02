using Maliev.NotificationService.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Unit.Services;

public class EncryptionServiceTests
{
    // 32 zero bytes → valid 256-bit key
    private static string ValidKeyBase64 => Convert.ToBase64String(new byte[32]);

    private static EncryptionService CreateService(string? keyBase64 = null)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Encryption:DataProtectionKey"]).Returns(keyBase64 ?? ValidKeyBase64);
        var logger = new Mock<ILogger<EncryptionService>>();
        return new EncryptionService(config.Object, logger.Object);
    }

    [Fact]
    public void Constructor_WhenKeyMissing_ThrowsInvalidOperationException()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Encryption:DataProtectionKey"]).Returns((string?)null);
        var logger = new Mock<ILogger<EncryptionService>>();
        Assert.Throws<InvalidOperationException>(() => new EncryptionService(config.Object, logger.Object));
    }

    [Fact]
    public void Constructor_WhenKeyIsEmpty_ThrowsInvalidOperationException()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Encryption:DataProtectionKey"]).Returns("");
        var logger = new Mock<ILogger<EncryptionService>>();
        Assert.Throws<InvalidOperationException>(() => new EncryptionService(config.Object, logger.Object));
    }

    [Fact]
    public void Constructor_WhenKeyIsInvalidBase64_ThrowsInvalidOperationException()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Encryption:DataProtectionKey"]).Returns("not-valid-base64!!!");
        var logger = new Mock<ILogger<EncryptionService>>();
        Assert.Throws<InvalidOperationException>(() => new EncryptionService(config.Object, logger.Object));
    }

    [Fact]
    public void Constructor_WhenKeyIsWrongSize_ThrowsInvalidOperationException()
    {
        var shortKey = Convert.ToBase64String(new byte[16]); // 16 bytes = 128 bits, not 256
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Encryption:DataProtectionKey"]).Returns(shortKey);
        var logger = new Mock<ILogger<EncryptionService>>();
        Assert.Throws<InvalidOperationException>(() => new EncryptionService(config.Object, logger.Object));
    }

    [Fact]
    public void Encrypt_EmptyString_ReturnsEmpty()
    {
        var service = CreateService();
        Assert.Equal("", service.Encrypt(""));
    }

    [Fact]
    public void Decrypt_EmptyString_ReturnsEmpty()
    {
        var service = CreateService();
        Assert.Equal("", service.Decrypt(""));
    }

    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginal()
    {
        var service = CreateService();
        var original = "Hello, World! Test encryption.";
        var encrypted = service.Encrypt(original);
        var decrypted = service.Decrypt(encrypted);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Encrypt_DifferentCallsProduceDifferentCiphertexts()
    {
        var service = CreateService();
        var plaintext = "same text";
        var enc1 = service.Encrypt(plaintext);
        var enc2 = service.Encrypt(plaintext);
        Assert.NotEqual(enc1, enc2); // Different nonces
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsInvalidOperationException()
    {
        var service = CreateService();
        var encrypted = service.Encrypt("test data");
        var bytes = Convert.FromBase64String(encrypted);
        bytes[15] ^= 0xFF; // Tamper with ciphertext bytes
        var tampered = Convert.ToBase64String(bytes);
        Assert.Throws<InvalidOperationException>(() => service.Decrypt(tampered));
    }

    [Fact]
    public void Decrypt_TooShortInput_ThrowsInvalidOperationException()
    {
        var service = CreateService();
        // NonceSize (12) + TagSize (16) = 28 bytes minimum; use 10 bytes
        var tooShort = Convert.ToBase64String(new byte[10]);
        Assert.Throws<InvalidOperationException>(() => service.Decrypt(tooShort));
    }

    [Fact]
    public void Decrypt_InvalidBase64_ThrowsInvalidOperationException()
    {
        var service = CreateService();
        Assert.Throws<InvalidOperationException>(() => service.Decrypt("not-valid-base64!!!"));
    }

    [Fact]
    public void Encrypt_Unicode_RoundTripsCorrectly()
    {
        var service = CreateService();
        var original = "สวัสดี ครับ! Hello 🌍";
        var encrypted = service.Encrypt(original);
        Assert.NotEqual(original, encrypted);
        Assert.Equal(original, service.Decrypt(encrypted));
    }
}
