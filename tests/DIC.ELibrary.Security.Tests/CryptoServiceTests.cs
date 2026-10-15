using System.Security.Cryptography;
using DIC.ELibrary.Core.Options;
using DIC.ELibrary.Security.Cryptography;
using DIC.ELibrary.Security.Keys;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.Security.Tests;

public sealed class CryptoServiceTests : IDisposable
{
    private readonly string _directory;
    private readonly ProtectedKeyStore _keyStore;

    public CryptoServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "dic-eltests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);

        var library = Options.Create(new LibraryOptions { DataDirectory = _directory });
        var security = Options.Create(new SecurityOptions
        {
            KeyStoreDirectory = Path.Combine(_directory, "keys"),
            RsaKeySize = 2048
        });
        _keyStore = new ProtectedKeyStore(library, security, NullLogger<ProtectedKeyStore>.Instance);
    }

    [Fact]
    public void ContentCipher_RoundTripsPayload()
    {
        var cipher = new AesGcmContentCipher(_keyStore);
        var plaintext = "The quick brown fox"u8.ToArray();

        var ciphertext = cipher.Encrypt(plaintext);

        Assert.NotEqual(plaintext, ciphertext);
        Assert.Equal(plaintext, cipher.Decrypt(ciphertext));
    }

    [Fact]
    public void ContentCipher_ProducesUniqueNonceEachTime()
    {
        var cipher = new AesGcmContentCipher(_keyStore);
        var plaintext = "data"u8.ToArray();

        Assert.NotEqual(cipher.Encrypt(plaintext), cipher.Encrypt(plaintext));
    }

    [Fact]
    public void ContentCipher_DetectsTampering()
    {
        var cipher = new AesGcmContentCipher(_keyStore);
        var ciphertext = cipher.Encrypt("secret"u8.ToArray());
        ciphertext[^1] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => cipher.Decrypt(ciphertext));
    }

    [Fact]
    public void ContentCipher_RejectsTruncatedInput()
    {
        var cipher = new AesGcmContentCipher(_keyStore);

        Assert.Throws<CryptographicException>(() => cipher.Decrypt(new byte[4]));
    }

    [Fact]
    public void PackageSigner_SignAndVerify_RoundTrips()
    {
        var signer = new RsaPackageSigner(_keyStore);
        var data = "manifest"u8.ToArray();

        var signature = signer.Sign(data);

        Assert.True(signer.HasPrivateKey);
        Assert.NotEmpty(signature);
        Assert.True(signer.Verify(data, signature));
    }

    [Fact]
    public void PackageSigner_RejectsTamperedData()
    {
        var signer = new RsaPackageSigner(_keyStore);
        var data = "manifest"u8.ToArray();
        var signature = signer.Sign(data);
        var tampered = "manifesu"u8.ToArray();

        Assert.False(signer.Verify(tampered, signature));
        Assert.False(signer.Verify(data, new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void KeyManager_ReportsFingerprints_AndRotatesKey()
    {
        var manager = new RsaKeyManager(_keyStore);

        var original = manager.PublicKeyFingerprint;
        Assert.NotEmpty(original);
        Assert.NotEmpty(manager.ContentKeyFingerprint);

        manager.RegenerateSigningKey();

        Assert.True(manager.HasSigningKey);
        Assert.NotEqual(original, manager.PublicKeyFingerprint);
    }

    [Fact]
    public void KeyManager_ExportsAndImportsPublicKey()
    {
        var manager = new RsaKeyManager(_keyStore);
        var original = manager.PublicKeyFingerprint;
        var pem = manager.ExportPublicKeyPem();

        Assert.Contains("PUBLIC KEY", pem);

        manager.ImportPublicKeyPem(pem);
        Assert.Equal(original, manager.PublicKeyFingerprint);
    }

    [Fact]
    public void ImportPublicKey_RejectsEmptyPem()
    {
        var manager = new RsaKeyManager(_keyStore);

        Assert.Throws<ArgumentException>(() => manager.ImportPublicKeyPem(""));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }
}
