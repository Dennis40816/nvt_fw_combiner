using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Application.Tests.VersionManagement;

/// <summary>Tests the one canonical stable application-version identity.</summary>
public sealed class ManagedAppVersionTests
{
    /// <summary>Canonical stable three-component values round-trip unchanged.</summary>
    [Theory]
    [InlineData("0.0.0")]
    [InlineData("0.10.6")]
    [InlineData("1.0.0")]
    [InlineData("12.345.6789")]
    public void CanonicalStableVersionRoundTrips(string value)
    {
        ManagedAppVersion version = ManagedAppVersion.Parse(value);

        Assert.Equal(value, version.ToString());
    }

    /// <summary>Aliases and non-stable SemVer shapes never acquire a managed identity.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("01.0.0")]
    [InlineData("1.00.0")]
    [InlineData("1.0.00")]
    [InlineData("1.0.0.0")]
    [InlineData("v1.0.0")]
    [InlineData("1.0.0-beta.1")]
    [InlineData("1.0.0+build.1")]
    [InlineData("1.0.0 ")]
    [InlineData("-1.0.0")]
    [InlineData("1.a.0")]
    public void NonCanonicalOrNonStableVersionFailsClosed(string? value)
    {
        Assert.False(ManagedAppVersion.TryParse(value, out _));
    }

    /// <summary>The throwing parser reports a malformed public version as a format error.</summary>
    [Fact]
    public void ParseRejectsMalformedVersion()
    {
        _ = Assert.Throws<FormatException>(() => _ = ManagedAppVersion.Parse("v1.0.0"));
    }

    /// <summary>Version precedence compares major, then minor, then patch numerically.</summary>
    [Fact]
    public void VersionOrderingUsesNumericComponents()
    {
        ManagedAppVersion[] versions =
        [
            ManagedAppVersion.Parse("1.0.0"),
            ManagedAppVersion.Parse("0.10.10"),
            ManagedAppVersion.Parse("0.10.2"),
            ManagedAppVersion.Parse("0.9.99"),
        ];

        Assert.Equal(
            ["0.9.99", "0.10.2", "0.10.10", "1.0.0"],
            versions.Order().Select(version => version.ToString()));

        ManagedAppVersion lower = ManagedAppVersion.Parse("1.9.99");
        ManagedAppVersion higher = ManagedAppVersion.Parse("1.10.0");
        ManagedAppVersion equal = ManagedAppVersion.Parse("1.10.0");
        Assert.True(lower < higher);
        Assert.True(higher > lower);
        Assert.True(lower <= higher);
        Assert.True(higher >= lower);
        Assert.True(higher <= equal);
        Assert.True(higher >= equal);
        Assert.Equal(0, higher.CompareTo(equal));
    }
}
