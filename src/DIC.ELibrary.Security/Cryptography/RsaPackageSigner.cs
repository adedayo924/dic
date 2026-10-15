using System.Security.Cryptography;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Security.Keys;

namespace DIC.ELibrary.Security.Cryptography;

/// <summary>RSA-PSS (SHA-256) signer that reads keys from the protected key store.</summary>
public sealed class RsaPackageSigner : IPackageSigner
{
    private readonly ProtectedKeyStore _keyStore;

    public RsaPackageSigner(ProtectedKeyStore keyStore) => _keyStore = keyStore;

    public bool HasPrivateKey => _keyStore.HasSigningKey;

    public byte[] Sign(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var rsa = _keyStore.GetSigningPrivateKey();
        return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
    }

    public bool Verify(byte[] data, byte[] signature)
    {
        if (data is null || signature is null || signature.Length == 0)
        {
            return false;
        }

        try
        {
            using var rsa = _keyStore.GetPublicKey();
            return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
