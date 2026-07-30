using System.Numerics;
using System.Text.Json;
using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    internal static CompositionProfileOperation NormalizeOperation(
        CompositionProfileOperationDocument document,
        string path = "operations[0]",
        string schemaVersion = "2.0")
    {
        ArgumentNullException.ThrowIfNull(document);
        BigInteger sequence = ReadInteger(document.Sequence, $"{path}.sequence");
        if (sequence.Sign < 0)
        {
            throw Error($"{path}.sequence", "Operation sequence cannot be negative.");
        }

        OverlapPolicy overlapPolicy = NormalizeOverlapPolicy(
            document.OverlapPolicy,
            $"{path}.overlapPolicy");
        return document.Kind switch
        {
            "copy-range" => NormalizeCopyOrReplace(
                document,
                sequence,
                overlapPolicy,
                CompositionProfileOperationKind.CopyRange,
                path),
            "replace-range" => NormalizeCopyOrReplace(
                document,
                sequence,
                overlapPolicy,
                CompositionProfileOperationKind.ReplaceRange,
                path),
            "fill-range" => Wrap(path, () => new FillRangeProfileOperation(
                document.OperationId,
                sequence,
                overlapPolicy,
                document.Reason,
                RequireText(document.TargetViewId, $"{path}.targetViewId", "Target view is missing."),
                ReadByte(Require(document.FillByte, $"{path}.fillByte"), $"{path}.fillByte"))),
            "patch-scalar" => Wrap(path, () => new PatchScalarProfileOperation(
                document.OperationId,
                sequence,
                overlapPolicy,
                document.Reason,
                RequireText(document.TargetViewId, $"{path}.targetViewId", "Target view is missing."),
                ReadBytes(
                    RequireText(document.ValueHex, $"{path}.valueHex", "Patch value is missing."),
                    $"{path}.valueHex"))),
            "transform-scalar" => NormalizeTransform(
                document,
                sequence,
                overlapPolicy,
                schemaVersion,
                path),
            "run-processor" => Wrap(path, () => new RunProcessorProfileOperation(
                document.OperationId,
                sequence,
                overlapPolicy,
                document.Reason,
                RequireText(
                    document.ProcessorStageId,
                    $"{path}.processorStageId",
                    "Processor stage is missing."))),
            _ => throw Error($"{path}.kind", "Unknown profile operation kind."),
        };
    }

    private static CopyOrReplaceProfileOperation NormalizeCopyOrReplace(
        CompositionProfileOperationDocument document,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        CompositionProfileOperationKind kind,
        string path)
    {
        return Wrap(path, () => new CopyOrReplaceProfileOperation(
            document.OperationId,
            sequence,
            overlapPolicy,
            document.Reason,
            kind,
            RequireText(document.SourceViewId, $"{path}.sourceViewId", "Source view is missing."),
            RequireText(document.TargetViewId, $"{path}.targetViewId", "Target view is missing.")));
    }

    private static TransformScalarProfileOperation NormalizeTransform(
        CompositionProfileOperationDocument document,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        string schemaVersion,
        string path)
    {
        RequireConstant(
            document.ValueInterpretation,
            "unsigned",
            $"{path}.valueInterpretation",
            "Scalar value interpretation must be unsigned.");
        RequireConstant(
            document.OverflowPolicy,
            "reject",
            $"{path}.overflowPolicy",
            "Scalar overflow policy must reject overflow.");
        ulong? expectedBefore = document.ExpectedBefore is { } expected
            ? ReadUInt64(expected, $"{path}.expectedBefore")
            : null;
        return Wrap(path, () => new TransformScalarProfileOperation(
            document.OperationId,
            sequence,
            overlapPolicy,
            document.Reason,
            RequireText(document.SourceViewId, $"{path}.sourceViewId", "Source view is missing."),
            RequireText(document.TargetViewId, $"{path}.targetViewId", "Target view is missing."),
            NormalizeScalarWidth(
                Require(document.WidthBytes, $"{path}.widthBytes"),
                $"{path}.widthBytes"),
            NormalizeScalarByteOrder(
                RequireText(document.ByteOrder, $"{path}.byteOrder", "Scalar byte order is missing."),
                $"{path}.byteOrder"),
            NormalizeTransformAddend(
                Require(document.Addend, $"{path}.addend"),
                schemaVersion,
                $"{path}.addend"),
            expectedBefore));
    }

    private static TransformAddendSource NormalizeTransformAddend(
        JsonElement document,
        string schemaVersion,
        string path)
    {
        if (document.ValueKind == JsonValueKind.Number)
        {
            return new FixedTransformAddendSource(ReadInteger(document, path));
        }

        if (document.ValueKind != JsonValueKind.Object)
        {
            throw Error(path, "Scalar addend must be an integer or a declared addend object.");
        }

        if (schemaVersion != "2.14")
        {
            throw Error(
                path,
                "Region-instance delta addends require composition-profile schema version '2.14'.");
        }

        CompositionProfileRegionInstanceDeltaAddendDocument addend;
        try
        {
            addend = JsonSerializer.Deserialize(
                document,
                ProfileBundleSemanticJsonContext.Default
                    .CompositionProfileRegionInstanceDeltaAddendDocument) ?? throw Error(
                        path,
                        "Region-instance delta addend is missing.");
        }
        catch (JsonException exception)
        {
            throw Error(path, exception.Message);
        }

        RequireConstant(
            addend.Kind,
            "region-instance-delta",
            $"{path}.kind",
            "Unknown scalar addend object kind.");
        return Wrap(path, () => new RegionInstanceDeltaTransformAddendSource(
            RequireText(
                addend.SourceRegionInstanceId,
                $"{path}.sourceRegionInstanceId",
                "Source region instance is missing."),
            RequireText(
                addend.TargetRegionInstanceId,
                $"{path}.targetRegionInstanceId",
                "Target region instance is missing.")));
    }

    private static OverlapPolicy NormalizeOverlapPolicy(string value, string path)
    {
        return value switch
        {
            "reject" => OverlapPolicy.Reject,
            "allow-declared" => OverlapPolicy.AllowDeclared,
            "replace-existing" => OverlapPolicy.ReplaceExisting,
            _ => throw Error(path, "Unknown overlap policy."),
        };
    }

    private static CompositionProfileScalarWidth NormalizeScalarWidth(JsonElement value, string path)
    {
        return ReadInt64(value, 1, 8, path) switch
        {
            1 => CompositionProfileScalarWidth.OneByte,
            2 => CompositionProfileScalarWidth.TwoBytes,
            4 => CompositionProfileScalarWidth.FourBytes,
            8 => CompositionProfileScalarWidth.EightBytes,
            _ => throw Error(path, "Scalar width must be 1, 2, 4, or 8 bytes."),
        };
    }

    private static CompositionProfileScalarByteOrder NormalizeScalarByteOrder(string value, string path)
    {
        return value switch
        {
            "little" => CompositionProfileScalarByteOrder.LittleEndian,
            "big" => CompositionProfileScalarByteOrder.BigEndian,
            _ => throw Error(path, "Unknown scalar byte order."),
        };
    }

    private static string RequireText(string? value, string path, string message)
    {
        return value ?? throw Error(path, message);
    }

    private static void RequireConstant(
        string? value,
        string expected,
        string path,
        string message)
    {
        if (!StringComparer.Ordinal.Equals(value, expected))
        {
            throw Error(path, message);
        }
    }
}
