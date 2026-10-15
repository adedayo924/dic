using System.Security.Cryptography;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Security.Keys;

namespace DIC.ELibrary.Security.Cryptography;

/// <summary>
/// AES-256-GCM content cipher using the library content key. Ciphertext layout:
/// [12-byte nonce][16-byte tag][ciphertext].
/// </summary>
public sealed class AesGcmContentCipher : IContentCipher
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly ProtectedKeyStore _keyStore;

    public AesGcmContentCipher(ProtectedKeyStore keyStore) => _keyStore = keyStore;

    public byte[] Encrypt(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var key = _keyStore.GetContentKey();
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var output = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, output, NonceSize + TagSize, ciphertext.Length);
        return output;
    }

    public byte[] Decrypt(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        if (ciphertext.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Ciphertext is too short.");
        }

        var key = _keyStore.GetContentKey();
        var nonce = ciphertext.AsSpan(0, NonceSize);
        var tag = ciphertext.AsSpan(NonceSize, TagSize);
        var payload = ciphertext.AsSpan(NonceSize + TagSize);
        var plaintext = new byte[payload.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, payload, tag, plaintext);
        return plaintext;
    }
}
