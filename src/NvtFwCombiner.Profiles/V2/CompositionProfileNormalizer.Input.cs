using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using System.Text.Json;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    private const long LegacyTpMaximumBytes = 262144;

    internal static CompositionInputSlotDefinition NormalizeInputSlot(
        CompositionProfileInputSlotDocument document,
        string schemaVersion = "2.10",
        string path = "inputSlots[0]")
    {
        ArgumentNullException.ThrowIfNull(document);
        CompositionProfileInputAcceptanceDocument acceptance = document.Acceptance ?? throw Error(
            $"{path}.acceptance",
            "Input acceptance is missing.");
        CompositionProfileLengthRuleDocument lengthRule = acceptance.LengthRule ?? throw Error(
            $"{path}.acceptance.lengthRule",
            "Input length rule is missing.");
        CompositionProfileInputNormalizationDocument normalization = acceptance.Normalization ?? throw Error(
            $"{path}.acceptance.normalization",
            "Input normalization is missing.");

        return Wrap(path, () => new CompositionInputSlotDefinition(
            document.SlotId,
            document.Role,
            NormalizeArtifactClass(document.ArtifactClass, $"{path}.artifactClass"),
            document.Required,
            NormalizeCardinality(document.Cardinality, $"{path}.cardinality"),
            RequireList(document.AcceptedExtensions, $"{path}.acceptedExtensions"),
            NormalizeLengthRule(lengthRule, schemaVersion, $"{path}.acceptance.lengthRule"),
            NormalizeInputNormalization(normalization, $"{path}.acceptance.normalization"),
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
        string schemaVersion,
        string path)
    {
        return document.Kind switch
        {
            "exact-bytes" => Wrap(path, () => new CompiledExactBytesInputLengthRequirement(ReadInt64(
                Require(document.Bytes, $"{path}.bytes"),
                1,
                long.MaxValue,
                $"{path}.bytes"))),
            "exact-resolved-map-capacity" => new ResolvedMapCapacityInputLengthDefinition(),
            "bounded" => Wrap(path, () => new CompiledBoundedInputLengthRequirement(
                ReadInt64(
                    Require(document.MinimumBytes, $"{path}.minimumBytes"),
                    1,
                    long.MaxValue,
                    $"{path}.minimumBytes"),
                ReadInt64(
                    Require(document.MaximumBytes, $"{path}.maximumBytes"),
                    1,
                    long.MaxValue,
                    $"{path}.maximumBytes"))),
            "normal-dp-extract-with-warning" => Wrap(path, () =>
                new SourceViewCoverageInputLengthDefinition(
                    NormalizeExpectedInputLengths(document.ExpectedInputLengths, $"{path}.expectedInputLengths"),
                    InputPolicyValueRules.RequireIssueCode(
                        document.IssueCode ?? throw Error(
                            $"{path}.issueCode",
                            "Warning issue code is missing."),
                        nameof(document.IssueCode)))),
            "tp-maximum-256k" => NormalizeTpMaximum(document, path),
            "source-view-coverage" when schemaVersion is "2.13" or "2.14" or "2.15" => Wrap(path, () =>
                new SourceViewCoverageInputLengthDefinition(
                    NormalizeExpectedInputLengths(
                        document.ExpectedOuterLengths,
                        $"{path}.expectedOuterLengths"),
                    document.UnexpectedOuterLengthIssueCode is { } issueCode
                        ? InputPolicyValueRules.RequireIssueCode(issueCode, nameof(document.UnexpectedOuterLengthIssueCode))
                        : null)),
            "source-view-coverage" => throw Error(
                $"{path}.kind",
                "Source-view coverage requires composition-profile schema version '2.13' or later."),
            "declared-prefix-with-warning" when schemaVersion is "2.10" or "2.11" or "2.12" or "2.13" or "2.14" or "2.15" => Wrap(path, () =>
                new CompiledDeclaredPrefixWithWarningInputLengthRequirement(
                    ReadInt64(
                        Require(document.RequiredEndExclusive, $"{path}.requiredEndExclusive"),
                        1,
                        int.MaxValue,
                        $"{path}.requiredEndExclusive"),
                    NormalizeExpectedInputLengths(
                        document.ExpectedOuterLengths,
                        $"{path}.expectedOuterLengths") ?? throw Error(
                            $"{path}.expectedOuterLengths",
                            "Expected outer lengths are missing."),
                    InputPolicyValueRules.RequireIssueCode(
                        document.ShortInputIssueCode ?? throw Error(
                            $"{path}.shortInputIssueCode",
                            "Short-input issue code is missing."),
                        nameof(document.ShortInputIssueCode)),
                    InputPolicyValueRules.RequireIssueCode(
                        document.UnexpectedOuterLengthIssueCode ?? throw Error(
                            $"{path}.unexpectedOuterLengthIssueCode",
                            "Unexpected outer-length issue code is missing."),
                        nameof(document.UnexpectedOuterLengthIssueCode)))),
            "declared-prefix-with-warning" => throw Error(
                $"{path}.kind",
                "Declared-prefix input authority requires composition-profile schema version '2.10' through '2.15'."),
            _ => throw Error($"{path}.kind", "Unknown input length rule."),
        };
    }

    private static CompiledTpMaximum256KInputLengthRequirement NormalizeTpMaximum(
        CompositionProfileLengthRuleDocument document,
        string path)
    {
        long maximum = ReadInt64(
            Require(document.MaximumBytes, $"{path}.maximumBytes"),
            1,
            long.MaxValue,
            $"{path}.maximumBytes");
        return maximum == LegacyTpMaximumBytes
            ? new CompiledTpMaximum256KInputLengthRequirement()
            : throw Error(
                $"{path}.maximumBytes",
                $"TP maximum must be {LegacyTpMaximumBytes} bytes.");
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
                    Require(document.FillByte, $"{path}.fillByte"),
                    $"{path}.fillByte"),
                InputPolicyValueRules.RequireCanonicalId(
                    document.EvidenceRef ?? throw Error(
                        $"{path}.evidenceRef",
                        "Evidence reference is missing."),
                    nameof(document.EvidenceRef)))),
            "truncate-ctrlram" => Wrap(path, () => new CompiledTruncateCtrlRamInputNormalization(
                InputPolicyValueRules.RequireIssueCode(
                    document.WarningIssueCode ?? throw Error(
                        $"{path}.warningIssueCode",
                        "Warning issue code is missing."),
                    nameof(document.WarningIssueCode)),
                InputPolicyValueRules.RequireCanonicalId(
                    document.EvidenceRef ?? throw Error(
                        $"{path}.evidenceRef",
                        "Evidence reference is missing."),
                    nameof(document.EvidenceRef)))),
            _ => throw Error($"{path}.kind", "Unknown input normalization."),
        };
    }
}
