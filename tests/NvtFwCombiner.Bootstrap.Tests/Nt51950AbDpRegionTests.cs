using System.Text.Json;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Capabilities;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Decision 192 preserves AB writes while declaring the complete DP complement.</summary>
public sealed class Nt51950AbDpRegionTests
{
    private const string BundleHash = "74c20ce3f1d53ca343995e1b757e0785cb842fe926e0799140d6d8251f137c49";

    /// <summary>Publication pins follow the existing dynamic compiler without changing route decisions.</summary>
    [Fact]
    public void PublishedRoutesPinTheCurrentAbDeclarations()
    {
        CanonicalCapabilityPolicyRoute[] routes = [.. BuiltInCanonicalCapabilityPolicy.Load().Routes.Where(route =>
            route.Identity.IcId is "NT51950" or "NT51951" &&
            (route.Identity.WorkflowId == ExperienceIds.AbMerge ||
             (route.Identity.WorkflowId == ExperienceIds.CtrlRamReplace && route.Identity.MapVariant.Contains("-ab-", StringComparison.Ordinal))))];
        Assert.Equal(7, routes.Length);
        (CanonicalCapabilityPolicyRoute Policy, CanonicalDynamicRoute Current)[] actual =
            [.. routes.Select(route => (route, CanonicalDynamicRouteInventory.Resolve(route.Identity)))];
        foreach ((CanonicalCapabilityPolicyRoute policy, CanonicalDynamicRoute current) in actual)
        {
            TestContext.Current.TestOutputHelper!.WriteLine(
                $"ROUTE-PIN {policy.Identity.RouteId} {policy.CapabilityFingerprint} {current.CapabilityFingerprint}");
        }
        Assert.All(actual, pair => Assert.Equal(pair.Current.CapabilityFingerprint, pair.Policy.CapabilityFingerprint));
    }

    /// <summary>Every map exposes its exact compiled writes for before/after auditing, including dummy DP.</summary>
    [Theory]
    [InlineData("NT51950", "-desay", "0.2.1", 1, 0x100000, "nt51950-ab-desay-single-1024k", 0x40000)]
    [InlineData("NT51950", "-desay", "0.2.1", 2, 0x100000, "nt51950-ab-desay-cascade-1024k", 0x40000)]
    [InlineData("NT51951", "-desay", "0.2.1", 0, 0x100000, "nt51951-ab-desay-1024k", 0x40000)]
    [InlineData("NT51950", "", "0.8.0", 1, 0x80000, "nt51950-ab-merge-512k", 0x40000)]
    [InlineData("NT51950", "-cascade", "0.4.0", 2, 0x100000, "nt51950-ab-merge-1024k", 0x80000)]
    [InlineData("NT51951", "", "0.7.0", 0, 0x100000, "nt51951-ab-merge-1024k", 0x80000)]
    public void CompiledWritesRemainTpOnlyAfterDpInitialization(
        string ic, string suffix, string version, int count, int capacity, string mapId, int bankOffset)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-dp-range-audit");
        TrustedProfileBundleCatalog catalog = AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(
            workspace, "nt51950-ab-merge", BundleHash);
        foreach (bool dummy in new[] { false, true })
        {
            CompiledComposition composition = Compile(catalog, ic, suffix, version, count, capacity, dummy);
            Assert.Equal(mapId, composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
            CompositionOperation[] overlays = [.. composition.Plan.OrderedOperations.Where(operation =>
                operation.OperationId is "overlay-tpa-into-output" or "overlay-tpb-into-output")];
            Assert.Equal([new ByteRange(0xA000, 0x2D000), new ByteRange(bankOffset + 0xA000, 0x2D000)],
                overlays.Select(operation => operation.TargetRange));
            foreach (CompositionOperation operation in composition.Plan.OrderedOperations.Where(operation =>
                operation.TargetSpaceId == "output-image" && operation.OperationId != "copy-dp-ab-image"))
            {
                Assert.All(operation.DeclaredWriteRanges, range =>
                    Assert.Contains(overlays, overlay => overlay.TargetRange.Contains(range)));
            }

            string audit = JsonSerializer.Serialize(new
            {
                mapId,
                dummy,
                initialization = composition.Plan.OutputInitialization,
                operations = composition.Plan.OrderedOperations.Select(operation => new
                {
                    operation.OperationId,
                    operation.Sequence,
                    operation.Kind,
                    operation.SourceSpaceId,
                    operation.SourceRange,
                    operation.TargetSpaceId,
                    operation.TargetRange,
                    operation.DeclaredWriteRanges,
                    operation.OverlapPolicy,
                    operation.FillByte,
                    operation.PatchBytes,
                    operation.ScalarTransform,
                    operation.ExternalProcessorInvocation,
                }),
            });
            TestContext.Current.TestOutputHelper!.WriteLine("WRITE-AUDIT " + audit);
        }
    }

