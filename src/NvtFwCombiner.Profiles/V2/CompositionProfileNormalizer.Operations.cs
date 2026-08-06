using System.Numerics;
using System.Text.Json;
using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    internal static CompositionOperationDefinition NormalizeOperation(
        CompositionProfileOperationDocument document,
        string path = "operations[0]")
    {
        ArgumentNullException.ThrowIfNull(document);
        BigInteger sequence = ReadInteger(document.Sequence, $"{path}.sequence");
        OverlapPolicy overlapPolicy = NormalizeOverlapPolicy(
            document.OverlapPolicy,
            $"{path}.overlapPolicy");
        return document.Kind switch
        {
            "copy-range" => NormalizeCopyOrReplace(
                document, sequence, overlapPolicy, CompositionOperationKind.CopyRange, path),
            "replace-range" => NormalizeCopyOrReplace(
                document, sequence, overlapPolicy, CompositionOperationKind.ReplaceRange, path),
            "fill-range" => Wrap(path, () => CompositionOperationDefinition.FillRange(
                document.OperationId,
                sequence,
                overlapPolicy,
                document.Reason,
                document.TargetViewId!,
                ReadByte(document.FillByte!.Value, $"{path}.fillByte"))),
            "patch-scalar" => Wrap(path, () => CompositionOperationDefinition.PatchScalar(
                document.OperationId,
                sequence,
                overlapPolicy,
                document.Reason,
                document.TargetViewId!,
                ReadBytes(document.ValueHex!, $"{path}.valueHex"))),
            "transform-scalar" => NormalizeTransform(
                document,
                sequence,
                overlapPolicy,
                path),
            "run-processor" => Wrap(path, () => CompositionOperationDefinition.RunProcessor(
                document.OperationId,
                sequence,
                overlapPolicy,
                document.Reason,
                document.ProcessorStageId!)),
            _ => throw Error($"{path}.kind", "Unknown profile operation kind."),
        };
    }

    private static CompositionOperationDefinition NormalizeCopyOrReplace(
        CompositionProfileOperationDocument document,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        CompositionOperationKind kind,
        string path)
    {
        return Wrap(path, () => CompositionOperationDefinition.CopyOrReplace(
            document.OperationId,
            sequence,
            overlapPolicy,
            document.Reason,
            kind,
            document.SourceViewId!,
            document.TargetViewId!));
    }

    private static CompositionOperationDefinition NormalizeTransform(
        CompositionProfileOperationDocument document,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        string path)
    {
        ulong? expectedBefore = document.ExpectedBefore is { } expected
            ? ReadUInt64(expected, $"{path}.expectedBefore")
            : null;
        (ScalarTransformAddendSource addendSource, BigInteger? fixedAddend) = NormalizeTransformAddend(
            document.Addend!.Value,
            $"{path}.addend");
        return Wrap(path, () => CompositionOperationDefinition.TransformScalar(
            document.OperationId,
            sequence,
            overlapPolicy,
            document.Reason,
            document.SourceViewId!,
            document.TargetViewId!,
            NormalizeScalarWidth(
                document.WidthBytes!.Value,
                $"{path}.widthBytes"),
            NormalizeScalarByteOrder(
                document.ByteOrder!,
                $"{path}.byteOrder"),
            fixedAddend,
            addendSource,
            expectedBefore));
    }

    private static (ScalarTransformAddendSource Source, BigInteger? FixedAddend) NormalizeTransformAddend(
        JsonElement document,
        string path)
    {
        if (document.ValueKind == JsonValueKind.Number)
        {
            return (ScalarTransformAddendSource.Fixed, ReadInteger(document, path));
        }

        if (document.ValueKind != JsonValueKind.Object)
        {
            throw Error(path, "Scalar addend must be an integer or a declared addend object.");
        }

        CompositionProfileRegionInstanceDeltaAddendDocument addend;
        try
        {
            addend = JsonSerializer.Deserialize(
                document,
                ProfileBundleSemanticJsonContext.Default
                    .CompositionProfileRegionInstanceDeltaAddendDocument)!;
        }
        catch (JsonException exception)
        {
            throw Error(path, exception.Message);
        }

        return (
            Wrap(path, () => ScalarTransformAddendSource.RegionInstanceDelta(
                addend.SourceRegionInstanceId,
                addend.TargetRegionInstanceId)),
            null);
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

    private static ScalarTransformWidth NormalizeScalarWidth(JsonElement value, string path)
    {
        return ReadInt64(value, 1, 8, path) switch
        {
            1 => ScalarTransformWidth.OneByte,
            2 => ScalarTransformWidth.TwoBytes,
            4 => ScalarTransformWidth.FourBytes,
            8 => ScalarTransformWidth.EightBytes,
            _ => throw Error(path, "Scalar width must be 1, 2, 4, or 8 bytes."),
        };
    }

    private static ScalarTransformByteOrder NormalizeScalarByteOrder(string value, string path)
    {
        return value switch
        {
            "little" => ScalarTransformByteOrder.LittleEndian,
            "big" => ScalarTransformByteOrder.BigEndian,
            _ => throw Error(path, "Unknown scalar byte order."),
        };
    }

}
