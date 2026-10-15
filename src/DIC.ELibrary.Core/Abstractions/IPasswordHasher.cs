namespace DIC.ELibrary.Core.Abstractions;

/// <summary>Hashes and verifies user passwords. Implemented by the Security layer.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
    bool NeedsRehash(string hash);
}
