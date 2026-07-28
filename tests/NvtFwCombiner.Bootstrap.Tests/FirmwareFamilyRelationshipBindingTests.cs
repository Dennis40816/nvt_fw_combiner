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
        FirmwareFamilyRelationship relationship =
            Assert.Single(family.FamilyRelationships);

        Assert.Same(family, Assert.Single(nt51919.Entries).FamilyDefinition);
        Assert.Same(family, Assert.Single(nt51932.Entries).FamilyDefinition);
        Assert.Equal(
            FirmwareFamilyRelationshipKind.PerfectLikeFamily,
            relationship.Kind);
        Assert.Equal(
            ["NT51919", "NT51929", "NT51932"],
            relationship.MemberIds);
        Assert.Empty(relationship.SharedRegionIds);
        Assert.Empty(relationship.MetadataDefinitions);
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
        FirmwareFamilyRelationship initialCode =
            Assert.Single(family.FamilyRelationships, relationship =>
                relationship.Kind ==
                FirmwareFamilyRelationshipKind.InitialCodeSharedFamily);
        FirmwareFamilyRelationship tp =
            Assert.Single(family.FamilyRelationships, relationship =>
                relationship.Kind ==
                FirmwareFamilyRelationshipKind.TpSharedFamily);

        Assert.Equal(["NT51927", "NT51928"], initialCode.MemberIds);
        Assert.Equal(["dp-code"], initialCode.SharedRegionIds);
        Assert.Equal(
            [DpcmiMetadataContract.StructureId],
            initialCode.MetadataDefinitions.Select(
                static definition => definition.DefinitionId));
        Assert.Equal(["NT51927", "NT51928"], tp.MemberIds);
        Assert.Equal(["tp-code"], tp.SharedRegionIds);
        Assert.Equal(
            ["firmware-config-general-parameters"],
            tp.MetadataDefinitions.Select(
                static definition => definition.DefinitionId));
        Assert.DoesNotContain(
            family.FamilyRelationships.SelectMany(
                static relationship => relationship.SharedRegionIds),
            regionId => StringComparer.Ordinal.Equals(regionId, "ldc-code"));

        FirmwareMetadataStructure dpcmi =
            AssertStructure(family, plan.Entries[0].ResolvedMap.ImageMap.MapId, "dpcmi");
        Assert.Same(
            Assert.Single(initialCode.MetadataDefinitions),
            dpcmi.Definition);
        Assert.Same(
            Assert.Single(tp.MetadataDefinitions),
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
        FirmwareFamilyRelationship relationship = Assert.Single(
            plan.Entries[0].FamilyDefinition.FamilyRelationships);

        Assert.Equal(
            FirmwareFamilyRelationshipKind.TpSharedFamily,
            relationship.Kind);
        Assert.Equal(["NT51950", "NT51951"], relationship.MemberIds);
        Assert.Equal(["tp-overlay"], relationship.SharedRegionIds);
        Assert.Equal(
            ["firmware-config-general-parameters"],
            relationship.MetadataDefinitions.Select(
                static definition => definition.DefinitionId));
        Assert.DoesNotContain(
            relationship.MetadataDefinitions,
            static definition =>
                StringComparer.Ordinal.Equals(
                    definition.DefinitionId,
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
