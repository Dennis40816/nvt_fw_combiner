using NvtFwCombiner.Contracts.Profiles;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    internal static CompositionProfileInputSlot NormalizeInputSlot(
        CompositionProfileInputSlotDocument document,
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

        return Wrap(path, () => new CompositionProfileInputSlot(
            document.SlotId,
            document.Role,
            NormalizeArtifactClass(document.ArtifactClass, $"{path}.artifactClass"),
            document.Required,
            NormalizeCardinality(document.Cardinality, $"{path}.cardinality"),
            RequireList(document.AcceptedExtensions, $"{path}.acceptedExtensions"),
            NormalizeLengthRule(lengthRule, $"{path}.acceptance.lengthRule"),
            NormalizeInputNormalization(normalization, $"{path}.acceptance.normalization")));
    }

    private static CompositionProfileArtifactClass NormalizeArtifactClass(string value, string path)
    {
        return value switch
        {
            "tp-firmware" => CompositionProfileArtifactClass.TpFirmware,
            "dp-firmware" => CompositionProfileArtifactClass.DpFirmware,
            "reference-image" => CompositionProfileArtifactClass.ReferenceImage,
            CompositionProfileWireTokens.CtrlRamReplacementArtifactClass =>
                CompositionProfileArtifactClass.CtrlRamReplacement,
            "auxiliary" => CompositionProfileArtifactClass.Auxiliary,
            _ => throw Error(path, "Unknown input artifact class."),
        };
    }

    private static CompositionProfileSlotCardinality NormalizeCardinality(string value, string path)
    {
        return value switch
        {
            "exactly-one" => CompositionProfileSlotCardinality.ExactlyOne,
            "zero-or-one" => CompositionProfileSlotCardinality.ZeroOrOne,
            "one-or-more" => CompositionProfileSlotCardinality.OneOrMore,
            _ => throw Error(path, "Unknown input slot cardinality."),
        };
    }

    private static CompositionProfileLengthRule NormalizeLengthRule(
        CompositionProfileLengthRuleDocument document,
        string path)
    {
        return document.Kind switch
        {
            "exact-bytes" => Wrap(path, () => new ExactBytesLengthRule(ReadInt64(
                Require(document.Bytes, $"{path}.bytes"),
                1,
                long.MaxValue,
                $"{path}.bytes"))),
            "exact-resolved-map-capacity" => new ExactResolvedMapCapacityLengthRule(),
            "bounded" => Wrap(path, () => new BoundedLengthRule(
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
                new NormalDpExtractWithWarningLengthRule(document.IssueCode ?? throw Error(
                    $"{path}.issueCode",
                    "Warning issue code is missing."))),
            "tp-maximum-256k" => NormalizeTpMaximum(document, path),
            _ => throw Error($"{path}.kind", "Unknown input length rule."),
        };
    }

    private static TpMaximum256KLengthRule NormalizeTpMaximum(
        CompositionProfileLengthRuleDocument document,
        string path)
    {
        long maximum = ReadInt64(
            Require(document.MaximumBytes, $"{path}.maximumBytes"),
            1,
            long.MaxValue,
            $"{path}.maximumBytes");
        return maximum == TpMaximum256KLengthRule.MaximumBytes
            ? new TpMaximum256KLengthRule()
            : throw Error(
                $"{path}.maximumBytes",
                $"TP maximum must be {TpMaximum256KLengthRule.MaximumBytes} bytes.");
    }

    private static CompositionProfileInputNormalization NormalizeInputNormalization(
        CompositionProfileInputNormalizationDocument document,
        string path)
    {
        return document.Kind switch
        {
            "none" => new NoInputNormalization(),
            "pad-shorter" => Wrap(path, () => new PadShorterInputNormalization(
                ReadByte(
                    Require(document.FillByte, $"{path}.fillByte"),
                    $"{path}.fillByte"),
                document.EvidenceRef ?? throw Error($"{path}.evidenceRef", "Evidence reference is missing."))),
            "truncate-ctrlram" => Wrap(path, () => new TruncateCtrlRamInputNormalization(
                document.WarningIssueCode ?? throw Error(
                    $"{path}.warningIssueCode",
                    "Warning issue code is missing."),
                document.EvidenceRef ?? throw Error($"{path}.evidenceRef", "Evidence reference is missing."))),
            _ => throw Error($"{path}.kind", "Unknown input normalization."),
        };
    }
}
