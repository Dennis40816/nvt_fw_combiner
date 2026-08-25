using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks CtrlRAM V2 routing to profile and typed-plan authority.</summary>
public sealed class CtrlRamV2RouteRegistryTests
{
    /// <summary>Every declared CtrlRAM route must normalize through its trusted bundle.</summary>
    [Fact]
    public void EveryDeclaredRouteNormalizesThroughItsTrustedBundle()
    {
        foreach (CtrlRamV2Route route in CtrlRamV2RouteRegistry.All)
        {
            IReadOnlyList<FirmwareImageMap> maps = BuiltInV2BundleRegistry.All[route.BundleId]
                .GetMapVariants(
                    route.ProfileId,
                    route.ProfileVersion,
                    route.Key.IcId,
                    ExperienceIds.CtrlRamReplace,
                    out IReadOnlyList<CompositionIssue> issues);

            Assert.True(
                issues.Count == 0,
                $"Route '{route.Key}' did not normalize: {string.Join(" | ", issues.Select(static issue => $"{issue.Code}: {issue.Message}"))}");
            Assert.NotEmpty(maps);
        }
    }

    /// <summary>The eleven owner-approved TP/full route cells expose exactly one TP-core map and one full-container map.</summary>
    [Theory]
    [InlineData("NT51917", "nt51917-ctrlram-replace-fw141-single", "0.3.0", "nt51927-ctrlram-fw141-single-tp-work-212k", "nt51927-ctrlram-fw141-single-full-flash")]
    [InlineData("NT51917", "nt51917-ctrlram-replace-fw132-twochip", "0.3.0", "nt51927-ctrlram-fw132-twochip-tp-work-212k", "nt51927-ctrlram-fw132-twochip-full-flash")]
    [InlineData("NT51917", "nt51917-ctrlram-replace-fw140-threechip", "0.3.0", "nt51927-ctrlram-fw140-threechip-tp-work-212k", "nt51927-ctrlram-fw140-threechip-full-flash")]
    [InlineData("NT51923", "nt51923-ctrlram-replace-fw141-single", "0.4.0", "nt51923-ctrlram-fw141-single-tp-work-240k", "nt51923-ctrlram-fw141-single-full-flash")]
    [InlineData("NT51923", "nt51923-ctrlram-replace-fw141-cascade3", "0.4.0", "nt51923-ctrlram-fw141-cascade3-tp-work-240k", "nt51923-ctrlram-fw141-cascade3-full-flash")]
    [InlineData("NT51927", "nt51927-ctrlram-replace-fw141-single", "0.3.0", "nt51927-ctrlram-fw141-single-tp-work-212k", "nt51927-ctrlram-fw141-single-full-flash")]
    [InlineData("NT51927", "nt51927-ctrlram-replace-fw132-twochip", "0.3.0", "nt51927-ctrlram-fw132-twochip-tp-work-212k", "nt51927-ctrlram-fw132-twochip-full-flash")]
    [InlineData("NT51927", "nt51927-ctrlram-replace-fw140-threechip", "0.3.0", "nt51927-ctrlram-fw140-threechip-tp-work-212k", "nt51927-ctrlram-fw140-threechip-full-flash")]
    [InlineData("NT51928", "nt51928-ctrlram-replace-fw141-single", "0.4.0", "nt51928-ctrlram-fw141-single-tp-work-212k", "nt51928-ctrlram-fw141-single-full-flash")]
    [InlineData("NT51928", "nt51928-ctrlram-replace-fw132-twochip", "0.3.0", "nt51928-ctrlram-fw132-twochip-tp-work-212k", "nt51928-ctrlram-fw132-twochip-full-flash")]
    [InlineData("NT51928", "nt51928-ctrlram-replace-fw140-threechip", "0.4.0", "nt51928-ctrlram-fw140-threechip-tp-work-212k", "nt51928-ctrlram-fw140-threechip-full-flash")]
    public void TpAndFullRouteCellBindsExactlyTwoCapacityMaps(
        string icId,
        string profileId,
        string profileVersion,
        string expectedTpMapId,
        string expectedFullMapId)
    {
        CtrlRamV2Route route = Assert.Single(
            CtrlRamV2RouteRegistry.All,
            candidate => StringComparer.Ordinal.Equals(candidate.Key.IcId, icId) &&
                StringComparer.Ordinal.Equals(candidate.ProfileId, profileId));
        IReadOnlyList<FirmwareImageMap> maps = BuiltInV2BundleRegistry.All[route.BundleId]
            .GetMapVariants(
                route.ProfileId,
                route.ProfileVersion,
                route.Key.IcId,
                ExperienceIds.CtrlRamReplace,
                out IReadOnlyList<CompositionIssue> issues);

        Assert.Empty(issues);
        Assert.Equal(profileVersion, route.ProfileVersion);
        Assert.Equal([expectedFullMapId, expectedTpMapId], maps.Select(static map => map.MapId));
    }

    /// <summary>Every declared route has one unique IC/effective-profile/plan authority key.</summary>
    [Fact]
    public void ProductionRouteKeysAreUnique()
    {
        Assert.Equal(25, CtrlRamV2RouteRegistry.All.Count);
        Assert.Equal(
            CtrlRamV2RouteRegistry.All.Count,
            CtrlRamV2RouteRegistry.All.Select(static route => route.Key).Distinct().Count());
        Assert.Equal(
            19,
            CtrlRamV2RouteRegistry.All.Count(static route =>
                route.ReportMetadataMapId is not null));
        Assert.Equal(
            6,
            CtrlRamV2RouteRegistry.All.Count(static route =>
                route.ReportMetadataMapId is null));
    }

