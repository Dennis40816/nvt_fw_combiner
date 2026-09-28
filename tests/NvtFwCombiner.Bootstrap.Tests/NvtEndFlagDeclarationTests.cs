using System.Reflection;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// NVT-END-FLAG-1113-01 (owner decisions 30/35/37, ADR 0076): every NT51950/NT51951 layout that reads an NVT marker
/// declares the same end flag right after the FWConfig Backup region, TP section end minus 4; only the named migration
/// inventory may resolve without a declaration, and that inventory is exactly the built-in families still pending.
/// </summary>
public sealed class NvtEndFlagDeclarationTests
{
    private static readonly FirmwareAddressedRange Nt5195xEndFlag = new("flash", new ByteRange(0x36FFC, 4));
    private static readonly string[] MigratedIcIds = ["NT51950", "NT51951"];
    private static readonly string[] PendingIcIds =
        ["NT51917", "NT51919", "NT51923", "NT51926", "NT51927", "NT51928", "NT51929", "NT51932"];

    /// <summary>Standard/DP Replace/General Merge, CtrlRAM Replace and AB Merge all declare 0x36FFC.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void EveryNt5195xLayoutThatReadsTheMarkerDeclaresTheEndFlag(string icId)
    {
        (string Workflow, string FamilyId, string MapId, FirmwareNvtEndFlagResolution Resolution)[] declared =
            DeclaredResolutions(icId);

        Assert.Contains(declared, static entry => entry.Workflow == "standard-merge");
        Assert.Contains(declared, static entry => entry.Workflow == "general-merge");
        Assert.Contains(declared, static entry => entry.Workflow == "ctrlram-replace");
        Assert.Contains(declared, static entry => entry.Workflow == "ab-merge");
        Assert.All(declared, entry => Assert.Equal(Nt5195xEndFlag,
            Assert.IsType<FirmwareNvtEndFlag>(entry.Resolution.EndFlag).Position));
        Assert.Equal(Nt5195xEndFlag, Assert.IsType<FirmwareNvtEndFlag>(
            BuiltInFirmwareInspection.ResolveCtrlRamBaseNvtEndFlag(icId).EndFlag).Position);
    }

    /// <summary>
    /// F-3 executable inventory: the families that resolve without a declaration are exactly the named migration
    /// inventory, no NT51950/NT51951 family is in it, and no built-in layout fails to resolve.
    /// </summary>
    [Fact]
    public void LegacyInventoryIsExactlyTheBuiltInFamiliesStillPending()
    {
        var legacy = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string icId in RegisteredIcIds())
        {
            foreach ((string _, string familyId, string mapId, FirmwareNvtEndFlagResolution resolution) in
                     DeclaredResolutions(icId, includeMapsWithoutNvtLocator: true))
            {
                Assert.True(resolution.Succeeded, $"{familyId}/{mapId} does not resolve its NVT end flag.");
                if (resolution.UsesLegacyCompatibilityRead)
                {
                    Assert.DoesNotContain(icId, MigratedIcIds);
                    _ = legacy.Add(familyId);
                }
            }
        }

