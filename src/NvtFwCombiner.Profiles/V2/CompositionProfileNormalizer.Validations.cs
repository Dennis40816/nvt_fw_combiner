using System.Text.Json;
using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    internal static CompositionProfileValidation NormalizeValidation(
        CompositionProfileValidationDocument document,
        string path = "validations[0]")
    {
        ArgumentNullException.ThrowIfNull(document);
        CompiledValidationStage stage = NormalizeValidationStage(
            document.Stage,
            $"{path}.stage");
        CompiledValidationSeverity severity = NormalizeValidationSeverity(
            document.Severity,
            $"{path}.severity");

        return document.Kind switch
        {
            "metadata-value" => NormalizeMetadataValue(document, stage, severity, path),
            "pid-sanity" => Wrap(path, () => new PidSanityProfileValidation(
                document.RuleId,
                stage,
                severity,
                document.IssueCode,
                NormalizeFieldReference(document.Field, $"{path}.field"))),
            "metadata-equality" => Wrap(path, () => new MetadataEqualityProfileValidation(
                document.RuleId,
                stage,
                severity,
                document.IssueCode,
                NormalizeFieldReference(document.Left, $"{path}.left"),
                NormalizeFieldReference(document.Right, $"{path}.right"))),
            "reject-metadata-byte-pattern" => NormalizeRejectedPatterns(document, stage, severity, path),
            "view-byte-assertion" => NormalizeViewAssertion(document, stage, severity, path),
            "non-uniform-region" => Wrap(path, () => new NonUniformRegionProfileValidation(
                document.RuleId,
                stage,
                severity,
                document.IssueCode,
                RequireText(document.ViewId, $"{path}.viewId", "Non-uniform source view is missing."))),
            _ => throw Error($"{path}.kind", "Unknown profile validation kind."),
        };
    }

    private static MetadataValueProfileValidation NormalizeMetadataValue(
        CompositionProfileValidationDocument document,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string path)
    {
        IReadOnlyList<JsonElement> valueDocuments = RequireList(
            document.ExpectedValues,
            $"{path}.expectedValues");
        var expectedValues = new CompositionProfileScalarLiteral[valueDocuments.Count];
        for (int index = 0; index < valueDocuments.Count; index++)
        {
            expectedValues[index] = NormalizeScalarLiteral(
                valueDocuments[index],
                $"{path}.expectedValues[{index}]");
        }

        return Wrap(path, () => new MetadataValueProfileValidation(
            document.RuleId,
            stage,
            severity,
            document.IssueCode,
            NormalizeFieldReference(document.Field, $"{path}.field"),
            NormalizeMetadataComparison(
                RequireText(document.Operator, $"{path}.operator", "Metadata comparison is missing."),
                $"{path}.operator"),
            expectedValues));
    }

    private static RejectMetadataBytePatternProfileValidation NormalizeRejectedPatterns(
        CompositionProfileValidationDocument document,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string path)
    {
        IReadOnlyList<string> patternDocuments = RequireList(
            document.RejectedPatterns,
            $"{path}.rejectedPatterns");
        var patterns = new CompiledValidationRejectedBytePattern[patternDocuments.Count];
        for (int index = 0; index < patternDocuments.Count; index++)
        {
            patterns[index] = NormalizeRejectedPattern(
                patternDocuments[index],
                $"{path}.rejectedPatterns[{index}]");
        }

        return Wrap(path, () => new RejectMetadataBytePatternProfileValidation(
            document.RuleId,
            stage,
            severity,
            document.IssueCode,
            NormalizeFieldReference(document.Field, $"{path}.field"),
            patterns));
    }

    private static ViewByteAssertionProfileValidation NormalizeViewAssertion(
        CompositionProfileValidationDocument document,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string path)
    {
        CompositionProfileByteValue expected = ReadBytes(
            RequireText(document.ExpectedHex, $"{path}.expectedHex", "Expected bytes are missing."),
            $"{path}.expectedHex");
        CompositionProfileByteValue? mask = document.MaskHex is { } maskHex
            ? ReadBytes(maskHex, $"{path}.maskHex")
            : null;
        return Wrap(path, () => new ViewByteAssertionProfileValidation(
            document.RuleId,
            stage,
            severity,
            document.IssueCode,
            RequireText(document.ViewId, $"{path}.viewId", "Assertion view is missing."),
            expected,
            mask));
    }

    private static CompositionProfileMetadataFieldReference NormalizeFieldReference(
        CompositionProfileMetadataFieldReferenceDocument? document,
        string path)
    {
        CompositionProfileMetadataFieldReferenceDocument value = document ?? throw Error(
            path,
            "Metadata field reference is missing.");
        return Wrap(path, () => new CompositionProfileMetadataFieldReference(value.BindingId, value.FieldId));
    }

    private static CompositionProfileScalarLiteral NormalizeScalarLiteral(JsonElement value, string path)
    {
        return value.ValueKind == JsonValueKind.Number
            ? new CompositionProfileIntegerLiteral(ReadInteger(value, path))
            : value.ValueKind == JsonValueKind.String
                ? Wrap(path, () => new CompositionProfileTextLiteral(ReadString(value, path)))
                : throw Error(path, "Metadata scalar must be an integer or non-empty string.");
    }

    private static CompiledValidationStage NormalizeValidationStage(string value, string path)
    {
        return value switch
        {
            "profile-compile" => CompiledValidationStage.ProfileCompile,
            "input-load" => CompiledValidationStage.InputLoad,
            "pre-operation" => CompiledValidationStage.PreOperation,
            "post-operation" => CompiledValidationStage.PostOperation,
            "final-output" => CompiledValidationStage.FinalOutput,
            _ => throw Error(path, "Unknown validation stage."),
        };
    }

    private static CompiledValidationSeverity NormalizeValidationSeverity(string value, string path)
    {
        return value switch
        {
            "info" => CompiledValidationSeverity.Info,
            "warning" => CompiledValidationSeverity.Warning,
            "error" => CompiledValidationSeverity.Error,
            _ => throw Error(path, "Unknown validation severity."),
        };
    }

    private static CompiledValidationMetadataComparison NormalizeMetadataComparison(string value, string path)
    {
        return value switch
        {
            "equals" => CompiledValidationMetadataComparison.Equal,
            "not-equals" => CompiledValidationMetadataComparison.NotEqual,
            "one-of" => CompiledValidationMetadataComparison.OneOf,
            _ => throw Error(path, "Unknown metadata comparison."),
        };
    }

    private static CompiledValidationRejectedBytePattern NormalizeRejectedPattern(string value, string path)
    {
        return value switch
        {
            "all-zero" => CompiledValidationRejectedBytePattern.AllZero,
            "all-ff" => CompiledValidationRejectedBytePattern.AllFF,
            _ => throw Error(path, "Unknown rejected byte pattern."),
        };
    }
}
