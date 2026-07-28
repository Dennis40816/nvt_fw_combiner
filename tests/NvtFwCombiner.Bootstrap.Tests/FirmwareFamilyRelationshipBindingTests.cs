using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// Issue #177 deployed-bundle evidence for explicit family relationships and
/// one canonical DPCMI definition identity.
/// </summary>
public sealed class FirmwareFamilyRelationshipBindingTests
{
    /// <summary>NT51919/29/32 resolve one perfect-like family map and one DPCMI definition.</summary>
    [Fact]
    public void Nt51919Nt51929Nt51932UseOnePerfectLikeFamily()
    {
        MetadataPlanDefinition nt51919 = CreateDpReplacePlan("NT51919");
        MetadataPlanDefinition nt51929 = CreateDpReplacePlan("NT51929");
        MetadataPlanDefinition nt51932 = CreateDpReplacePlan("NT51932");
        FirmwareFamilyResolutionDefinition family =
            Assert.Single(nt51929.Entries).FamilyDefinition;
        PerfectFamilyRelationship relationship =
            Assert.IsType<PerfectFamilyRelationship>(
                Assert.Single(family.FamilyRelationships));

        Assert.Same(family, Assert.Single(nt51919.Entries).FamilyDefinition);
        Assert.Same(family, Assert.Single(nt51932.Entries).FamilyDefinition);
        Assert.Equal(
            ["NT51919", "NT51929", "NT51932"],
            relationship.MemberIds);
        FirmwareImageMap map = Assert.Single(family.ImageMaps);
        Assert.Equal(
            "nt51919-nt51929-nt51932-perfect-map-256k",
            map.MapId);
        Assert.Equal(
            relationship.MemberIds,
            map.Applicability.MemberIds);
        Assert.Same(
            Assert.Single(nt51929.Entries).StructureDefinition.Definition,
            Assert.Single(nt51919.Entries).StructureDefinition.Definition);
        Assert.Same(
            Assert.Single(nt51929.Entries).StructureDefinition.Definition,
            Assert.Single(nt51932.Entries).StructureDefinition.Definition);
    }

    /// <summary>
    /// NT51927/28 share only Initial Code and TP; NT51928 LDC remains outside
    /// both partial relationships.
    /// </summary>
    [Theory]
    [InlineData("NT51927")]
    [InlineData("NT51928")]
    public void Nt51927Nt51928RelationshipsRemainFactScoped(string icId)
    {
        MetadataPlanDefinition plan = CreateStandardMergePlan(icId);
        FirmwareFamilyResolutionDefinition family =
            plan.Entries[0].FamilyDefinition;
        SharedFactRelationship initialCode = Assert.Single(
            family.FamilyRelationships.OfType<SharedFactRelationship>(),
            static relationship =>
                relationship.Role == FirmwareSharedFactRole.InitialCodeShared);
        SharedFactRelationship tp = Assert.Single(
            family.FamilyRelationships.OfType<SharedFactRelationship>(),
            static relationship =>
                relationship.Role == FirmwareSharedFactRole.TpShared);

        Assert.Equal(["NT51927", "NT51928"], initialCode.MemberIds);
        Assert.Equal(
            ["nt51927-standard-merge-256k", "nt51928-standard-merge-512k"],
            initialCode.ApplicableMaps.Select(static map => map.MapId));
        Assert.Equal(
            [
                (FirmwareSharedFactKind.Region, "dp-code"),
                (FirmwareSharedFactKind.MetadataDefinition, DpcmiMetadataContract.StructureId),
            ],
            initialCode.SharedFactReferences.Select(static reference =>
                (reference.Kind, reference.FactId)));
        AssertSharedRegionIdentity(initialCode, "dp-code");
        Assert.Equal(["NT51927", "NT51928"], tp.MemberIds);
        Assert.Equal(
            [
                (FirmwareSharedFactKind.Region, "tp-code"),
                (FirmwareSharedFactKind.MetadataDefinition, "firmware-config-general-parameters"),
            ],
            tp.SharedFactReferences.Select(static reference =>
                (reference.Kind, reference.FactId)));
        AssertSharedRegionIdentity(tp, "tp-code");
        Assert.DoesNotContain(
            family.FamilyRelationships
                .OfType<SharedFactRelationship>()
                .SelectMany(static relationship => relationship.SharedFactReferences),
            static reference =>
                reference.Kind == FirmwareSharedFactKind.Region &&
                StringComparer.Ordinal.Equals(reference.FactId, "ldc-code"));

        FirmwareMetadataStructure dpcmi =
            AssertStructure(family, plan.Entries[0].ResolvedMap.ImageMap.MapId, "dpcmi");
        Assert.Same(
            Assert.Single(
                initialCode.SharedFactReferences,
                static reference =>
                    reference.Kind == FirmwareSharedFactKind.MetadataDefinition)
                .MetadataDefinition,
            dpcmi.Definition);
        Assert.Same(
            Assert.Single(
                tp.SharedFactReferences,
                static reference =>
                    reference.Kind == FirmwareSharedFactKind.MetadataDefinition)
                .MetadataDefinition,
            plan.Entries.Single(entry =>
                StringComparer.Ordinal.Equals(
                    entry.StructureDefinition.Definition.DefinitionId,
                    "firmware-config-general-parameters"))
                .StructureDefinition.Definition);
    }

