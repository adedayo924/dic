namespace DIC.ELibrary.Core.Abstractions;

/// <summary>Symmetric content cipher (AES-256-GCM). Self-describing ciphertext (nonce + tag + payload).</summary>
public interface IContentCipher
{
    byte[] Encrypt(byte[] plaintext);
    byte[] Decrypt(byte[] ciphertext);
}

/// <summary>RSA-PSS signer/verifier used to authenticate packages and catalog bundles.</summary>
public interface IPackageSigner
{
    bool HasPrivateKey { get; }
    byte[] Sign(byte[] data);
    bool Verify(byte[] data, byte[] signature);
}

/// <summary>Administrative key management (export/import public key, rotate signing key).</summary>
public interface IKeyManager
{
    bool HasSigningKey { get; }
    string PublicKeyFingerprint { get; }
    string ExportPublicKeyPem();
    void ImportPublicKeyPem(string pem);
    void RegenerateSigningKey();
    string ContentKeyFingerprint { get; }
}