        Assert.Equal(FirmwareNvtEndFlagMigration.LegacyCompatibilityFamilyIds.Order(StringComparer.Ordinal), legacy);
        foreach (string icId in PendingIcIds)
        {
            Assert.True(BuiltInFirmwareInspection.ResolveCtrlRamBaseNvtEndFlag(icId).UsesLegacyCompatibilityRead);
        }
    }

    /// <summary>
    /// F-3 retirement gate: when the migration inventory is empty, the compatibility state and the no-declaration
    /// reader overloads are deleted in the same batch.
    /// </summary>
    [Fact]
    public void EmptyInventoryRequiresDeletingTheCompatibilityRead()
    {
        bool compatibilityStateExists = typeof(FirmwareNvtEndFlagResolution).GetProperty(
            nameof(FirmwareNvtEndFlagResolution.UsesLegacyCompatibilityRead)) is not null;
        bool undeclaredOverloadExists = typeof(FirmwareConfigMetadataReader)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Any(static method => method.Name == nameof(FirmwareConfigMetadataReader.TryReadBackup) &&
                method.GetParameters().All(static parameter =>
                    parameter.ParameterType != typeof(FirmwareNvtEndFlagResolution)));

        Assert.True(FirmwareNvtEndFlagMigration.LegacyCompatibilityFamilyIds.Count > 0 ||
            !(compatibilityStateExists || undeclaredOverloadExists));
    }

    /// <summary>All six AB maps declare the TP A and TP B end flag through the one new metadata set.</summary>
    [Fact]
    public void AllSixAbMapsDeclareTheTpInputEndFlag()
    {
        ProfileBundlePackageTrustEntry bundle = BuiltInV2BundleRegistry.TrustIndex.Bundles.Single(static entry =>
            entry.BundleDirectory == "nt51950-ab-merge");
        ProfileBundleRuntimeRegistration registration = bundle.RuntimeRegistrations[0];
        FirmwareFamilyResolutionDefinition family = BuiltInV2BundleRegistry.All[bundle.BundleDirectory]
            .GetFirmwareFamily(registration.ProfileId, registration.ProfileVersion);

        Assert.Equal(6, family.ImageMaps.Count);
        Assert.All(family.ImageMaps, map =>
        {
            FirmwareMetadataStructure[] nvt =
            [
                .. family.GetStructuresForMap(map.MapId).Where(static structure =>
                    structure.Locator is FirmwareMarkerRelativeLocator { MarkerBytes.Hex: "004e5654" }),
            ];
            Assert.Equal(["tp-a-fwconfig-backup", "tp-b-fwconfig-backup"],
                nvt.Select(static structure => structure.StructureId).Order(StringComparer.Ordinal));
            Assert.Equal([CompositionAddressSpaceIds.TpAInput, CompositionAddressSpaceIds.TpBInput],
                nvt.Select(static structure => structure.ArtifactBindingId).Order(StringComparer.Ordinal));
            Assert.Equal(Nt5195xEndFlag, Assert.IsType<FirmwareNvtEndFlag>(
                family.ResolveNvtEndFlag(map.MapId).EndFlag).Position);
        });
    }

    /// <summary>
    /// F-5 catalog regression: a fresh catalog load admits every NT51950/NT51951 AB route through the trusted
    /// profile-selective path with the declared end flag, the end-flag structures never become metadata-plan entries,
    /// and every NT51950/NT51951 full-image metadata plan is still created.
    /// </summary>
    [Fact]
    public void FreshCatalogAdmitsEveryNt5195xAbRouteAndMetadataPlan()
    {
        var host = new IsolatedBootstrapTestHost();
        ResolvedCapabilityRoute[] routes =
        [
            .. host.Catalog.GetCurrentSnapshot().DynamicRoutes.Where(static route =>
                route.Identity.WorkflowId == ExperienceIds.AbMerge && route.Identity.IcId is "NT51950" or "NT51951"),
        ];

        Assert.Equal(3, routes.Length);
        foreach (ResolvedCapabilityRoute route in routes)
        {
            Assert.True(host.Canonical.Compiler.TryCompilePublishedDynamicCapability(route.Identity, null, null,
                    out CompiledComposition? composition, out ResolvedCapability? capability,
                    out IReadOnlyList<CompositionIssue> issues, route.AbMergeTopologyChoice?.Selection),
                string.Join(',', issues.Select(static issue => issue.Code)));
            Assert.Equal(Nt5195xEndFlag, Assert.IsType<FirmwareNvtEndFlag>(
                composition!.V2Details.Provenance.ResolvedMap.NvtEndFlagResolution.EndFlag).Position);
            string[] planned =
            [
                .. capability!.MetadataPlan.Entries.Select(static entry =>
                    entry.Definition.StructureDefinition.StructureId),
            ];
            Assert.NotEmpty(planned);
            Assert.DoesNotContain("tp-a-fwconfig-backup", planned);
            Assert.DoesNotContain("tp-b-fwconfig-backup", planned);
        }

        foreach (string icId in MigratedIcIds)
        {
            foreach (long capacity in new long[] { 0x40000, 0x80000, 0x100000 })
            {
                MetadataPlanResolutionResult plan = host.Catalog.ResolveFullImageMetadataPlan(icId, capacity);
                Assert.True(plan.Succeeded, $"{icId} {capacity:X}");
                Assert.NotEmpty(plan.MetadataPlan!.Entries);
            }
        }
    }

    /// <summary>
    /// Auxiliary cross-check only (the Golden evidence is the execution of every applicable certified output case):
    /// every complete marker in the committed NT51950/NT51951 Golden BINs sits at a bank-local 0x36FFC.
    /// </summary>
    [Fact]
    public void Nt5195xGoldenMarkersSitAtTheDeclaredEndFlag()
    {
        int checkedMarkers = 0;
        foreach (string ic in MigratedIcIds)
        {
            foreach (string path in Directory.EnumerateFiles(Path.Combine(CanonicalGoldenTestData.Root, ic), "*.bin",
                         SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');
                bool abImage = normalized.Contains("/ab-merge/", StringComparison.Ordinal) &&
                    normalized.Contains("/expected/", StringComparison.Ordinal);
                foreach (int offset in MarkerOffsets(File.ReadAllBytes(path)))
                {
                    Assert.Equal(Nt5195xEndFlag.Range.Start, abImage ? offset % 0x40000 : offset);
                    checkedMarkers++;
                }
            }
        }

        Assert.True(checkedMarkers > 0);
    }

    private static IEnumerable<string> RegisteredIcIds()
    {
        return BuiltInV2BundleRegistry.TrustIndex.Bundles
            .SelectMany(static bundle => bundle.RuntimeRegistrations)
            .Select(static registration => registration.IcId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
    }

    private static (string Workflow, string FamilyId, string MapId, FirmwareNvtEndFlagResolution Resolution)[]
        DeclaredResolutions(string icId, bool includeMapsWithoutNvtLocator = false)
    {
        var declared = new List<(string, string, string, FirmwareNvtEndFlagResolution)>();
        foreach (ProfileBundlePackageTrustEntry bundle in BuiltInV2BundleRegistry.TrustIndex.Bundles)
        {
            foreach (ProfileBundleRuntimeRegistration registration in bundle.RuntimeRegistrations.Where(
                         registration => registration.IcId == icId))
            {
                FirmwareFamilyResolutionDefinition family = BuiltInV2BundleRegistry.All[bundle.BundleDirectory]
                    .GetFirmwareFamily(registration.ProfileId, registration.ProfileVersion);
                foreach (FirmwareImageMap map in family.ImageMaps.Where(map =>
                             map.Applicability.MemberIds.Contains(icId, StringComparer.Ordinal) &&
                             (includeMapsWithoutNvtLocator || family.GetStructuresForMap(map.MapId).Any(static structure =>
                                 structure.Locator is FirmwareMarkerRelativeLocator { MarkerBytes.Hex: "004e5654" }))))
                {
                    declared.Add((registration.WorkflowId, family.FamilyId, map.MapId, family.ResolveNvtEndFlag(map.MapId)));
                }
            }
        }

        Assert.NotEmpty(declared);
        return [.. declared];
    }

    private static List<int> MarkerOffsets(ReadOnlySpan<byte> bytes)
    {
        var offsets = new List<int>();
        int start = 0;
        int found;
        while ((found = bytes[start..].IndexOf(FirmwareNvtEndFlag.MarkerBytes)) >= 0)
        {
            offsets.Add(start + found);
            start += found + 1;
        }

        return offsets;
    }
}
