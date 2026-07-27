using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

public static partial class FirmwareFamilyResolutionNormalizer
{
    private static FirmwareFamilyRelationship[] NormalizeFamilyRelationships(
        FirmwareFamilyDocument document,
        IReadOnlyList<FirmwareImageMap> maps,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>> structuresByMap,
        IReadOnlyList<AliasDeclaration> aliases,
        IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> capabilities)
    {
        IReadOnlyList<FirmwareFamilyRelationshipDocument> relationshipDocuments =
            document.FamilyRelationships ?? [];
        if (relationshipDocuments.Count == 0)
        {
            return [];
        }

        Dictionary<string, FirmwareFamilyMemberDocument> membersById = IndexUnique(
            RequireList(document.Members, "members"),
            static member => member.MemberId,
            "members",
            "memberId");
        var definitionsById =
            structuresByMap.Values
                .SelectMany(static structures => structures.Values)
                .Select(static structure => structure.Definition)
                .GroupBy(static definition => definition.DefinitionId, StringComparer.Ordinal)
                .ToDictionary(
                    static group => group.Key,
                    static group => group.First(),
                    StringComparer.Ordinal);
        var relationshipIds = new HashSet<string>(StringComparer.Ordinal);
        var membersByKind = new HashSet<(FirmwareFamilyRelationshipKind Kind, string MemberId)>();
        var normalized = new FirmwareFamilyRelationship[relationshipDocuments.Count];

        for (int index = 0; index < relationshipDocuments.Count; index++)
        {
            FirmwareFamilyRelationshipDocument relationship =
                relationshipDocuments[index] ?? throw Error(
                    $"familyRelationships[{index}]",
                    "Family relationship cannot be null.");
            string path = $"familyRelationships[{index}]";
            if (!relationshipIds.Add(relationship.RelationshipId))
            {
                throw Error(
                    $"{path}.relationshipId",
                    $"Duplicate family relationship id '{relationship.RelationshipId}'.");
            }

            ValidateMemberReferences(relationship.MemberIds, membersById, $"{path}.memberIds");
            if (relationship.MemberIds.Count < 2 ||
                relationship.MemberIds.Distinct(StringComparer.Ordinal).Count() != relationship.MemberIds.Count)
            {
                throw Error(
                    $"{path}.memberIds",
                    "Family relationships require at least two ordinally unique members.");
            }

            FirmwareFamilyRelationshipKind kind = relationship switch
            {
                FirmwarePerfectLikeFamilyRelationshipDocument =>
                    FirmwareFamilyRelationshipKind.PerfectLikeFamily,
                FirmwareInitialCodeSharedFamilyRelationshipDocument =>
                    FirmwareFamilyRelationshipKind.InitialCodeSharedFamily,
                FirmwareTpSharedFamilyRelationshipDocument =>
                    FirmwareFamilyRelationshipKind.TpSharedFamily,
                _ => throw Error(path, "Unknown family relationship shape."),
            };
            foreach (string memberId in relationship.MemberIds)
            {
                if (!membersByKind.Add((kind, memberId)))
                {
                    throw Error(
                        $"{path}.memberIds",
                        $"Member '{memberId}' has more than one '{kind}' relationship.");
                }
            }

            normalized[index] = relationship switch
            {
                FirmwarePerfectLikeFamilyRelationshipDocument perfect =>
                    NormalizePerfectLikeRelationship(
                        perfect,
                        kind,
                        path,
                        maps,
                        aliases,
                        capabilities),
                FirmwareInitialCodeSharedFamilyRelationshipDocument initialCode =>
                    NormalizeSharedPartRelationship(
                        initialCode,
                        initialCode.SharedRegionIds,
                        initialCode.MetadataDefinitionIds,
                        kind,
                        path,
                        maps,
                        structuresByMap,
                        definitionsById),
                FirmwareTpSharedFamilyRelationshipDocument tp =>
                    NormalizeSharedPartRelationship(
                        tp,
                        tp.SharedRegionIds,
                        tp.MetadataDefinitionIds,
                        kind,
                        path,
                        maps,
                        structuresByMap,
                        definitionsById),
                _ => throw Error(path, "Unknown family relationship shape."),
            };
        }

        return normalized;
    }

