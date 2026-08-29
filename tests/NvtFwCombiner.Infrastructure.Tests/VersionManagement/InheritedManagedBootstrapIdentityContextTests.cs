using System.Diagnostics;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Infrastructure.VersionManagement;

namespace NvtFwCombiner.Infrastructure.Tests.VersionManagement;

/// <summary>Verifies bounded inherited Root Bootstrap authority transport.</summary>
public sealed class InheritedManagedBootstrapIdentityContextTests
{
    private static readonly ManagedImmutableBootstrapIdentity ExactIdentity = new(
        "NvtFwCombiner.Bootstrap.exe",
        12_345,
        new string('a', 64));

    /// <summary>One exact identity is propagated and captured without loss.</summary>
    [Fact]
    public void ExactIdentityRoundTripsThroughOneBoundedProcessContext()
    {
        var startInfo = new ProcessStartInfo { UseShellExecute = false };

        InheritedManagedBootstrapIdentityContext.Apply(startInfo, ExactIdentity);
        string serialized = Assert.IsType<string>(
            startInfo.Environment[InheritedManagedBootstrapIdentityContext.EnvironmentName]);
        string? cleared = null;

        ManagedImmutableBootstrapIdentity? captured =
            InheritedManagedBootstrapIdentityContext.CaptureAndClear(
                _ => serialized,
                (_, value) => cleared = value);

        Assert.Equal(ExactIdentity, captured);
        Assert.Null(cleared);
        Assert.InRange(serialized.Length, 1, 128);
    }

    /// <summary>Missing or malformed context never creates restart authority.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2|NvtFwCombiner.Bootstrap.exe|12345|aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("1|Other.exe|12345|aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("1|NvtFwCombiner.Bootstrap.exe|0|aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("1|NvtFwCombiner.Bootstrap.exe|12345|AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void MissingOrMalformedContextClearsAndReturnsNoAuthority(string? serialized)
    {
        bool cleared = false;

        ManagedImmutableBootstrapIdentity? captured =
            InheritedManagedBootstrapIdentityContext.CaptureAndClear(
                _ => serialized,
                (_, value) => cleared = value is null);

        Assert.Null(captured);
        Assert.True(cleared);
    }

    /// <summary>Oversized inherited values are cleared and rejected.</summary>
    [Fact]
    public void OversizedContextIsRejectedAfterClear()
    {
        bool cleared = false;

        ManagedImmutableBootstrapIdentity? captured =
            InheritedManagedBootstrapIdentityContext.CaptureAndClear(
                _ => new string('x', 129),
                (_, value) => cleared = value is null);

        Assert.Null(captured);
        Assert.True(cleared);
    }
}
