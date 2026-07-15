using System.Numerics;
using System.Security.Cryptography;

namespace NvtFwCombiner.Domain.Composition;

public static partial class CompositionEngine
{
    private static CompositionIssue? ApplyHostOperation(
        CompositionOperation operation,
        Dictionary<string, byte[]> input,
        Dictionary<string, byte[]> mutableBuffers)
    {
        byte[] targetBuffer = mutableBuffers[operation.TargetSpaceId];
        Span<byte> targetSpan = targetBuffer.AsSpan((int)operation.TargetRange.Start, (int)operation.TargetRange.Length);

        if (operation.Kind is CompositionOperationKind.CopyRange or CompositionOperationKind.ReplaceRange)
        {
            ReadOperationSource(operation, input, mutableBuffers).CopyTo(targetSpan);
            return null;
        }

        if (operation.Kind == CompositionOperationKind.FillRange)
        {
            targetSpan.Fill(operation.FillByte!.Value);
            return null;
        }

        if (operation.Kind == CompositionOperationKind.PatchScalar)
        {
            operation.PatchBytes.Span.CopyTo(targetSpan);
            return null;
        }

        return operation.Kind == CompositionOperationKind.TransformScalar
            ? ApplyScalarTransform(operation, targetSpan, input, mutableBuffers)
            : throw new InvalidOperationException($"Unsupported operation kind '{operation.Kind}'.");
    }

    private static CompositionIssue? ApplyScalarTransform(
        CompositionOperation operation,
        Span<byte> target,
        Dictionary<string, byte[]> input,
        Dictionary<string, byte[]> mutableBuffers)
    {
        ScalarTransform transform = operation.ScalarTransform ?? throw new InvalidOperationException(
            $"Transform operation '{operation.OperationId}' is missing scalar transform metadata.");
        ulong before = ReadUnsignedScalar(
            ReadOperationSource(operation, input, mutableBuffers),
            transform.ByteOrder);
        if (transform.ExpectedBefore is { } expectedBefore && before != expectedBefore)
        {
            return new CompositionIssue(
                CompositionIssueCodes.ExecutionScalarTransformExpectedValueMismatch,
                $"Operation '{operation.OperationId}' expected scalar value 0x{expectedBefore:X} but read 0x{before:X}.",
                operation.OperationId);
        }

        BigInteger after = before + transform.Addend;
        if (after < BigInteger.Zero || after > transform.MaximumValue)
        {
            return transform.OverflowPolicy switch
            {
                ScalarTransformOverflowPolicy.Reject => new CompositionIssue(
                    CompositionIssueCodes.ExecutionScalarTransformOverflow,
                    $"Operation '{operation.OperationId}' scalar transform overflows its {transform.WidthBytes}-byte unsigned range.",
                    operation.OperationId),
                _ => throw new InvalidOperationException(
                    $"Unsupported scalar transform overflow policy '{transform.OverflowPolicy}'."),
            };
        }

        WriteUnsignedScalar(target, checked((ulong)after), transform.ByteOrder);
        return null;
    }

    private static ulong ReadUnsignedScalar(
        ReadOnlySpan<byte> source,
        ScalarTransformByteOrder byteOrder)
    {
        ulong value = 0;
        if (byteOrder == ScalarTransformByteOrder.LittleEndian)
        {
            for (int index = 0; index < source.Length; index++)
            {
                value |= (ulong)source[index] << (index * 8);
            }

            return value;
        }

        for (int index = 0; index < source.Length; index++)
        {
            value = (value << 8) | source[index];
        }

        return value;
    }

    private static void WriteUnsignedScalar(
        Span<byte> target,
        ulong value,
        ScalarTransformByteOrder byteOrder)
    {
        if (byteOrder == ScalarTransformByteOrder.LittleEndian)
        {
            for (int index = 0; index < target.Length; index++)
            {
                target[index] = (byte)(value >> (index * 8));
            }

            return;
        }

        for (int index = target.Length - 1; index >= 0; index--)
        {
            target[index] = (byte)value;
            value >>= 8;
        }
    }

    private static ReadOnlySpan<byte> ReadOperationSource(
        CompositionOperation operation,
        Dictionary<string, byte[]> input,
        Dictionary<string, byte[]> mutableBuffers)
    {
        ByteRange sourceRange = operation.SourceRange!.Value;
        if (mutableBuffers.TryGetValue(operation.SourceSpaceId!, out byte[]? mutableSource))
        {
            return mutableSource.AsSpan((int)sourceRange.Start, (int)sourceRange.Length);
        }

        byte[] sourceBytes = input[operation.SourceSpaceId!];
        return sourceBytes.AsSpan((int)sourceRange.Start, (int)sourceRange.Length);
    }

    private static MutationRecord CreateMutationRecord(CompositionOperation operation, byte[] before, byte[] after)
    {
        IReadOnlyList<ByteRange> changedRanges = [
            .. ByteDiff.FindChangedRanges(before, after)
                .Select(range => new ByteRange(checked(operation.TargetRange.Start + range.Start), range.Length)),
        ];

        return new MutationRecord(
            operation.OperationId,
            operation.Kind,
            operation.TargetSpaceId,
            operation.TargetRange,
            changedRanges,
            FormatSha256(before),
            FormatSha256(after),
            operation.Reason);
    }

    private static string FormatSha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static byte[] ReadSlice(byte[] buffer, ByteRange range)
    {
        return [.. buffer.AsSpan((int)range.Start, (int)range.Length)];
    }

    private static CompositionExecutionResult PrependIssues(
        CompositionExecutionResult result,
        IReadOnlyList<CompositionIssue> issues)
    {
        return issues.Count == 0
            ? result
            : CompositionExecutionResult.Failed([.. issues, .. result.Issues]);
    }
}
