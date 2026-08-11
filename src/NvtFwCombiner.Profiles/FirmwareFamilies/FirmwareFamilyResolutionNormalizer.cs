using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

/// <summary>Normalizes schema-validated v1 family facts into one map-bound Domain definition.</summary>
internal static partial class FirmwareFamilyResolutionNormalizer
{
    /// <summary>Normalizes direct and aliased family facts without inferring maps, workflows, or execution support.</summary>
    internal static FirmwareFamilyResolutionDefinition Normalize(
        FirmwareFamilyDocument document,
        string familyContentHash,
        IFirmwareMetadataStructureDefinitionResolver? metadataDefinitionResolver = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyContentHash);
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
        FirmwareMetadataPredicate[] predicates = NormalizeItems(
            predicateDocuments,
            $"{path}.metadataPredicates",
            (predicate, predicatePath) => NormalizePredicate(
                predicate,
                selectedStructures,
                predicatePath));

        return TranslateInvariant(path, () => new FirmwareMapApplicability(
                document.MemberIds,
                document.ModeIds,
                NormalizeTopology(document.TopologyRequirement, $"{path}.topologyRequirement"),
                ReadInt64(document.CapacityBytes, $"{path}.capacityBytes"),
                document.CommonFirmwareCategoryIds,
                predicates));
    }

    private static FirmwareFactApplicability NormalizeFactApplicability(
        FirmwareAliasApplicabilityDocument document,
        IReadOnlyDictionary<string, FirmwareMetadataStructure> selectedStructures,
        string path)
    {
        IReadOnlyList<FirmwareMetadataPredicateDocument> predicateDocuments =
            document.MetadataPredicates ?? [];
        FirmwareMetadataPredicate[] predicates = NormalizeItems(
            predicateDocuments,
            $"{path}.metadataPredicates",
            (predicate, predicatePath) => NormalizePredicate(
                predicate,
                selectedStructures,
                predicatePath));

        return TranslateInvariant(path, () => new FirmwareFactApplicability(
                document.ModeIds,
                NormalizeTopology(document.TopologyRequirement, $"{path}.topologyRequirement"),
                ReadInt64(document.CapacityBytes, $"{path}.capacityBytes"),
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
        IReadOnlyList<System.Text.Json.JsonElement> expectedDocuments = document.ExpectedValues;
        FirmwareMetadataValue[] expectedValues = NormalizeItems(
            expectedDocuments,
            $"{path}.expectedValues",
            (expected, expectedPath) => NormalizeExpectedValue(expected, field, expectedPath));

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
        IReadOnlyList<string> memberIds,
        IReadOnlyDictionary<string, FirmwareFamilyMemberDocument> membersById,
        string path)
    {
        foreach (string memberId in memberIds)
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
            T value = values[index];
            string id = idSelector(value);
            if (!indexed.TryAdd(id, value))
            {
                throw Error($"{path}[{index}].{idProperty}", $"Duplicate identifier '{id}'.");
            }
        }

        return indexed;
    }

    private static TResult[] NormalizeItems<TSource, TResult>(
        IReadOnlyList<TSource> values,
        string path,
        Func<TSource, string, TResult> normalize)
    {
        var normalized = new TResult[values.Count];
        for (int index = 0; index < values.Count; index++)
        {
            normalized[index] = normalize(values[index], $"{path}[{index}]");
        }

        return normalized;
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
