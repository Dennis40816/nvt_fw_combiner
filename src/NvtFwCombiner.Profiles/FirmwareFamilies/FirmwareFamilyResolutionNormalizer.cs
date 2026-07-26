using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

/// <summary>Normalizes schema-validated v1.1 family facts into one map-bound Domain definition.</summary>
public static partial class FirmwareFamilyResolutionNormalizer
{
    /// <summary>Normalizes direct and aliased family facts without inferring maps, workflows, or execution support.</summary>
    public static FirmwareFamilyResolutionDefinition Normalize(
        FirmwareFamilyDocument document,
        string familyContentHash,
        IFirmwareMetadataStructureDefinitionResolver? metadataDefinitionResolver = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyContentHash);
        _ = StringComparer.Ordinal.Equals(document.SchemaVersion, "1.1")
            ? true
            : throw Error("schemaVersion", "Expected firmware-family schema version '1.1'.");

        return NormalizeMapBoundFacts(
            document,
            familyContentHash,
            metadataDefinitionResolver);
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

        return TranslateInvariant(path, () => new FirmwareMapApplicability(
                document.MemberIds,
                document.ModeIds,
                NormalizeTopology(document.TopologyRequirement, $"{path}.topologyRequirement"),
                ReadInt64(document.CapacityBytes, 1, long.MaxValue, $"{path}.capacityBytes"),
                document.CommonFirmwareCategoryIds,
                predicates));
    }

    private static FirmwareFactApplicability NormalizeFactApplicability(
        FirmwareAliasApplicabilityDocument document,
        IReadOnlyDictionary<string, FirmwareMetadataStructure> selectedStructures,
        string path)
    {
        ArgumentNullException.ThrowIfNull(document);
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

        return TranslateInvariant(path, () => new FirmwareFactApplicability(
                document.ModeIds,
                NormalizeTopology(document.TopologyRequirement, $"{path}.topologyRequirement"),
                ReadInt64(document.CapacityBytes, 1, long.MaxValue, $"{path}.capacityBytes"),
                document.CommonFirmwareCategoryIds,
                predicates));
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

        return TranslateInvariant(path, () => new FirmwareMetadataPredicate(
                document.MetadataStructureId,
                document.FieldId,
                comparison,
                expectedValues));
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

    private static T TranslateInvariant<T>(string path, Func<T> factory)
    {
        try
        {
            return factory();
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw Error(path, exception.Message, exception);
        }
    }

    private static void TranslateInvariant(string path, Action action)
    {
        _ = TranslateInvariant(path, () =>
        {
            action();
            return true;
        });
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
