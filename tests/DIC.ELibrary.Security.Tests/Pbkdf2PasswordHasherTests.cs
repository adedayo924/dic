using DIC.ELibrary.Core.Options;
using DIC.ELibrary.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.Security.Tests;

public class Pbkdf2PasswordHasherTests
{
    private static Pbkdf2PasswordHasher Create(int iterations = 1000) =>
        new(Options.Create(new SecurityOptions
        {
            PasswordIterations = iterations,
            PasswordSaltSize = 16,
            PasswordKeySize = 32
        }));

    [Fact]
    public void Hash_ProducesExpectedEncodedFormat()
    {
        var hasher = Create();

        var parts = hasher.Hash("s3cret").Split('$');

        Assert.Equal(4, parts.Length);
        Assert.Equal("PBKDF2", parts[0]);
        Assert.Equal("1000", parts[1]);
        Assert.NotEmpty(parts[2]);
        Assert.NotEmpty(parts[3]);
    }

    [Fact]
    public void Verify_AcceptsCorrectPassword_AndRejectsWrong()
    {
        var hasher = Create();
        var hash = hasher.Hash("s3cret");

        Assert.True(hasher.Verify("s3cret", hash));
        Assert.False(hasher.Verify("wrong", hash));
    }

    [Fact]
    public void Hash_IsSalted_SoIdenticalPasswordsDiffer()
    {
        var hasher = Create();

        Assert.NotEqual(hasher.Hash("same"), hasher.Hash("same"));
    }

    [Theory]
    [InlineData("not-a-hash")]
    [InlineData("")]
    [InlineData("PBKDF2$abc$AA$BB")]
    [InlineData("PBKDF2$1000$not-base64$)")]
    public void Verify_RejectsMalformedHashes(string hash)
    {
        var hasher = Create();

        Assert.False(hasher.Verify("x", hash));
    }

    [Fact]
    public void Verify_RejectsNullOrEmptyInputs()
    {
        var hasher = Create();

        Assert.False(hasher.Verify(string.Empty, hasher.Hash("pw")));
        Assert.False(hasher.Verify("pw", string.Empty));
    }

    [Fact]
    public void NeedsRehash_IsTrueWhenIterationCountDiffersOrHashInvalid()
    {
        var current = Create(2000);
        var legacyHash = Create(1000).Hash("pw");

        Assert.True(current.NeedsRehash(legacyHash));
        Assert.False(current.NeedsRehash(current.Hash("pw")));
        Assert.True(current.NeedsRehash("garbage"));
    }
}
