using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

/// <summary>Normalizes schema-validated alias-free family facts into the Domain resolution subset.</summary>
public static partial class FirmwareFamilyResolutionNormalizer
{
    /// <summary>Normalizes direct family facts and rejects aliases until provenance expansion is supplied.</summary>
    public static FirmwareFamilyResolutionDefinition NormalizeAliasFree(
        FirmwareFamilyDocument document,
        string familyContentHash)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyContentHash);
        if (!StringComparer.Ordinal.Equals(document.SchemaVersion, "1.0"))
        {
            throw Error("schemaVersion", "Expected firmware-family schema version '1.0'.");
        }

        IReadOnlyList<FirmwareFamilyMemberDocument> members =
            RequireList(document.Members, "members");
        Dictionary<string, FirmwareFamilyMemberDocument> membersById = IndexUnique(
            members,
            static member => member.MemberId,
            "members",
            "memberId");
        ValidateCapabilities(document.Capabilities, membersById);

        IReadOnlyList<FirmwareFactAliasDocument> aliases =
            RequireList(document.FactAliases, "factAliases");
        if (aliases.Count != 0)
        {
            throw Error(
                "factAliases",
                "Alias-free normalization cannot discard unresolved alias declarations.");
        }

        IReadOnlyList<FirmwareRegionSetDocument> regionSetDocuments =
            RequireList(document.RegionSets, "regionSets");
        Dictionary<string, FirmwareRegionSet> regionSetsById = NormalizeRegionSets(regionSetDocuments);
        IReadOnlyList<FirmwareMetadataSetDocument> metadataSetDocuments =
            RequireList(document.MetadataSets, "metadataSets");
        Dictionary<string, FirmwareMetadataSet> metadataSetsById = NormalizeMetadataSets(metadataSetDocuments);
        ValidateGlobalStructureIds(metadataSetsById.Values);

        IReadOnlyList<FirmwareImageMapDocument> mapDocuments =
            RequireList(document.ImageMaps, "imageMaps");
        HashSet<string> referencedRegionSetIds = new(StringComparer.Ordinal);
        var maps = new FirmwareImageMap[mapDocuments.Count];
        for (int index = 0; index < mapDocuments.Count; index++)
        {
            maps[index] = NormalizeMap(
                mapDocuments[index],
                index,
                membersById,
                regionSetsById,
                metadataSetsById,
                referencedRegionSetIds);
        }

        string? orphanRegionSetId = regionSetsById.Keys.FirstOrDefault(id =>
            !referencedRegionSetIds.Contains(id));
        if (orphanRegionSetId is not null)
        {
            throw Error("regionSets", $"Region set '{orphanRegionSetId}' is not referenced by an image map.");
        }

        try
        {
            return new FirmwareFamilyResolutionDefinition(
                document.FamilyId,
                document.FamilyVersion,
                familyContentHash,
                maps,
                metadataSetsById.Values);
        }
        catch (ArgumentException exception)
        {
            throw Error("$", exception.Message, exception);
        }
        catch (OverflowException exception)
        {
            throw Error("$", exception.Message, exception);
        }
    }

    private static FirmwareImageMap NormalizeMap(
        FirmwareImageMapDocument document,
        int mapIndex,
        IReadOnlyDictionary<string, FirmwareFamilyMemberDocument> membersById,
        IReadOnlyDictionary<string, FirmwareRegionSet> regionSetsById,
        IReadOnlyDictionary<string, FirmwareMetadataSet> metadataSetsById,
        HashSet<string> referencedRegionSetIds)
    {
        string path = $"imageMaps[{mapIndex}]";
        ValidateMemberReferences(document.Applicability.MemberIds, membersById, $"{path}.applicability.memberIds");
        FirmwareRegionSet[] regionSets = ResolveReferences(
            document.RegionSetIds,
            regionSetsById,
            $"{path}.regionSetIds",
            referencedRegionSetIds);
        FirmwareMetadataSet[] metadataSets = ResolveReferences(
            document.MetadataSetIds,
            metadataSetsById,
            $"{path}.metadataSetIds");

        Dictionary<string, FirmwareMetadataStructure> selectedStructures = [];
        foreach (FirmwareMetadataStructure structure in metadataSets.SelectMany(static set => set.Structures))
        {
            if (!selectedStructures.TryAdd(structure.StructureId, structure))
            {
                throw Error(
                    $"{path}.metadataSetIds",
                    $"Selected metadata structure id '{structure.StructureId}' is ambiguous.");
            }
        }

        FirmwareMapApplicability applicability = NormalizeApplicability(
            document.Applicability,
            selectedStructures,
            $"{path}.applicability");
        FirmwareImageMapCoveragePolicy coveragePolicy = document.CoveragePolicy switch
        {
            "complete-with-explicit-gaps" => FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            _ => throw Error($"{path}.coveragePolicy", "Unknown image-map coverage policy."),
        };

        try
        {
            return FirmwareImageMap.CreateDirect(
                document.MapId,
                document.AddressSpaceId,
                applicability,
                coveragePolicy,
                regionSets,
                metadataSets,
                document.EvidenceRefs);
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
        catch (OverflowException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static FirmwareMapApplicability NormalizeApplicability(
        FirmwareMapApplicabilityDocument document,
        IReadOnlyDictionary<string, FirmwareMetadataStructure> selectedStructures,
        string path)
    {
        IReadOnlyList<FirmwareMetadataPredicateDocument> predicateDocuments =
            document.MetadataPredicates ?? [];
        var predicates = new FirmwareMetadataPredicate[predicateDocuments.Count];
        for (int index = 0; index < predicateDocuments.Count; index++)
        {
            predicates[index] = NormalizePredicate(
                predicateDocuments[index],
                selectedStructures,
                $"{path}.metadataPredicates[{index}]");
        }

        try
        {
            return new FirmwareMapApplicability(
                document.MemberIds,
                document.ModeIds,
                NormalizeTopology(document.TopologyRequirement, $"{path}.topologyRequirement"),
                ReadInt64(document.CapacityBytes, 1, long.MaxValue, $"{path}.capacityBytes"),
                document.CommonFirmwareCategoryIds,
                predicates);
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
        catch (OverflowException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static FirmwareMetadataPredicate NormalizePredicate(
        FirmwareMetadataPredicateDocument document,
        IReadOnlyDictionary<string, FirmwareMetadataStructure> selectedStructures,
        string path)
    {
        if (!selectedStructures.TryGetValue(
            document.MetadataStructureId,
            out FirmwareMetadataStructure? structure))
        {
            throw Error(
                $"{path}.metadataStructureId",
                $"Structure '{document.MetadataStructureId}' is not selected by this image map.");
        }

        FirmwareMetadataField? field = structure.Fields.FirstOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.FieldId, document.FieldId)) ?? throw Error(
                $"{path}.fieldId",
                $"Field '{document.FieldId}' does not exist in structure '{structure.StructureId}'.");
        IReadOnlyList<System.Text.Json.JsonElement> expectedDocuments =
            RequireList(document.ExpectedValues, $"{path}.expectedValues");
        var expectedValues = new FirmwareMetadataValue[expectedDocuments.Count];
        for (int index = 0; index < expectedDocuments.Count; index++)
        {
            expectedValues[index] = NormalizeExpectedValue(
                expectedDocuments[index],
                field,
                $"{path}.expectedValues[{index}]");
        }

        FirmwareMetadataPredicateOperator comparison = document.Operator switch
        {
            "equals" => FirmwareMetadataPredicateOperator.Equal,
            "not-equals" => FirmwareMetadataPredicateOperator.NotEqual,
            "one-of" => FirmwareMetadataPredicateOperator.OneOf,
            _ => throw Error($"{path}.operator", "Unknown metadata predicate operator."),
        };

        try
        {
            return new FirmwareMetadataPredicate(
                document.MetadataStructureId,
                document.FieldId,
                comparison,
                expectedValues);
        }
        catch (ArgumentException exception)
        {
            throw Error(path, exception.Message, exception);
        }
        catch (OverflowException exception)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static void ValidateCapabilities(
        IReadOnlyList<FirmwareCapabilityDocument>? capabilities,
        IReadOnlyDictionary<string, FirmwareFamilyMemberDocument> membersById)
    {
        IReadOnlyList<FirmwareCapabilityDocument> required = RequireList(capabilities, "capabilities");
        _ = IndexUnique(required, static capability => capability.CapabilityId, "capabilities", "capabilityId");
        for (int index = 0; index < required.Count; index++)
        {
            ValidateMemberReferences(
                required[index].MemberIds,
                membersById,
                $"capabilities[{index}].memberIds");
        }
    }

    private static void ValidateMemberReferences(
        IReadOnlyList<string>? memberIds,
        IReadOnlyDictionary<string, FirmwareFamilyMemberDocument> membersById,
        string path)
    {
        IReadOnlyList<string> required = RequireList(memberIds, path);
        foreach (string memberId in required)
        {
            if (!membersById.ContainsKey(memberId))
            {
                throw Error(path, $"Unknown family member '{memberId}'.");
            }
        }
    }

    private static TResolved[] ResolveReferences<TResolved>(
        IReadOnlyList<string>? ids,
        IReadOnlyDictionary<string, TResolved> valuesById,
        string path,
        HashSet<string>? referencedIds = null)
    {
        IReadOnlyList<string> requiredIds = RequireList(ids, path);
        var resolved = new TResolved[requiredIds.Count];
        for (int index = 0; index < requiredIds.Count; index++)
        {
            string id = requiredIds[index];
            if (!valuesById.TryGetValue(id, out TResolved? value))
            {
                throw Error($"{path}[{index}]", $"Unknown reference '{id}'.");
            }

            resolved[index] = value;
            _ = referencedIds?.Add(id);
        }

        return resolved;
    }

    private static Dictionary<string, T> IndexUnique<T>(
        IReadOnlyList<T> values,
        Func<T, string> idSelector,
        string path,
        string idProperty)
        where T : class
    {
        Dictionary<string, T> indexed = new(StringComparer.Ordinal);
        for (int index = 0; index < values.Count; index++)
        {
            T value = values[index] ?? throw Error($"{path}[{index}]", "Value cannot be null.");
            string id = idSelector(value);
            if (!indexed.TryAdd(id, value))
            {
                throw Error($"{path}[{index}].{idProperty}", $"Duplicate identifier '{id}'.");
            }
        }

        return indexed;
    }

    private static IReadOnlyList<T> RequireList<T>(IReadOnlyList<T>? values, string path)
    {
        return values ?? throw Error(path, "Required array is missing.");
    }

    private static FirmwareFamilyNormalizationException Error(
        string path,
        string message,
        Exception? innerException = null)
    {
        return innerException is null
            ? new FirmwareFamilyNormalizationException(path, message)
            : new FirmwareFamilyNormalizationException(path, message, innerException);
    }
}
