using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Security.Keys;

namespace DIC.ELibrary.Security.Cryptography;

/// <summary>Exposes administrative key management over the protected key store.</summary>
public sealed class RsaKeyManager : IKeyManager
{
    private readonly ProtectedKeyStore _keyStore;

    public RsaKeyManager(ProtectedKeyStore keyStore) => _keyStore = keyStore;

    public bool HasSigningKey => _keyStore.HasSigningKey;

    public string PublicKeyFingerprint => _keyStore.PublicKeyFingerprint();

    public string ContentKeyFingerprint => _keyStore.ContentKeyFingerprint();

    public string ExportPublicKeyPem() => _keyStore.ExportPublicKeyPem();

    public void ImportPublicKeyPem(string pem) => _keyStore.ImportPublicKeyPem(pem);

    public void RegenerateSigningKey() => _keyStore.RegenerateSigningKey();
}