    /// <summary>NT51950/51 declare TP sharing without importing DP/topology/AB facts.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void Nt51950Nt51951RelationshipIsTpOnly(string icId)
    {
        MetadataPlanDefinition plan = CreateStandardMergePlan(icId);
        SharedFactRelationship relationship =
            Assert.IsType<SharedFactRelationship>(
                Assert.Single(
                    plan.Entries[0].FamilyDefinition.FamilyRelationships));

        Assert.Equal(FirmwareSharedFactRole.TpShared, relationship.Role);
        Assert.Equal(["NT51950", "NT51951"], relationship.MemberIds);
        Assert.Equal(
            [
                "nt51950-standard-merge-1024k",
                "nt51950-standard-merge-256k",
                "nt51950-standard-merge-512k",
                "nt51951-standard-merge-1024k",
                "nt51951-standard-merge-256k",
                "nt51951-standard-merge-512k",
            ],
            relationship.ApplicableMaps.Select(static map => map.MapId));
        Assert.Equal(
            [
                (FirmwareSharedFactKind.Region, "tp-overlay"),
                (FirmwareSharedFactKind.MetadataDefinition, "firmware-config-general-parameters"),
            ],
            relationship.SharedFactReferences.Select(static reference =>
                (reference.Kind, reference.FactId)));
        AssertSharedRegionIdentity(relationship, "tp-overlay");
        Assert.DoesNotContain(
            relationship.SharedFactReferences,
            static reference =>
                reference.Kind == FirmwareSharedFactKind.MetadataDefinition &&
                StringComparer.Ordinal.Equals(
                    reference.FactId,
                    DpcmiMetadataContract.StructureId));
    }

    /// <summary>Every evidenced DP route reuses the canonical DPCMI definition at its owner-backed locator.</summary>
    [Theory]
    [InlineData("NT51917", 0x3C01C)]
    [InlineData("NT51919", 0x401A)]
    [InlineData("NT51923", 0x3E014)]
    [InlineData("NT51926", 0x3E014)]
    [InlineData("NT51927", 0x3C01C)]
    [InlineData("NT51928", 0x3C01C)]
    [InlineData("NT51929", 0x401A)]
    [InlineData("NT51932", 0x401A)]
    public void EvidencedDpRoutesReuseOneDpcmiDefinition(
        string icId,
        long expectedOffset)
    {
        MetadataPlanEntry provider =
            Assert.Single(CreateDpReplacePlan("NT51929").Entries);
        MetadataPlanEntry candidate =
            Assert.Single(CreateDpReplacePlan(icId).Entries);
        FirmwareRegionRelativeLocator locator =
            Assert.IsType<FirmwareRegionRelativeLocator>(
                candidate.StructureDefinition.Locator);
        FirmwareRegion baseRegion =
            candidate.ResolvedMap.ImageMap.Regions.Single(region =>
                StringComparer.Ordinal.Equals(
                    region.RegionId,
                    locator.RegionId));

        Assert.Same(
            provider.StructureDefinition.Definition,
            candidate.StructureDefinition.Definition);
        Assert.Equal(
            expectedOffset,
            baseRegion.Range.Start + locator.Offset);
    }

    private static FirmwareMetadataStructure AssertStructure(
        FirmwareFamilyResolutionDefinition family,
        string mapId,
        string structureId)
    {
        Assert.True(
            family.TryResolveStructure(
                mapId,
                structureId,
                out FirmwareMetadataStructure? structure));
        return Assert.IsType<FirmwareMetadataStructure>(structure);
    }

    private static void AssertSharedRegionIdentity(
        SharedFactRelationship relationship,
        string regionId)
    {
        FirmwareRegion sharedRegion = Assert.IsType<FirmwareRegion>(
            Assert.Single(
                relationship.SharedFactReferences,
                reference =>
                    reference.Kind == FirmwareSharedFactKind.Region &&
                    StringComparer.Ordinal.Equals(reference.FactId, regionId))
                .Region);
        Assert.All(
            relationship.ApplicableMaps,
            map => Assert.Same(
                sharedRegion,
                map.Regions.Single(region =>
                    StringComparer.Ordinal.Equals(region.RegionId, regionId))));
    }

    private static MetadataPlanDefinition CreateStandardMergePlan(string icId)
    {
        return CreatePlan(
            BuiltInV2RegistrationRegistry.StandardMergeByIc[icId]);
    }

    private static MetadataPlanDefinition CreateDpReplacePlan(string icId)
    {
        return CreatePlan(
            BuiltInV2RegistrationRegistry.DpReplaceByIc.Value[icId]);
    }

    private static MetadataPlanDefinition CreatePlan(
        BuiltInV2Registration registration)
    {
        IReadOnlyList<long> capacities =
            registration.GetMapCapacities(
                out IReadOnlyList<CompositionIssue> capacityIssues);
        Assert.True(
            capacityIssues.Count == 0,
            string.Join(
                Environment.NewLine,
                capacityIssues.Select(static issue =>
                    $"{issue.Code}: {issue.Message}")));
        Assert.NotEmpty(capacities);
        long capacity = capacities[0];
        registration.TryCompile(
            capacity,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);
        Assert.True(
            issues.Count == 0,
            string.Join(
                Environment.NewLine,
                issues.Select(static issue =>
                    $"{issue.Code}: {issue.Message}")));
        CompiledComposition compiled =
            Assert.IsType<CompiledComposition>(composition);
        return registration.CreateMetadataPlan(compiled);
    }
}
