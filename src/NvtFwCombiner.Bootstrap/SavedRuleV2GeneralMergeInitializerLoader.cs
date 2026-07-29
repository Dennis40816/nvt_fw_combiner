using System.Text.Json;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Typed Saved Rule v2 projection for the General Merge initializer slice.</summary>
internal sealed record SavedRuleV2GeneralMergeInitializerLoadResult(
    GeneralMergeOutputInitializer? Initializer,
    IReadOnlyList<SavedRuleValidationIssue> Issues)
{
    internal bool IsValid => Initializer is not null && Issues.Count == 0;
}

/// <summary>
/// Strictly loads the Saved Rule v2 initialization contract without claiming
/// the exact-parent lifecycle owned by the later complete v2 rule loader.
/// </summary>
internal static class SavedRuleV2GeneralMergeInitializerLoader
{
    private static readonly HashSet<string> TopLevelProperties =
    [
        "schemaVersion",
        "ruleId",
        "ruleVersion",
        "displayName",
        "description",
        "compositionKind",
        "sourceExperienceId",
        "imageInitialization",
        "parentBinding",
        "promotion",
        "slotTemplates",
        "mappingFragments",
        "accessEnvelope",
        "validationRuleIds",
        "processorStageIds",
        "owner",
        "reviewers",
        "evidenceRefs",
    ];

    private static readonly HashSet<string> InitializationProperties =
    [
        "kind",
        "capacity",
        "fillByte",
    ];

    /// <summary>Loads one exact v2 initializer and rejects Replace declarations.</summary>
    internal static SavedRuleV2GeneralMergeInitializerLoadResult Parse(
        JsonElement root)
    {
        List<SavedRuleValidationIssue> issues = [];
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new SavedRuleV2GeneralMergeInitializerLoadResult(
                null,
                [Issue(
                    SavedRuleIssueCodes.RootInvalid,
                    "Saved Rule v2 root must be an object.",
                    "$")]);
        }

