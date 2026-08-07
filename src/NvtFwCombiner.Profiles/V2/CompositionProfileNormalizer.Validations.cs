using System.Text.Json;
using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    internal static ValidationRequirementDefinition NormalizeValidation(
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
            "pid-sanity" => Wrap(path, () => CanonicalValidationDefinitionRules.RequireProfileDefinition(
                new CompiledPidSanityValidation(
                    document.RuleId,
                    stage,
                    severity,
                    document.IssueCode,
                    NormalizeFieldReference(document.Field, $"{path}.field")))),
            "metadata-equality" => Wrap(path, () => CanonicalValidationDefinitionRules.RequireProfileDefinition(
                new CompiledMetadataEqualityValidation(
                    document.RuleId,
                    stage,
                    severity,
                    document.IssueCode,
                    NormalizeFieldReference(document.Left, $"{path}.left"),
                    NormalizeFieldReference(document.Right, $"{path}.right")))),
            "reject-metadata-byte-pattern" => NormalizeRejectedPatterns(document, stage, severity, path),
            "view-byte-assertion" => NormalizeViewAssertion(document, stage, severity, path),
            "non-uniform-region" => Wrap(path, () => new SourceViewNonUniformValidationDefinition(
                document.RuleId,
                stage,
                severity,
                document.IssueCode,
                document.ViewId!)),
            _ => throw Error($"{path}.kind", "Unknown profile validation kind."),
        };
    }

    private static CompiledMetadataValueValidation NormalizeMetadataValue(
        CompositionProfileValidationDocument document,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string path)
    {
        IReadOnlyList<JsonElement> valueDocuments = document.ExpectedValues!;
        var expectedValues = new CompiledValidationScalarLiteral[valueDocuments.Count];
        for (int index = 0; index < valueDocuments.Count; index++)
        {
            expectedValues[index] = NormalizeScalarLiteral(
                valueDocuments[index],
                $"{path}.expectedValues[{index}]");
        }

        return Wrap(path, () => CanonicalValidationDefinitionRules.RequireProfileDefinition(
            new CompiledMetadataValueValidation(
                document.RuleId,
                stage,
                severity,
                document.IssueCode,
                NormalizeFieldReference(document.Field, $"{path}.field"),
                NormalizeMetadataComparison(
                    document.Operator!,
                    $"{path}.operator"),
                expectedValues)));
    }

    private static CompiledRejectMetadataBytePatternValidation NormalizeRejectedPatterns(
        CompositionProfileValidationDocument document,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string path)
    {
        IReadOnlyList<string> patternDocuments = document.RejectedPatterns!;
        var patterns = new CompiledValidationRejectedBytePattern[patternDocuments.Count];
        for (int index = 0; index < patternDocuments.Count; index++)
        {
            patterns[index] = NormalizeRejectedPattern(
                patternDocuments[index],
                $"{path}.rejectedPatterns[{index}]");
        }

        return Wrap(path, () => CanonicalValidationDefinitionRules.RequireProfileDefinition(
            new CompiledRejectMetadataBytePatternValidation(
                document.RuleId,
                stage,
                severity,
                document.IssueCode,
                NormalizeFieldReference(document.Field, $"{path}.field"),
                patterns)));
    }

    private static CompiledViewByteAssertionValidation NormalizeViewAssertion(
        CompositionProfileValidationDocument document,
        CompiledValidationStage stage,
        CompiledValidationSeverity severity,
        string path)
    {
        CompiledValidationBytes expected = ReadBytes(
            document.ExpectedHex!,
            $"{path}.expectedHex");
        CompiledValidationBytes? mask = document.MaskHex is { } maskHex
            ? ReadBytes(maskHex, $"{path}.maskHex")
            : null;
        return Wrap(path, () => CanonicalValidationDefinitionRules.RequireProfileDefinition(
            new CompiledViewByteAssertionValidation(
                document.RuleId,
                stage,
                severity,
                document.IssueCode,
                document.ViewId!,
                expected,
                mask)));
    }

    private static CompiledValidationFieldReference NormalizeFieldReference(
        CompositionProfileMetadataFieldReferenceDocument? document,
        string path)
    {
        return Wrap(path, () => CanonicalValidationDefinitionRules.RequireProfileField(
            new CompiledValidationFieldReference(document!.BindingId, document.FieldId)));
    }

    private static CompiledValidationScalarLiteral NormalizeScalarLiteral(JsonElement value, string path)
    {
        return value.ValueKind == JsonValueKind.Number
            ? new CompiledValidationIntegerLiteral(ReadInteger(value, path))
            : value.ValueKind == JsonValueKind.String
                ? Wrap(path, () => new CompiledValidationTextLiteral(ReadString(value, path)))
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
