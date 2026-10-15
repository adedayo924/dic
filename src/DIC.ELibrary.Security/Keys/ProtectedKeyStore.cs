using System.Security.Cryptography;
using DIC.ELibrary.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.Security.Keys;

/// <summary>
/// Persists cryptographic key material on disk. Secrets are protected with Windows DPAPI
/// (CurrentUser scope) so they are only usable by the signed-in Windows account.
/// </summary>
public sealed class ProtectedKeyStore
{
    private static readonly byte[] Entropy = "DIC.ELibrary.key-protection.v1"u8.ToArray();

    private readonly string _directory;
    private readonly int _rsaKeySize;
    private readonly ILogger<ProtectedKeyStore> _logger;
    private readonly object _gate = new();

    public ProtectedKeyStore(IOptions<LibraryOptions> library, IOptions<SecurityOptions> security, ILogger<ProtectedKeyStore> logger)
    {
        var sec = security.Value;
        _directory = string.IsNullOrWhiteSpace(sec.KeyStoreDirectory)
            ? Path.Combine(library.Value.ResolvedDataDirectory, "keys")
            : sec.KeyStoreDirectory;
        _rsaKeySize = sec.RsaKeySize;
        _logger = logger;
        Directory.CreateDirectory(_directory);
    }

    private string ContentKeyPath => Path.Combine(_directory, "content.key");
    private string SigningKeyPath => Path.Combine(_directory, "signing.key");
    private string PublicKeyPath => Path.Combine(_directory, "verification.pub");

    public byte[] GetContentKey()
    {
        lock (_gate)
        {
            if (File.Exists(ContentKeyPath))
            {
                try
                {
                    return Unprotect(File.ReadAllBytes(ContentKeyPath));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to read content key; regenerating.");
                }
            }

            var key = RandomNumberGenerator.GetBytes(32);
            File.WriteAllBytes(ContentKeyPath, Protect(key));
            _logger.LogInformation("Generated a new library content key.");
            return key;
        }
    }

    public RSA GetSigningPrivateKey()
    {
        lock (_gate)
        {
            var rsa = RSA.Create(_rsaKeySize);
            if (File.Exists(SigningKeyPath))
            {
                try
                {
                    var pkcs8 = Unprotect(File.ReadAllBytes(SigningKeyPath));
                    rsa.ImportPkcs8PrivateKey(pkcs8, out _);
                    return rsa;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to read signing key; regenerating.");
                }
            }

            EnsurePublicKeyFile(rsa);
            File.WriteAllBytes(SigningKeyPath, Protect(rsa.ExportPkcs8PrivateKey()));
            _logger.LogInformation("Generated a new RSA package signing key.");
            return rsa;
        }
    }

    public RSA GetPublicKey()
    {
        lock (_gate)
        {
            var rsa = RSA.Create();
            if (File.Exists(PublicKeyPath))
            {
                rsa.ImportFromPem(File.ReadAllText(PublicKeyPath));
                return rsa;
            }

            if (File.Exists(SigningKeyPath))
            {
                var priv = GetSigningPrivateKey();
                EnsurePublicKeyFile(priv);
                rsa.ImportFromPem(File.ReadAllText(PublicKeyPath));
                return rsa;
            }

            return GetSigningPrivateKey();
        }
    }

    public string ExportPublicKeyPem()
    {
        lock (_gate)
        {
            if (File.Exists(PublicKeyPath))
            {
                return File.ReadAllText(PublicKeyPath);
            }
            using var rsa = GetPublicKey();
            var pem = rsa.ExportSubjectPublicKeyInfoPem();
            File.WriteAllText(PublicKeyPath, pem);
            return pem;
        }
    }

    public void ImportPublicKeyPem(string pem)
    {
        if (string.IsNullOrWhiteSpace(pem))
        {
            throw new ArgumentException("Public key PEM is empty.", nameof(pem));
        }
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        lock (_gate)
        {
            File.WriteAllText(PublicKeyPath, pem);
        }
    }

    public void RegenerateSigningKey()
    {
        lock (_gate)
        {
            if (File.Exists(SigningKeyPath))
            {
                File.Delete(SigningKeyPath);
            }
            if (File.Exists(PublicKeyPath))
            {
                File.Delete(PublicKeyPath);
            }
        }
        using var _ = GetSigningPrivateKey();
    }

    public bool HasSigningKey => File.Exists(SigningKeyPath);

    public string PublicKeyFingerprint()
    {
        using var rsa = GetPublicKey();
        var der = rsa.ExportSubjectPublicKeyInfo();
        var hash = SHA256.HashData(der);
        return Convert.ToHexString(hash)[..16];
    }

    public string ContentKeyFingerprint()
    {
        var hash = SHA256.HashData(GetContentKey());
        return Convert.ToHexString(hash)[..16];
    }

    private void EnsurePublicKeyFile(RSA rsa)
    {
        if (!File.Exists(PublicKeyPath))
        {
            File.WriteAllText(PublicKeyPath, rsa.ExportSubjectPublicKeyInfoPem());
        }
    }

    private static byte[] Protect(byte[] data)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI key protection requires Windows.");
        }
        return ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
    }

    private static byte[] Unprotect(byte[] data)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI key protection requires Windows.");
        }
        return ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
    }
}
