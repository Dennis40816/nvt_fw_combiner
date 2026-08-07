using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

internal static partial class FirmwareFamilyResolutionNormalizer
{
    private static FirmwareFamilyRelationship[] NormalizeFamilyRelationships(
        FirmwareFamilyDocument document,
        IReadOnlyList<FirmwareImageMap> maps,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>> structuresByMap)
    {
        IReadOnlyList<FirmwareFamilyRelationshipDocument> relationshipDocuments =
            document.FamilyRelationships ?? [];
        if (relationshipDocuments.Count == 0)
        {
            return [];
        }

        Dictionary<string, FirmwareFamilyMemberDocument> membersById = IndexUnique(
            document.Members,
            static member => member.MemberId,
            "members",
            "memberId");
        var mapsById = maps.ToDictionary(
            static map => map.MapId,
            StringComparer.Ordinal);
        return NormalizeItems<FirmwareFamilyRelationshipDocument, FirmwareFamilyRelationship>(
            relationshipDocuments,
            "familyRelationships",
            (relationship, path) =>
            {
                ValidateMemberReferences(relationship.MemberIds, membersById, $"{path}.memberIds");
                return relationship switch
                {
                    FirmwarePerfectFamilyRelationshipDocument perfect =>
                        NormalizePerfectFamilyRelationship(perfect, path),
                    FirmwareSharedFactRelationshipDocument shared =>
                        NormalizeSharedFactRelationship(
                            shared,
                            path,
                            mapsById,
                            structuresByMap),
                    _ => throw Error(path, "Unknown family relationship shape."),
                };
            });
    }

    private static PerfectFamilyRelationship NormalizePerfectFamilyRelationship(
        FirmwarePerfectFamilyRelationshipDocument document,
        string path)
    {
        return TranslateInvariant(path, () => new PerfectFamilyRelationship(
                document.RelationshipId,
                document.MemberIds,
                document.Reason,
                document.EvidenceRefs));
    }

    private static SharedFactRelationship NormalizeSharedFactRelationship(
        FirmwareSharedFactRelationshipDocument document,
        string path,
        Dictionary<string, FirmwareImageMap> mapsById,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>> structuresByMap)
    {
        FirmwareSharedFactRole role = NormalizeSharedFactRole(document.Role, $"{path}.role");
        IReadOnlyList<string> mapIds = document.Applicability.MapIds;

        FirmwareImageMap[] applicableMaps = NormalizeItems(
            mapIds,
            $"{path}.applicability.mapIds",
            (mapId, mapPath) =>
            {
                return mapsById.TryGetValue(mapId, out FirmwareImageMap? map)
                    ? map
                    : throw Error(
                        mapPath,
                        $"Unknown shared-fact applicability map '{mapId}'.");
            });

        IReadOnlyList<FirmwareSharedFactReferenceDocument> referenceDocuments =
            document.SharedFactReferences;
        FirmwareSharedFactReference[] references = NormalizeItems(
            referenceDocuments,
            $"{path}.sharedFactReferences",
            (reference, referencePath) => NormalizeSharedFactKind(
                reference.FactKind,
                $"{referencePath}.factKind") switch
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
            });

        return TranslateInvariant(path, () => new SharedFactRelationship(
                document.RelationshipId,
                role,
                document.MemberIds,
                applicableMaps,
                references,
                document.Reason,
                document.EvidenceRefs));
    }

    private static FirmwareSharedFactReference ResolveSharedRegionReference(
        string regionId,
        FirmwareImageMap[] applicableMaps,
        string path)
    {
        FirmwareImageMap map = applicableMaps[0];
        FirmwareRegion[] matches =
        [
            .. map.Regions.Where(region =>
                StringComparer.Ordinal.Equals(region.RegionId, regionId)),
        ];
        return matches.Length == 1
            ? FirmwareSharedFactReference.ForRegion(matches[0])
            : throw Error(
                $"{path}.factId",
                $"Map '{map.MapId}' does not expose exactly one shared region '{regionId}'.");
    }

    private static FirmwareSharedFactReference ResolveSharedMetadataDefinitionReference(
        string definitionId,
        FirmwareImageMap[] applicableMaps,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>> structuresByMap,
        string path)
    {
        FirmwareImageMap map = applicableMaps[0];
        return FirmwareSharedFactReference.ForMetadataDefinition(ResolveSharedDefinition(
            structuresByMap[map.MapId],
            definitionId,
            path));
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

}