    private static FirmwareFamilyRelationship NormalizePerfectLikeRelationship(
        FirmwarePerfectLikeFamilyRelationshipDocument document,
        FirmwareFamilyRelationshipKind kind,
        string path,
        IReadOnlyList<FirmwareImageMap> maps,
        IReadOnlyList<AliasDeclaration> aliases,
        IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> capabilities)
    {
        var memberIds = new HashSet<string>(document.MemberIds, StringComparer.Ordinal);
        FirmwareImageMap[] relatedMaps =
        [
            .. maps.Where(map => map.Applicability.MemberIds.Any(memberIds.Contains)),
        ];
        if (relatedMaps.Length == 0 ||
            document.MemberIds.Any(memberId =>
                relatedMaps.All(map => !map.Applicability.MemberIds.Contains(memberId, StringComparer.Ordinal))))
        {
            throw Error(path, "Every perfect-like member must be selected by a family-owned map.");
        }

        foreach (FirmwareImageMap map in relatedMaps)
        {
            if (!memberIds.SetEquals(map.Applicability.MemberIds))
            {
                throw Error(
                    path,
                    $"Perfect-like relationship '{document.RelationshipId}' contains member-specific map " +
                    $"'{map.MapId}'.");
            }
        }

        bool hasMemberAlias = aliases.Any(alias =>
            memberIds.Contains(alias.TargetKey.MemberId) ||
            memberIds.Contains(alias.SourceKey.MemberId));
        bool hasMemberCapability =
            capabilities.Any(binding => memberIds.Contains(binding.EffectiveKey.MemberId));
        return (hasMemberAlias, hasMemberCapability) switch
        {
            (true, _) => throw Error(
                path,
                "Perfect-like firmware semantics cannot be represented by member-specific fact aliases."),
            (_, true) => throw Error(
                path,
                "Perfect-like firmware semantics cannot contain member-specific capability facts."),
            _ => TranslateInvariant(path, () => new FirmwareFamilyRelationship(
                    document.RelationshipId,
                    kind,
                    document.MemberIds,
                    [],
                    [],
                    document.Reason,
                    document.EvidenceRefs)),
        };
    }

    private static FirmwareFamilyRelationship NormalizeSharedPartRelationship(
        FirmwareFamilyRelationshipDocument document,
        IReadOnlyList<string> sharedRegionIds,
        IReadOnlyList<string> metadataDefinitionIds,
        FirmwareFamilyRelationshipKind kind,
        string path,
        IReadOnlyList<FirmwareImageMap> maps,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>> structuresByMap,
        Dictionary<string, FirmwareMetadataStructureDefinition> definitionsById)
    {
        ValidateDistinctFactIds(sharedRegionIds, $"{path}.sharedRegionIds");
        ValidateDistinctFactIds(metadataDefinitionIds, $"{path}.metadataDefinitionIds");
        if (sharedRegionIds.Count == 0)
        {
            throw Error(
                $"{path}.sharedRegionIds",
                "Shared-part relationships require at least one shared region.");
        }

        var definitions = new FirmwareMetadataStructureDefinition[
            metadataDefinitionIds.Count];
        for (int index = 0; index < metadataDefinitionIds.Count; index++)
        {
            string definitionId = metadataDefinitionIds[index];
            if (!definitionsById.TryGetValue(definitionId, out FirmwareMetadataStructureDefinition? definition))
            {
                throw Error(
                    $"{path}.metadataDefinitionIds[{index}]",
                    $"Unknown metadata definition '{definitionId}'.");
            }

            definitions[index] = definition;
        }

        FirmwareImageMap? baselineMap = null;
        IReadOnlyDictionary<string, FirmwareMetadataStructure>? baselineStructures = null;
        foreach (string memberId in document.MemberIds)
        {
            FirmwareImageMap[] memberMaps =
            [
                .. maps.Where(map =>
                    map.Applicability.MemberIds.Contains(memberId, StringComparer.Ordinal) &&
                    sharedRegionIds.Any(regionId =>
                        map.Regions.Any(region =>
                            StringComparer.Ordinal.Equals(region.RegionId, regionId)))),
            ];
            if (memberMaps.Length == 0)
            {
                throw Error(
                    $"{path}.memberIds",
                    $"Member '{memberId}' has no map containing the declared shared part.");
            }

            foreach (FirmwareImageMap map in memberMaps)
            {
                IReadOnlyDictionary<string, FirmwareMetadataStructure> structures =
                    structuresByMap[map.MapId];
                if (sharedRegionIds.Any(regionId =>
                        map.Regions.All(region =>
                            !StringComparer.Ordinal.Equals(region.RegionId, regionId))))
                {
                    throw Error(
                        $"{path}.sharedRegionIds",
                        $"Map '{map.MapId}' contains only part of the declared shared geometry.");
                }

                if (metadataDefinitionIds.Any(definitionId =>
                        structures.Values.All(structure =>
                            !StringComparer.Ordinal.Equals(
                                structure.Definition.DefinitionId,
                                definitionId))))
                {
                    throw Error(
                        $"{path}.metadataDefinitionIds",
                        $"Map '{map.MapId}' does not select all metadata owned by the shared part.");
                }

                baselineMap ??= map;
                baselineStructures ??= structures;
                ValidateSharedPartMatchesBaseline(
                    path,
                    sharedRegionIds,
                    metadataDefinitionIds,
                    baselineMap,
                    baselineStructures,
                    map,
                    structures);
            }
        }

        return TranslateInvariant(path, () => new FirmwareFamilyRelationship(
                document.RelationshipId,
                kind,
                document.MemberIds,
                sharedRegionIds,
                definitions,
                document.Reason,
                document.EvidenceRefs));
    }

