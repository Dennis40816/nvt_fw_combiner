using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using System.Text.Json;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    internal static CompositionInputSlotDefinition NormalizeInputSlot(
        CompositionProfileInputSlotDocument document,
        string path = "inputSlots[0]")
    {
        CompositionProfileInputAcceptanceDocument acceptance = document.Acceptance;

        return Wrap(path, () => new CompositionInputSlotDefinition(
            document.SlotId,
            document.Role,
            NormalizeArtifactClass(document.ArtifactClass, $"{path}.artifactClass"),
            document.Required,
            NormalizeCardinality(document.Cardinality, $"{path}.cardinality"),
            document.AcceptedExtensions,
            NormalizeLengthRule(acceptance.LengthRule, $"{path}.acceptance.lengthRule"),
            NormalizeInputNormalization(acceptance.Normalization, $"{path}.acceptance.normalization"),
            document.NotApplicableReason));
    }

    private static CompiledInputArtifactClass NormalizeArtifactClass(string value, string path)
    {
        return value switch
        {
            "tp-firmware" => CompiledInputArtifactClass.TpFirmware,
            "dp-firmware" => CompiledInputArtifactClass.DpFirmware,
            "reference-image" => CompiledInputArtifactClass.ReferenceImage,
            CompositionProfileWireTokens.CtrlRamReplacementArtifactClass =>
                CompiledInputArtifactClass.CtrlRamReplacement,
            "auxiliary" => CompiledInputArtifactClass.Auxiliary,
            _ => throw Error(path, "Unknown input artifact class."),
        };
    }

    private static CompiledInputSlotCardinality NormalizeCardinality(string value, string path)
    {
        return value switch
        {
            "exactly-one" => CompiledInputSlotCardinality.ExactlyOne,
            "zero-or-one" => CompiledInputSlotCardinality.ZeroOrOne,
            "one-or-more" => CompiledInputSlotCardinality.OneOrMore,
            _ => throw Error(path, "Unknown input slot cardinality."),
        };
    }

    private static InputLengthRequirementDefinition NormalizeLengthRule(
        CompositionProfileLengthRuleDocument document,
        string path)
    {
        return document.Kind switch
        {
            "exact-bytes" => Wrap(path, () => new CompiledExactBytesInputLengthRequirement(ReadInt64(
                document.Bytes!.Value,
                1,
                long.MaxValue,
                $"{path}.bytes"))),
            "exact-resolved-map-capacity" => new ResolvedMapCapacityInputLengthDefinition(),
            "bounded" => Wrap(path, () => new CompiledBoundedInputLengthRequirement(
                ReadInt64(
                    document.MinimumBytes!.Value,
                    1,
                    long.MaxValue,
                    $"{path}.minimumBytes"),
                ReadInt64(
                    document.MaximumBytes!.Value,
                    1,
                    long.MaxValue,
                    $"{path}.maximumBytes"))),
            "normal-dp-extract-with-warning" => Wrap(path, () =>
                new SourceViewCoverageInputLengthDefinition(
                    NormalizeExpectedInputLengths(document.ExpectedInputLengths, $"{path}.expectedInputLengths"),
                    CanonicalPolicyValueRules.RequireIssueCode(
                        document.IssueCode!,
                        nameof(document.IssueCode)))),
            "tp-maximum-256k" => new CompiledTpMaximum256KInputLengthRequirement(),
            "source-view-coverage" => Wrap(path, () =>
                new SourceViewCoverageInputLengthDefinition(
                    NormalizeExpectedInputLengths(
                        document.ExpectedOuterLengths,
                        $"{path}.expectedOuterLengths"),
                    document.UnexpectedOuterLengthIssueCode is { } issueCode
                        ? CanonicalPolicyValueRules.RequireIssueCode(issueCode, nameof(document.UnexpectedOuterLengthIssueCode))
                        : null)),
            "declared-prefix-with-warning" => Wrap(path, () =>
                new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
                    ReadInt64(
                        document.RequiredEndExclusive!.Value,
                        1,
                        int.MaxValue,
                        $"{path}.requiredEndExclusive"),
                    NormalizeExpectedInputLengths(
                        document.ExpectedOuterLengths,
                        $"{path}.expectedOuterLengths")!,
                    CanonicalPolicyValueRules.RequireIssueCode(
                        document.ShortInputIssueCode!,
                        nameof(document.ShortInputIssueCode)),
                    CanonicalPolicyValueRules.RequireIssueCode(
                        document.UnexpectedOuterLengthIssueCode!,
                        nameof(document.UnexpectedOuterLengthIssueCode)))),
            _ => throw Error($"{path}.kind", "Unknown input length rule."),
        };
    }

    private static long[]? NormalizeExpectedInputLengths(
        IReadOnlyList<JsonElement>? documents,
        string path)
    {
        if (documents is null)
        {
            return null;
        }

        long[] values = new long[documents.Count];
        for (int index = 0; index < documents.Count; index++)
        {
            values[index] = ReadInt64(documents[index], 1, long.MaxValue, $"{path}[{index}]");
        }

        return values;
    }

    private static CompiledInputNormalization NormalizeInputNormalization(
        CompositionProfileInputNormalizationDocument document,
        string path)
    {
        return document.Kind switch
        {
            "none" => new CompiledNoInputNormalization(),
            "pad-shorter" => Wrap(path, () => new CompiledPadShorterInputNormalization(
                ReadByte(
                    document.FillByte!.Value,
                    $"{path}.fillByte"),
                CanonicalPolicyValueRules.RequireCanonicalId(
                    document.EvidenceRef!,
                    nameof(document.EvidenceRef)))),
            "truncate-ctrlram" => Wrap(path, () => new CompiledTruncateCtrlRamInputNormalization(
                CanonicalPolicyValueRules.RequireIssueCode(
                    document.WarningIssueCode!,
                    nameof(document.WarningIssueCode)),
                CanonicalPolicyValueRules.RequireCanonicalId(
                    document.EvidenceRef!,
                    nameof(document.EvidenceRef)))),
            _ => throw Error($"{path}.kind", "Unknown input normalization."),
        };
    }
}
