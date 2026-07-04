using ChatApi.Security;
using Xunit;

namespace ChatApi.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        var hash = PasswordHasher.Hash("secret123");
        Assert.True(PasswordHasher.Verify("secret123", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForWrongPassword()
    {
        var hash = PasswordHasher.Hash("secret123");
        Assert.False(PasswordHasher.Verify("wrong", hash));
    }

    [Fact]
    public void Hash_ProducesDifferentHashes_ForSamePassword()
    {
        // Random salt => different stored hashes each time.
        Assert.NotEqual(PasswordHasher.Hash("abc"), PasswordHasher.Hash("abc"));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForMalformedHash()
    {
        Assert.False(PasswordHasher.Verify("abc", "not-a-valid-hash"));
    }
}
