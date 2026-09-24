namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Canonical DP Perspective classification remains independent of retired execution.</summary>
public sealed class BuiltInV2DpReplaceRoutingTests
{
    /// <summary>Verifies DP Perspective classification remains a map-shape fact, not generic DP Replace availability.</summary>
    [Theory]
    [InlineData("51950")]
    [InlineData("nt51951")]
    [InlineData("NT51950")]
    public void DpPerspectiveClassificationNormalizesRegisteredV2IcIds(string icId)
    {
        Assert.True(BootstrapTestHost.Services.CompositionCapabilityExperience
            .IsDpPerspectiveIc(icId));
    }

    /// <summary>Verifies empty ids and registered non-DP-Perspective ICs remain outside the DP Perspective family.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("NT51929")]
    public void DpPerspectiveClassificationReturnsFalseOutsidePerspectiveFamily(string icId)
    {
        Assert.False(BootstrapTestHost.Services.CompositionCapabilityExperience
            .IsDpPerspectiveIc(icId));
    }

}