    private static void ValidateSharedPartMatchesBaseline(
        string path,
        IReadOnlyList<string> sharedRegionIds,
        IReadOnlyList<string> metadataDefinitionIds,
        FirmwareImageMap baselineMap,
        IReadOnlyDictionary<string, FirmwareMetadataStructure> baselineStructures,
        FirmwareImageMap candidateMap,
        IReadOnlyDictionary<string, FirmwareMetadataStructure> candidateStructures)
    {
        foreach (string regionId in sharedRegionIds)
        {
            FirmwareRegion baseline = baselineMap.Regions.Single(region =>
                StringComparer.Ordinal.Equals(region.RegionId, regionId));
            FirmwareRegion candidate = candidateMap.Regions.Single(region =>
                StringComparer.Ordinal.Equals(region.RegionId, regionId));
            if (baseline != candidate)
            {
                throw Error(
                    $"{path}.sharedRegionIds",
                    $"Shared region '{regionId}' differs between maps '{baselineMap.MapId}' and " +
                    $"'{candidateMap.MapId}'.");
            }
        }

        foreach (string definitionId in metadataDefinitionIds)
        {
            FirmwareMetadataStructureDefinition baselineDefinition = ResolveSharedDefinition(
                baselineStructures,
                definitionId,
                path);
            FirmwareMetadataStructureDefinition candidateDefinition = ResolveSharedDefinition(
                candidateStructures,
                definitionId,
                path);
            if (!ReferenceEquals(baselineDefinition, candidateDefinition))
            {
                throw Error(
                    $"{path}.metadataDefinitionIds",
                    $"Shared metadata definition '{definitionId}' does not reuse one canonical definition.");
            }
        }
    }

    private static FirmwareMetadataStructureDefinition ResolveSharedDefinition(
        IReadOnlyDictionary<string, FirmwareMetadataStructure> structures,
        string definitionId,
        string path)
    {
        FirmwareMetadataStructureDefinition[] matches =
        [
            .. structures.Values
                .Where(structure => StringComparer.Ordinal.Equals(
                    structure.Definition.DefinitionId,
                    definitionId))
                .Select(static structure => structure.Definition),
        ];
        return matches.Length != 0 &&
            matches.Skip(1).All(candidate => ReferenceEquals(matches[0], candidate))
            ? matches[0]
            : throw Error(
                $"{path}.metadataDefinitionIds",
                $"Metadata definition '{definitionId}' is missing or ambiguous in a shared-part map.");
    }
}