    /// <summary>DP images cover every non-TP byte and own CMI without relaxing protected leaf ranges.</summary>
    [Theory]
    [InlineData("NT51950", "-desay", "0.2.1", 1, 0x100000)]
    [InlineData("NT51950", "-desay", "0.2.1", 2, 0x100000)]
    [InlineData("NT51951", "-desay", "0.2.1", 0, 0x100000)]
    [InlineData("NT51950", "", "0.8.0", 1, 0x80000)]
    [InlineData("NT51950", "-cascade", "0.4.0", 2, 0x100000)]
    [InlineData("NT51951", "", "0.7.0", 0, 0x100000)]
    public void DpSectionsPartitionTheTpComplementAndOwnCmi(
        string ic, string suffix, string version, int count, int capacity)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-dp-declarations");
        TrustedProfileBundleCatalog catalog = AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(
            workspace, "nt51950-ab-merge", BundleHash);
        FirmwareImageMap map = Compile(catalog, ic, suffix, version, count, capacity, false)
            .V2Details.Provenance.ResolvedMap.ImageMap;
        Assert.DoesNotContain(map.Regions, region => region.Owner == FirmwareRegionOwner.Unknown ||
            region.Kind == FirmwareRegionKind.Unmapped);
        FirmwareRegion[] sections = [.. map.Regions.Where(region =>
            region.Owner == FirmwareRegionOwner.Tp ||
            (region.Owner == FirmwareRegionOwner.Dp && region.Kind == FirmwareRegionKind.Image))
            .OrderBy(region => region.Range.Start)];
        long cursor = 0;
        foreach (FirmwareRegion section in sections)
        {
            Assert.Equal(cursor, section.Range.Start);
            cursor = section.Range.EndExclusive;
        }
        Assert.Equal(capacity, cursor);
        Assert.Contains("owner-decision-2026-09-29-nt51950-dp-ranges", map.EvidenceRefs);
        foreach (FirmwareRegion region in map.Regions.Where(region => region.Owner == FirmwareRegionOwner.Dp))
        {
            Assert.Equal(1, region.Alignment);
            FirmwareRegion[] children = [.. map.Regions.Where(child => child.ParentRegionId == region.RegionId)];
            bool isCmi = region.RegionId.EndsWith("-cmi-dp-version", StringComparison.Ordinal);
            Assert.Equal(isCmi || children.Length > 0 ? FirmwareWriteConstraint.ExplicitRange :
                FirmwareWriteConstraint.Forbidden, region.WriteConstraint);
            if (isCmi)
            {
                FirmwareRegion parent = Assert.Single(map.Regions, candidate => candidate.RegionId == region.ParentRegionId);
                Assert.Equal(FirmwareRegionOwner.Dp, parent.Owner);
                Assert.Equal(FirmwareRegionKind.Image, parent.Kind);
                Assert.True(parent.Range.Contains(region.Range));
                Assert.Equal(FirmwareRegionKind.Command, region.Kind);
            }
        }
    }

    private static CompiledComposition Compile(TrustedProfileBundleCatalog catalog,
        string ic, string suffix, string version, int count, int capacity, bool dummy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ic);
        V2CompositionPlanCompileResult result = catalog.Compile(
            $"{ic.ToLowerInvariant()}-ab-merge{suffix}", version, ic, ExperienceIds.AbMerge, capacity,
            count == 0 ? null : new TopologySelection(count, $"{count} IC", TopologySelectionSource.Requested, "test"),
            [], selectedInputSlotIds: dummy ? [] : ["dp-ab-input"]);
        Assert.True(result.IsCompiled, string.Join(" | ", result.Issues.Select(issue => issue.Message)));
        return Assert.IsType<CompiledComposition>(result.CompiledComposition);
    }
}
