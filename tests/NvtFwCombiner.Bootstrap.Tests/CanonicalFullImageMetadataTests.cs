using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Infrastructure.Capabilities;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Read-only views retain independently frozen pre-migration metadata behavior without DP execution.</summary>
public sealed class CanonicalFullImageMetadataTests
{
    private static readonly string[] FullImageConfigFields = ["observed-ic-count", "tp-firmware-version", "tp-firmware-version-complement"];
    private static readonly string[] LegacyConfigFields = ["tp-firmware-version", "tp-firmware-version-complement"];
    // Expectations below were transcribed from source cae71c04, captured before data migration.
    // Five original family SHA-256 values, in table family order:
    // 78eae90596d03721f887492dfd85c70481f68646a87fe7c08165ce9d281a1072 (23/26)
    // f6b87ff1b5c4ecbe4df9299ea2d10fc6d568d6d02d528a1119b5369465db2134 (17/27)
    // 6cd257c38e4c9ecb4e44c14d12027e44a6d484b8176112dceccb7328d153b617 (19/29/32)
    // 538392be2e910627afe0283f947cb63f5e285e97a1d44dd50ea1f6985c177b20 (28)
    // 02597d709affd69adfbd92fac4a9a75f245385fb7c0954a5de1c86035e7babf6 (50/51)
    // Profile purposes are intentionally reduced to Inspection; source identity is now family/view,
    // with both bundle/family hashes. No expected locator/target is derived from the new views.

