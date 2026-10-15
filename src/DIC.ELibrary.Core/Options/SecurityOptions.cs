namespace DIC.ELibrary.Core.Options;

public class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>PBKDF2 iteration count for password hashing.</summary>
    public int PasswordIterations { get; set; } = 210_000;

    public int PasswordSaltSize { get; set; } = 16;
    public int PasswordKeySize { get; set; } = 32;

    /// <summary>Directory used to persist DPAPI-protected key material. Defaults under the data directory.</summary>
    public string KeyStoreDirectory { get; set; } = string.Empty;

    /// <summary>File name of the RSA private key used to sign packages (admin only).</summary>
    public string SigningKeyFileName { get; set; } = "signing-key.dpapi";

    /// <summary>File name of the RSA public key used to verify packages.</summary>
    public string PublicKeyFileName { get; set; } = "verification-key.pub";

    /// <summary>RSA key size for generated signing keys.</summary>
    public int RsaKeySize { get; set; } = 2048;

    /// <summary>Seed administrator username created on first run.</summary>
    public string DefaultAdminUsername { get; set; } = "admin";

    /// <summary>Seed administrator password created on first run.</summary>
    public string DefaultAdminPassword { get; set; } = "Admin@123";

    /// <summary>Lock an account after this many consecutive failed logins. 0 disables lockout.</summary>
    public int MaxFailedAttempts { get; set; } = 5;
}