        ValidateProperties(root, TopLevelProperties, "$", issues);
        string schemaVersion = ReadString(
            root,
            "schemaVersion",
            "$.schemaVersion",
            issues);
        if (!StringComparer.Ordinal.Equals(schemaVersion, "2.0"))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.SchemaVersionUnsupported,
                "Saved Rule initializer schemaVersion must be '2.0'.",
                "$.schemaVersion"));
        }

        string compositionKind = ReadString(
            root,
            "compositionKind",
            "$.compositionKind",
            issues);
        string sourceExperienceId = ReadString(
            root,
            "sourceExperienceId",
            "$.sourceExperienceId",
            issues);
        bool isMerge =
            StringComparer.Ordinal.Equals(
                compositionKind,
                SavedRuleSchemaTokens.CompositionKindMerge) &&
            StringComparer.Ordinal.Equals(
                sourceExperienceId,
                ExperienceIds.GeneralMerge);
        bool isReplace =
            StringComparer.Ordinal.Equals(
                compositionKind,
                SavedRuleSchemaTokens.CompositionKindReplace) &&
            StringComparer.Ordinal.Equals(
                sourceExperienceId,
                ExperienceIds.GeneralReplace);
        if (!isMerge && !isReplace)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.ExperienceKindMismatch,
                "Saved Rule v2 composition kind and source experience must identify General Merge or General Replace.",
                "$.sourceExperienceId"));
        }

        bool hasInitialization = root.TryGetProperty(
            "imageInitialization",
            out JsonElement initializationElement);
        if (isReplace && hasInitialization)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.InitializerForbidden,
                "General Replace cannot declare a blank-output initializer.",
                "$.imageInitialization"));
            return new SavedRuleV2GeneralMergeInitializerLoadResult(
                null,
                issues);
        }

        if (!isMerge)
        {
            return new SavedRuleV2GeneralMergeInitializerLoadResult(
                null,
                issues);
        }

        if (!hasInitialization)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.InitializerRequired,
                "General Merge Saved Rule v2 requires imageInitialization.",
                "$.imageInitialization"));
            return new SavedRuleV2GeneralMergeInitializerLoadResult(
                null,
                issues);
        }

        GeneralMergeOutputInitializer? initializer =
            ReadInitializer(initializationElement, issues);
        return new SavedRuleV2GeneralMergeInitializerLoadResult(
            issues.Count == 0 ? initializer : null,
            issues);
    }

    /// <summary>Serializes the canonical typed value using the normative v2 shape.</summary>
    internal static JsonElement Serialize(
        GeneralMergeOutputInitializer initializer)
    {
        ArgumentNullException.ThrowIfNull(initializer);
        return JsonSerializer.SerializeToElement(new
        {
            kind = "blank",
            capacity = initializer.Capacity,
            fillByte = initializer.FillByte,
        });
    }

    private static GeneralMergeOutputInitializer? ReadInitializer(
        JsonElement element,
        List<SavedRuleValidationIssue> issues)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.InitializerInvalid,
                "imageInitialization must be an object.",
                "$.imageInitialization"));
            return null;
        }

        ValidateProperties(
            element,
            InitializationProperties,
            "$.imageInitialization",
            issues);
        string kind = ReadString(
            element,
            "kind",
            "$.imageInitialization.kind",
            issues);
        if (!StringComparer.Ordinal.Equals(kind, "blank"))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.InitializerInvalid,
                "General Merge imageInitialization.kind must be 'blank'.",
                "$.imageInitialization.kind"));
        }

        long capacity = 0;
        if (!element.TryGetProperty(
                "capacity",
                out JsonElement capacityElement) ||
            !capacityElement.TryGetInt64(out capacity) ||
            capacity <= 0 ||
            capacity > GeneralMergeOutputInitializer.MaximumCapacity)
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.InitializerCapacityInvalid,
                $"General Merge imageInitialization.capacity must be in 1..{GeneralMergeOutputInitializer.MaximumCapacity}.",
                "$.imageInitialization.capacity"));
        }

        byte fillByte = GeneralMergeOutputInitializer.DefaultFillByte;
        if (element.TryGetProperty(
                "fillByte",
                out JsonElement fillElement) &&
            (!fillElement.TryGetInt32(out int fill) ||
             fill is < byte.MinValue or > byte.MaxValue))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.InitializerFillByteInvalid,
                "General Merge imageInitialization.fillByte must be in 0..255.",
                "$.imageInitialization.fillByte"));
        }
        else if (fillElement.ValueKind != JsonValueKind.Undefined)
        {
            fillByte = checked((byte)fillElement.GetInt32());
        }

        return issues.Count == 0
            ? new GeneralMergeOutputInitializer(capacity, fillByte)
            : null;
    }

    private static string ReadString(
        JsonElement element,
        string propertyName,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        if (!element.TryGetProperty(
                propertyName,
                out JsonElement property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            issues.Add(Issue(
                SavedRuleIssueCodes.StringRequired,
                $"Property '{propertyName}' must be a non-empty string.",
                path));
            return string.Empty;
        }

        return property.GetString()!;
    }

    private static void ValidateProperties(
        JsonElement element,
        HashSet<string> allowed,
        string path,
        List<SavedRuleValidationIssue> issues)
    {
        HashSet<string> seen = [];
        foreach (JsonProperty property in element.EnumerateObject())
        {
            string propertyPath = $"{path}.{property.Name}";
            if (!seen.Add(property.Name))
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.PropertyDuplicate,
                    $"Property '{property.Name}' is duplicated.",
                    propertyPath));
            }

            if (!allowed.Contains(property.Name))
            {
                issues.Add(Issue(
                    SavedRuleIssueCodes.PropertyUnknown,
                    $"Property '{property.Name}' is not allowed.",
                    propertyPath));
            }
        }
    }

    private static SavedRuleValidationIssue Issue(
        string code,
        string message,
        string path)
    {
        return new SavedRuleValidationIssue(code, message, path);
    }
}