    /// <summary>All fifteen old member/capacity cases retain selected targets, physical ranges and terminal display facts.</summary>
    [Theory]
    [InlineData("NT51923", 0x40000, 0x3E014, "firmware-config-general-parameters", 0, 0x3C000)]
    [InlineData("NT51926", 0x40000, 0x3E014, "firmware-config-general-parameters", 0, 0x3C000)]
    [InlineData("NT51917", 0x40000, 0x3C01C, "firmware-config-general-parameters", 0, 0x35000)]
    [InlineData("NT51927", 0x40000, 0x3C01C, "firmware-config-general-parameters", 0, 0x35000)]
    [InlineData("NT51919", 0x40000, 0x0401A, null, 0, 0)]
    [InlineData("NT51929", 0x40000, 0x0401A, null, 0, 0)]
    [InlineData("NT51932", 0x40000, 0x0401A, null, 0, 0)]
    [InlineData("NT51928", 0x40000, -1, null, 0, 0)]
    [InlineData("NT51928", 0x80000, -1, null, 0, 0)]
    [InlineData("NT51950", 0x40000, 0x3B016, "firmware-config-dp-replace", 0xA000, 0x2D000)]
    [InlineData("NT51950", 0x80000, 0x3B016, "firmware-config-dp-replace", 0xA000, 0x2D000)]
    [InlineData("NT51950", 0x100000, 0x3B016, "firmware-config-dp-replace", 0xA000, 0x2D000)]
    [InlineData("NT51951", 0x40000, 0x05016, "firmware-config-dp-replace", 0xA000, 0x2D000)]
    [InlineData("NT51951", 0x80000, 0x05016, "firmware-config-dp-replace", 0xA000, 0x2D000)]
    [InlineData("NT51951", 0x100000, 0x05016, "firmware-config-dp-replace", 0xA000, 0x2D000)]
    public void FullImageViewsMatchFrozenPreMigrationCases(string ic, int capacity, int dpcmiStart,
        string? configId, int searchStart, int searchLength)
    {
        var catalog = new CanonicalCapabilityCatalog(CreateSource(CanonicalFullImageMetadataInventory.Create));
        MetadataPlanResolutionResult result = catalog.ResolveFullImageMetadataPlan(ic, capacity);
        Assert.True(result.Succeeded);
        ResolvedMetadataPlan plan = result.MetadataPlan!;
        CanonicalFullImageMetadataContext context = plan.Definition.FullImageContext!;
        Assert.Equal(ic, context.MemberId);
        Assert.Equal(capacity, context.View.ImageMap.CapacityBytes);
        Assert.Same(context.View, context.Family.FullImageMetadataViews!.Single(view => view.ViewId == context.View.ViewId));
        Assert.Same(context.SourceIdentity, plan.Definition.SourceIdentity);
        Assert.Null(context.SourceIdentity.ProfileId);
        Assert.Equal(context.Family.FamilyContentHash, context.SourceIdentity.FamilyContentHash);
        Assert.Empty(plan.Definition.ReportProjections);
        Assert.DoesNotContain(catalog.GetCurrentSnapshot().Capabilities,
            static capability => capability.Identity.WorkflowId == ExperienceIds.DpReplace);
        Assert.Empty(catalog.GetCurrentSnapshot().DynamicRoutes);
        byte[] image = new byte[capacity];
        if (dpcmiStart < 0)
        {
            Assert.Empty(plan.Entries);
            Assert.Empty(FirmwareMetadataInspector.InspectFullImage(plan, new("reference", image)).Results);
            return;
        }

        Assert.Equal(configId is null ? 1 : 2, plan.Entries.Count);
        MetadataPlanEntry dpcmi = Assert.Single(plan.Entries, entry =>
            entry.Definition.StructureDefinition.Definition.DefinitionId == DpcmiMetadataContract.StructureId).Definition;
        Assert.Equal("dp-replacement", dpcmi.SpaceId);
        Assert.Equal(["dp-major", "dp-minor", "jira-high", "jira-low"], dpcmi.FieldIds);
        Assert.All(plan.Entries, entry =>
        {
            Assert.Same(context, entry.Definition.FullImageContext);
            Assert.Same(entry.Definition.FullImageBinding!.Structure, entry.Definition.StructureDefinition);
            Assert.Equal(entry.Definition.FullImageBinding.TargetReferences, entry.Definition.TargetReferences);
            Assert.Equal([MetadataReferencePurpose.Inspection], entry.Definition.Purposes);
        });
        if (configId is not null)
        {
            MetadataPlanEntry config = Assert.Single(plan.Entries, entry =>
                entry.Definition.StructureDefinition.StructureId == configId).Definition;
            Assert.Equal("reference-base", config.SpaceId);
            Assert.Equal(configId == "firmware-config-dp-replace"
                ? FullImageConfigFields
                : LegacyConfigFields, config.FieldIds);
            FirmwareMarkerRelativeLocator locator = Assert.IsType<FirmwareMarkerRelativeLocator>(config.StructureDefinition.Locator);
            Assert.Equal(new ByteRange(searchStart, searchLength), locator.SearchRange.Range);
            Assert.Equal(-4092, locator.ResultOffset);
            WriteConfig(image, searchStart == 0 ? 0x1000 : 0x36000, 1);
        }
        image[dpcmiStart] = 0x2E;
        image[dpcmiStart + 1] = 0x03;
        image[dpcmiStart + 2] = 0xA4;
        MetadataInspectionSnapshot snapshot = FirmwareMetadataInspector.InspectFullImage(plan, new("reference", image));
        Assert.True(DpcmiMetadataProjector.TryProject(snapshot, out DpcmiMetadataFacts facts));
        Assert.Equal(new ByteRange(dpcmiStart, 3), facts.ResolvedRange);
        Assert.Equal("030A", facts.VersionToken);
        Assert.Equal((ushort)1070, facts.JiraNumber);
        Assert.All(snapshot.Results, entry => Assert.Equal(MetadataInspectionState.Value, entry.State));
    }

