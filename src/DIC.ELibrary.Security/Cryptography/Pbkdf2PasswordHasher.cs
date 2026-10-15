using System.Security.Cryptography;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Options;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.Security.Cryptography;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hasher. Encoded format: <c>PBKDF2$iterations$salt$hash</c> (base64 parts).
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Prefix = "PBKDF2";
    private readonly int _iterations;
    private readonly int _saltSize;
    private readonly int _keySize;

    public Pbkdf2PasswordHasher(IOptions<SecurityOptions> options)
    {
        _iterations = options.Value.PasswordIterations;
        _saltSize = options.Value.PasswordSaltSize;
        _keySize = options.Value.PasswordKeySize;
    }

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(_saltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, HashAlgorithmName.SHA256, _keySize);
        return $"{Prefix}${_iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        var parts = hash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public bool NeedsRehash(string hash)
    {
        var parts = hash.Split('$');
        return parts.Length != 4 || !int.TryParse(parts[1], out var iterations) || iterations != _iterations;
    }
}