    /// <summary>Retired ICs have no production CtrlRAM route, bundle, profile, or processor owner.</summary>
    [Theory]
    [InlineData("NT51920")]
    [InlineData("NT51925")]
    [InlineData("NT51930")]
    [InlineData("NT51931")]
    public void RetiredIcIdsHaveNoCtrlRamV2Route(string icId)
    {
        Assert.DoesNotContain(
            CtrlRamV2RouteRegistry.All,
            route => StringComparer.Ordinal.Equals(route.Key.IcId, icId));
    }

    /// <summary>Both owner-modeled NT51926 Common FW 1.x plans have explicit V2 routes.</summary>
    [Theory]
    [InlineData(IcNumberInputMode.SingleSelector, IcNumberSelectionTokens.SingleChip, LegacyCombinerPostbuildBranch.SingleChip, "nt51926-ctrlram-replace-fw141-runtime-single")]
    [InlineData(IcNumberInputMode.CascadeSelector, IcNumberSelectionTokens.Cascade, LegacyCombinerPostbuildBranch.Cascade, "nt51926-ctrlram-replace-fw141-runtime-cascade")]
    public void Nt51926CommonFw1xPlanResolvesItsTypedV2Route(
        IcNumberInputMode mode,
        string token,
        LegacyCombinerPostbuildBranch expectedBranch,
        string expectedProfileId)
    {
        LegacyCombinerPostbuildProfile profile = LegacyCombinerPostbuildCatalog
            .GetProfiles("NT51926")
            .Single(static candidate => candidate.EffectiveCommonFwVersion == new LegacyCombinerCommonFwVersion(1, 0, 0));
        LegacyCombinerPostbuildCommandPlan plan = profile.ResolvePlan(new IcNumberSelection(mode, [token]));

        Assert.Equal(expectedBranch, plan.Branch);
        Assert.True(CtrlRamV2RouteRegistry.TryResolve(plan, out CtrlRamV2Route? route));
        Assert.NotNull(route);
        Assert.Equal(expectedProfileId, route.ProfileId);
    }

    /// <summary>Every runtime catalog plan has one production V2 route.</summary>
    [Fact]
    public void EveryRuntimeCatalogPlanIsRouted()
    {
        foreach (LegacyCombinerPostbuildProfile profile in LegacyCombinerPostbuildCatalog.All)
        {
            foreach (LegacyCombinerPostbuildPlanSelector selector in profile.PlanSelectors)
            {
                Assert.True(
                    CtrlRamV2RouteRegistry.TryResolve(profile, selector.Branch, out _),
                    $"Runtime plan '{Key(profile.IcId, profile.ProcessorId, selector.Branch)}' has no V2 route.");
            }
        }
    }

    /// <summary>NT51928 uses each matching NT51927 TP branch while retaining its separate DP/LDC tail.</summary>
    [Theory]
    [InlineData(IcNumberInputMode.SingleSelector, IcNumberSelectionTokens.SingleChip, "nt51928-ctrlram-replace-fw141-single", "0.4.0")]
    [InlineData(IcNumberInputMode.NumericSelector, "2", "nt51928-ctrlram-replace-fw132-twochip", "0.3.0")]
    [InlineData(IcNumberInputMode.NumericSelector, "3", "nt51928-ctrlram-replace-fw140-threechip", "0.4.0")]
    public void Nt51928NonNbPlanResolvesMatchingTpRoute(
        IcNumberInputMode mode,
        string token,
        string expectedProfileId,
        string expectedProfileVersion)
    {
        LegacyCombinerPostbuildProfile profile = Assert.Single(
            LegacyCombinerPostbuildCatalog.GetProfiles("NT51928"));
        LegacyCombinerPostbuildCommandPlan plan = profile.ResolvePlan(new IcNumberSelection(mode, [token]));

        Assert.True(CtrlRamV2RouteRegistry.TryResolve(plan, out CtrlRamV2Route? route));
        Assert.NotNull(route);
        Assert.Equal(expectedProfileId, route.ProfileId);
        Assert.Equal(expectedProfileVersion, route.ProfileVersion);
    }

    /// <summary>NT51950/NT51951 cascade resolve their new versioned profiles, not the single profile.</summary>
    [Theory]
    [InlineData("NT51950", "nfc.nt51950.ctrlram-postbuild-v1", "nt51950-ctrlram-replace-fw1x-cascade")]
    [InlineData("NT51951", "nfc.nt51951.ctrlram-postbuild-v1", "nt51951-ctrlram-replace-fw1x-cascade")]
    public void Nt51950FamilyCascadeResolvesVersionedProfile(
        string icId,
        string processorId,
        string expectedProfileId)
    {
        LegacyCombinerPostbuildProfile profile = Assert.Single(
            LegacyCombinerPostbuildCatalog.GetProfiles(icId));
        Assert.Equal(processorId, profile.ProcessorId);
        Assert.True(CtrlRamV2RouteRegistry.TryResolve(
            profile,
            LegacyCombinerPostbuildBranch.Cascade,
            out CtrlRamV2Route? route));
        Assert.NotNull(route);
        Assert.Equal(expectedProfileId, route.ProfileId);
        Assert.Equal("0.7.0", route.ProfileVersion);
    }

    private static CtrlRamV2RouteKey Key(
        string icId,
        string processorId,
        LegacyCombinerPostbuildBranch branch)
    {
        return new CtrlRamV2RouteKey(icId, processorId, branch);
    }
}
