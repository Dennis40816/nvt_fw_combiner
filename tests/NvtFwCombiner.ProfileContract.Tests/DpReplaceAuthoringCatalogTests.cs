using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests DP Replace authoring policy that is not yet promoted to executable per-IC profiles.</summary>
public sealed class DpReplaceAuthoringCatalogTests
{
    /// <summary>Additional DP Replace payload slots are explicit catalog rows.</summary>
    [Fact]
    public void AdditionalPayloadRulesAreExplicitAndUnique()
    {
        DpReplaceAdditionalPayloadRule rule = Assert.Single(
            DpReplaceAuthoringCatalog.GetAdditionalPayloads("NT51928"));
        Assert.Equal("NT51928", rule.IcId);
        Assert.Equal("dp-ldc-51928", rule.RegionId);
        Assert.Equal("replace-ldc", rule.SlotId);
        Assert.Equal(CompositionAddressSpaceIds.LdcReplacement, rule.AddressSpaceId);
    }

    /// <summary>NT51928 LDC authoring does not leak to the shared NT51927 TP Overview profile.</summary>
    [Fact]
    public void Nt51928LdcPayloadRuleDoesNotLeakToNt51927()
    {
        Assert.True(DpReplaceAuthoringCatalog.IsAdditionalPayloadRegion("NT51928", "dp-ldc-51928"));
        Assert.True(DpReplaceAuthoringCatalog.TryGetAdditionalPayload(
            "51928",
            "dp-ldc-51928",
            out DpReplaceAdditionalPayloadRule? rule));
        Assert.Equal(CompositionAddressSpaceIds.LdcReplacement, rule.AddressSpaceId);

        Assert.False(DpReplaceAuthoringCatalog.IsAdditionalPayloadRegion("NT51927", "dp-ldc-51928"));
        Assert.Empty(DpReplaceAuthoringCatalog.GetAdditionalPayloads("NT51927"));
    }
}
