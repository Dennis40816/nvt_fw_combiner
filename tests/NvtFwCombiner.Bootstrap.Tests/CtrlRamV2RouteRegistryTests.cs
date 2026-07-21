using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks CtrlRAM V2 routing to profile and typed-plan authority.</summary>
public sealed class CtrlRamV2RouteRegistryTests
{
    /// <summary>Every declared route has one unique IC/effective-profile/plan authority key.</summary>
    [Fact]
    public void ProductionRouteKeysAreUnique()
    {
        Assert.Equal(21, CtrlRamV2RouteRegistry.All.Count);
        Assert.Equal(
            CtrlRamV2RouteRegistry.All.Count,
            CtrlRamV2RouteRegistry.All.Select(static route => route.Key).Distinct().Count());
    }

    /// <summary>NT51930 routing is selected by its effective profile and bounded cascade plan, not golden metadata.</summary>
    [Fact]
    public void Nt51930BoundedCascadePlanResolvesWithoutGoldenTuple()
    {
        LegacyCombinerPostbuildProfile profile = Assert.Single(
            LegacyCombinerPostbuildCatalog.GetProfiles("NT51930"));
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            profile,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, [WorkbenchIcNumberTokens.CascadeTwoToThirteen]));

        Assert.True(CtrlRamV2RouteRegistry.TryResolve(plan, out CtrlRamV2Route? route));
        Assert.NotNull(route);
        Assert.Equal("nt51930-ctrlram-replace-candidate", route.BundleId);
        Assert.Equal("nt51930-ctrlram-replace-fw130-cascade3", route.ProfileId);
    }

    /// <summary>The partial NT51928 authority stays restricted to its owner-declared two-chip route.</summary>
    [Fact]
    public void Nt51928SinglePlanHasNoV2Route()
    {
        LegacyCombinerPostbuildProfile profile = Assert.Single(
            LegacyCombinerPostbuildCatalog.GetProfiles("NT51928"));
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            profile,
            new IcNumberSelection(IcNumberInputMode.SingleSelector, [IcNumberSelectionTokens.SingleChip]));

        Assert.False(CtrlRamV2RouteRegistry.TryResolve(plan, out CtrlRamV2Route? route));
        Assert.Null(route);
    }
}