    /// <summary>Observed count selects the old DP-specific locator using FWConfig in the same full image.</summary>
    [Theory]
    [InlineData("NT51950", 1, 0x3B016)]
    [InlineData("NT51950", 2, 0x05016)]
    [InlineData("NT51951", 1, 0x05016)]
    [InlineData("NT51951", 2, 0x05016)]
    public void FullImagePrerequisitesUseSameCapture(string ic, byte count, int expectedStart)
    {
        ResolvedMetadataPlan plan = Resolve(ic);
        byte[] image = new byte[0x40000];
        WriteConfig(image, 0x36000, count);
        image[0x3B017] = 0x12;
        image[0x05017] = 0x56;
        var captured = new FirmwareArtifactPayload("reference", image);
        image[0x36017] = 0;
        MetadataInspectionSnapshot snapshot = FirmwareMetadataInspector.InspectFullImage(plan, captured);
        MetadataInspectionResult dpcmi = Dpcmi(snapshot);
        Assert.Equal(new ByteRange(expectedStart, 3), dpcmi.Resolution!.Resolved!.LocatorOutcome.ResolvedRange.Range);
        Assert.True(DpcmiMetadataProjector.TryProject(snapshot, out DpcmiMetadataFacts facts));
        Assert.Equal(expectedStart == 0x3B016 ? (byte)0x12 : (byte)0x56, facts.MajorVersion);
        _ = Assert.Throws<ArgumentException>(() => FirmwareMetadataInspector.Inspect(plan,
            [new("reference-base", image), captured]));
    }

    /// <summary>Missing, corrupt and unsupported FWConfig retain original prerequisite identities and never try another offset.</summary>
    [Theory]
    [InlineData("NT51950", "missing", FirmwareMetadataStructureResolutionFailure.PrerequisiteRejected)]
    [InlineData("NT51951", "missing", FirmwareMetadataStructureResolutionFailure.PrerequisiteRejected)]
    [InlineData("NT51950", "corrupt", FirmwareMetadataStructureResolutionFailure.PrerequisiteRejected)]
    [InlineData("NT51951", "corrupt", FirmwareMetadataStructureResolutionFailure.PrerequisiteRejected)]
    [InlineData("NT51950", "unsupported", FirmwareMetadataStructureResolutionFailure.PrerequisiteValueUnsupported)]
    [InlineData("NT51951", "unsupported", FirmwareMetadataStructureResolutionFailure.PrerequisiteValueUnsupported)]
    public void FullImagePrerequisiteFailuresAreTerminal(string ic, string mutation,
        FirmwareMetadataStructureResolutionFailure failure)
    {
        byte[] image = new byte[0x40000];
        if (mutation != "missing")
        {
            WriteConfig(image, 0x36000, mutation == "unsupported" ? (byte)0 : (byte)1);
        }
        if (mutation == "corrupt")
        {
            image[0x36FFD] = 0;
        }
        image[0x3B017] = 0x12;
        image[0x05017] = 0x56;
        MetadataInspectionSnapshot snapshot = FirmwareMetadataInspector.InspectFullImage(Resolve(ic), new("reference", image));
        MetadataInspectionResult dpcmi = Dpcmi(snapshot);
        Assert.Equal(failure, dpcmi.Resolution!.Failure);
        Assert.Equal(new FirmwareMetadataPrerequisite("reference-base", "firmware-config-dp-replace", "observed-ic-count"),
            dpcmi.Resolution.Prerequisite);
        Assert.Null(dpcmi.NextAction);
        Assert.False(DpcmiMetadataProjector.TryProject(snapshot, out _));
    }

