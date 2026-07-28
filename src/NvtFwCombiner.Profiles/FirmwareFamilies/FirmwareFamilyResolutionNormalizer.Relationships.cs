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
        var mapsById = maps.ToDictionary(
            static map => map.MapId,
            StringComparer.Ordinal);
        var relationshipIds = new HashSet<string>(StringComparer.Ordinal);
        var perfectFamilyMembers = new HashSet<string>(StringComparer.Ordinal);
        var sharedBindings = new HashSet<SharedBindingKey>();
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

            normalized[index] = relationship switch
            {
                FirmwarePerfectFamilyRelationshipDocument perfect =>
                    NormalizePerfectFamilyRelationship(
                        perfect,
                        path,
                        maps,
                        aliases,
                        capabilities,
                        perfectFamilyMembers),
                FirmwareSharedFactRelationshipDocument shared =>
                    NormalizeSharedFactRelationship(
                        shared,
                        path,
                        mapsById,
                        structuresByMap,
                        sharedBindings),
                _ => throw Error(path, "Unknown family relationship shape."),
            };
        }

        return normalized;
    }

    private static PerfectFamilyRelationship NormalizePerfectFamilyRelationship(
        FirmwarePerfectFamilyRelationshipDocument document,
        string path,
        IReadOnlyList<FirmwareImageMap> maps,
        IReadOnlyList<AliasDeclaration> aliases,
        IReadOnlyList<FirmwareMapFactBinding<FirmwareCapabilityFact>> capabilities,
        HashSet<string> perfectFamilyMembers)
    {
        foreach (string memberId in document.MemberIds)
        {
            if (!perfectFamilyMembers.Add(memberId))
            {
                throw Error(
                    $"{path}.memberIds",
                    $"Member '{memberId}' has more than one perfect-family relationship.");
            }
        }

        var memberIds = new HashSet<string>(document.MemberIds, StringComparer.Ordinal);
        FirmwareImageMap[] relatedMaps =
        [
            .. maps.Where(map => map.Applicability.MemberIds.Any(memberIds.Contains)),
        ];
        if (relatedMaps.Length == 0 ||
            document.MemberIds.Any(memberId =>
                relatedMaps.All(map => !map.Applicability.MemberIds.Contains(memberId, StringComparer.Ordinal))))
        {
            throw Error(path, "Every perfect-family member must be selected by a family-owned map.");
        }

        foreach (FirmwareImageMap map in relatedMaps)
        {
            if (!memberIds.SetEquals(map.Applicability.MemberIds))
            {
                throw Error(
                    path,
                    $"Perfect-family relationship '{document.RelationshipId}' contains member-specific map " +
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
                "Perfect-family firmware semantics cannot be represented by member-specific fact aliases."),
            (_, true) => throw Error(
                path,
                "Perfect-family firmware semantics cannot contain member-specific capability facts."),
            _ => TranslateInvariant(path, () => new PerfectFamilyRelationship(
                    document.RelationshipId,
                    document.MemberIds,
                    document.Reason,
                    document.EvidenceRefs)),
        };
    }

    private static SharedFactRelationship NormalizeSharedFactRelationship(
        FirmwareSharedFactRelationshipDocument document,
        string path,
        Dictionary<string, FirmwareImageMap> mapsById,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>> structuresByMap,
        HashSet<SharedBindingKey> sharedBindings)
    {
        FirmwareSharedFactRole role = NormalizeSharedFactRole(document.Role, $"{path}.role");
        FirmwareSharedFactApplicabilityDocument applicability =
            document.Applicability ?? throw Error(
                $"{path}.applicability",
                "Shared-fact relationships require explicit map applicability.");
        IReadOnlyList<string> mapIds = RequireList(
            applicability.MapIds,
            $"{path}.applicability.mapIds");
        ValidateDistinctFactIds(mapIds, $"{path}.applicability.mapIds");
        if (mapIds.Count == 0)
        {
            throw Error(
                $"{path}.applicability.mapIds",
                "Shared-fact relationships require at least one applicable map.");
        }

        var applicableMaps = new FirmwareImageMap[mapIds.Count];
        var relationshipMembers = new HashSet<string>(document.MemberIds, StringComparer.Ordinal);
        for (int index = 0; index < mapIds.Count; index++)
        {
            string mapId = mapIds[index];
            if (!mapsById.TryGetValue(mapId, out FirmwareImageMap? map))
            {
                throw Error(
                    $"{path}.applicability.mapIds[{index}]",
                    $"Unknown shared-fact applicability map '{mapId}'.");
            }

            if (map.Applicability.MemberIds.Any(memberId => !relationshipMembers.Contains(memberId)))
            {
                throw Error(
                    $"{path}.applicability.mapIds[{index}]",
                    $"Map '{mapId}' admits a member outside relationship '{document.RelationshipId}'.");
            }

            applicableMaps[index] = map;
        }

        foreach (string memberId in document.MemberIds)
        {
            if (applicableMaps.All(map =>
                    !map.Applicability.MemberIds.Contains(memberId, StringComparer.Ordinal)))
            {
                throw Error(
                    $"{path}.applicability.mapIds",
                    $"Shared-fact applicability does not cover member '{memberId}'.");
            }
        }

        IReadOnlyList<FirmwareSharedFactReferenceDocument> referenceDocuments = RequireList(
            document.SharedFactReferences,
            $"{path}.sharedFactReferences");
        if (referenceDocuments.Count == 0)
        {
            throw Error(
                $"{path}.sharedFactReferences",
                "Shared-fact relationships require at least one typed fact reference.");
        }

        var referenceKeys = new HashSet<(FirmwareSharedFactKind Kind, string FactId)>();
        var references = new FirmwareSharedFactReference[referenceDocuments.Count];
        for (int index = 0; index < referenceDocuments.Count; index++)
        {
            FirmwareSharedFactReferenceDocument reference =
                referenceDocuments[index] ?? throw Error(
                    $"{path}.sharedFactReferences[{index}]",
                    "Shared-fact reference cannot be null.");
            string referencePath = $"{path}.sharedFactReferences[{index}]";
            FirmwareSharedFactKind kind = NormalizeSharedFactKind(
                reference.FactKind,
                $"{referencePath}.factKind");
            if (!referenceKeys.Add((kind, reference.FactId)))
            {
                throw Error(
                    referencePath,
                    $"Duplicate shared-fact reference '{kind}:{reference.FactId}'.");
            }

            references[index] = kind switch
            {
                FirmwareSharedFactKind.Region => ResolveSharedRegionReference(
                    reference.FactId,
                    applicableMaps,
                    referencePath),
                FirmwareSharedFactKind.MetadataDefinition =>
                    ResolveSharedMetadataDefinitionReference(
                        reference.FactId,
                        applicableMaps,
                        structuresByMap,
                        referencePath),
                _ => throw Error(
                    $"{referencePath}.factKind",
                    "Unknown shared firmware fact kind."),
            };
        }

        SharedFactRelationship normalized = TranslateInvariant(path, () => new SharedFactRelationship(
                document.RelationshipId,
                role,
                document.MemberIds,
                applicableMaps,
                references,
                document.Reason,
                document.EvidenceRefs));
        foreach (FirmwareImageMap map in normalized.ApplicableMaps)
        {
            foreach (FirmwareSharedFactReference reference in normalized.SharedFactReferences)
            {
                var key = new SharedBindingKey(map.MapId, reference.Kind, reference.FactId);
                if (!sharedBindings.Add(key))
                {
                    throw Error(
                        $"{path}.sharedFactReferences",
                        $"Canonical fact '{reference.Kind}:{reference.FactId}' has more than one " +
                        $"shared relationship on map '{map.MapId}'.");
                }
            }
        }

        return normalized;
    }

    private static FirmwareSharedFactReference ResolveSharedRegionReference(
        string regionId,
        IReadOnlyList<FirmwareImageMap> applicableMaps,
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        FirmwareRegion? baseline = null;
        FirmwareImageMap? baselineMap = null;
        foreach (FirmwareImageMap map in applicableMaps)
        {
            FirmwareRegion[] matches =
            [
                .. map.Regions.Where(region =>
                    StringComparer.Ordinal.Equals(region.RegionId, regionId)),
            ];
            if (matches.Length != 1)
            {
                throw Error(
                    $"{path}.factId",
                    $"Map '{map.MapId}' does not expose exactly one shared region '{regionId}'.");
            }

            baseline ??= matches[0];
            baselineMap ??= map;
            if (!ReferenceEquals(baseline, matches[0]))
            {
                throw Error(
                    $"{path}.factId",
                    $"Shared region '{regionId}' does not reuse one canonical region between maps " +
                    $"'{baselineMap.MapId}' and '{map.MapId}'.");
            }
        }

        return FirmwareSharedFactReference.ForRegion(
            baseline ?? throw Error(path, "Shared region resolution requires an applicable map."));
    }

    private static FirmwareSharedFactReference ResolveSharedMetadataDefinitionReference(
        string definitionId,
        IReadOnlyList<FirmwareImageMap> applicableMaps,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>> structuresByMap,
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        FirmwareMetadataStructureDefinition? baseline = null;
        FirmwareImageMap? baselineMap = null;
        foreach (FirmwareImageMap map in applicableMaps)
        {
            FirmwareMetadataStructureDefinition definition = ResolveSharedDefinition(
                structuresByMap[map.MapId],
                definitionId,
                path);
            baseline ??= definition;
            baselineMap ??= map;
            if (!ReferenceEquals(baseline, definition))
            {
                throw Error(
                    $"{path}.factId",
                    $"Shared metadata definition '{definitionId}' does not reuse one canonical definition " +
                    $"between maps '{baselineMap.MapId}' and '{map.MapId}'.");
            }
        }

        return FirmwareSharedFactReference.ForMetadataDefinition(
            baseline ?? throw Error(path, "Shared metadata resolution requires an applicable map."));
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
                $"{path}.factId",
                $"Metadata definition '{definitionId}' is missing or ambiguous in an applicable map.");
    }

    private static FirmwareSharedFactRole NormalizeSharedFactRole(string token, string path)
    {
        return token switch
        {
            "initial-code-shared" => FirmwareSharedFactRole.InitialCodeShared,
            "tp-shared" => FirmwareSharedFactRole.TpShared,
            "tp-flash-header-shared" => FirmwareSharedFactRole.TpFlashHeaderShared,
            "diffdlm-shared" => FirmwareSharedFactRole.DiffDlmShared,
            _ => throw Error(path, $"Unknown shared-fact role '{token}'."),
        };
    }

    private static FirmwareSharedFactKind NormalizeSharedFactKind(string token, string path)
    {
        return token switch
        {
            "region" => FirmwareSharedFactKind.Region,
            "metadata-definition" => FirmwareSharedFactKind.MetadataDefinition,
            _ => throw Error(path, $"Unknown shared firmware fact kind '{token}'."),
        };
    }

    private sealed record SharedBindingKey(
        string MapId,
        FirmwareSharedFactKind Kind,
        string FactId);
}