    /// <summary>Provider mismatches fail the entire load while an exact provider without views grants nothing.</summary>
    [Theory]
    [InlineData("family")]
    [InlineData("version")]
    [InlineData("bundle")]
    [InlineData("hash")]
    public void InvalidFullImageProviderRetainsCompletePublication(string mutation)
    {
        ProfileBundlePackageTrustEntry provider = BuiltInV2BundleRegistry.TrustIndex.Bundles.Single(bundle =>
            bundle.BundleDirectory == "nt51919-nt51929-nt51932-shared-facts");
        ProfileBundleMetadataProviderFamily identity = Assert.Single(provider.MetadataProviderFamilies);
        bool reject = false;
        var catalog = new CanonicalCapabilityCatalog(CreateSource(() =>
        {
            if (!reject)
            {
                return CanonicalFullImageMetadataInventory.Create();
            }
            ProfileBundlePackageTrustEntry invalid = provider with
            {
                BundleDirectory = mutation == "bundle" ? "nt51923-standard-merge" : provider.BundleDirectory,
                ContentHash = mutation == "hash" ? new string('a', 64) : provider.ContentHash,
                MetadataProviderFamilies = [new(mutation == "family" ? "missing" : identity.FamilyId,
                    mutation == "version" ? "99.0.0" : identity.FamilyVersion)],
            };
            return CanonicalFullImageMetadataInventory.Create([invalid]);
        }));
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        CanonicalCapabilityCatalogSnapshot old = catalog.GetCurrentSnapshot();
        ResolvedMetadataPlan accepted = catalog.ResolveFullImageMetadataPlan("NT51929", 0x40000).MetadataPlan!;
        reject = true;
        CapabilityCatalogReloadResult failed = catalog.Reload(TestContext.Current.CancellationToken);
        Assert.False(failed.Succeeded);
        Assert.True(failed.RetainedLastKnownGood);
        Assert.Same(old, failed.Snapshot);
        Assert.Same(accepted, catalog.ResolveFullImageMetadataPlan("NT51929", 0x40000).MetadataPlan);
        var cold = new CanonicalCapabilityCatalog(CreateSource(() => throw new InvalidDataException("Bad provider.")));
        Assert.Equal(CapabilityCatalogIssueCodes.CatalogUnavailable, cold.ResolveFullImageMetadataPlan("NT51929", 0x40000).Issue!.Code);
        ProfileBundlePackageTrustEntry noViews = BuiltInV2BundleRegistry.TrustIndex.Bundles.First(bundle =>
            bundle.MetadataProviderFamilies.Count > 0 && CanonicalFullImageMetadataInventory.Create([bundle]).Count == 0);
        Assert.Empty(CanonicalFullImageMetadataInventory.Create([noViews]));
    }

    private static ResolvedMetadataPlan Resolve(string ic)
    {
        var catalog = new CanonicalCapabilityCatalog(CreateSource(CanonicalFullImageMetadataInventory.Create));
        return Assert.IsType<ResolvedMetadataPlan>(catalog.ResolveFullImageMetadataPlan(ic, 0x40000).MetadataPlan);
    }

    private static MetadataInspectionResult Dpcmi(MetadataInspectionSnapshot snapshot)
    {
        return Assert.Single(snapshot.Results, entry => entry.PlanEntry.Definition.StructureDefinition.Definition.DefinitionId == DpcmiMetadataContract.StructureId);
    }

    private static void WriteConfig(byte[] image, int start, byte count)
    {
        image[start] = 0x42;
        image[start + 1] = 0xBD;
        image[start + 23] = count;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(image, start + 4092);
    }

    private static CanonicalCapabilityCatalogSource CreateSource(Func<IReadOnlyList<MetadataPlanDefinition>> loadMetadata)
    {
        return new(() =>
            {
                CanonicalCapabilityPolicySnapshot policy = BuiltInCanonicalCapabilityPolicy.Load();
                return policy with
                {
                    Routes = [policy.Routes.First(route =>
                        route.Identity.WorkflowId != ExperienceIds.DpReplace &&
                        !CanonicalDynamicRouteInventory.IsDynamic(route.Identity))],
                };
            },
            static _ => false, CanonicalCompiledRouteInventory.Resolve,
            static () => static _ => throw new InvalidOperationException("No dynamic route is declared."),
            static (_, _) => CanonicalCapabilityDisclosure.Empty, loadMetadata);
    }
}
