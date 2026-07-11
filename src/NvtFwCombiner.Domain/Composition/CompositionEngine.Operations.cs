using System.Security.Cryptography;

namespace NvtFwCombiner.Domain.Composition;

public static partial class CompositionEngine
{
    private static void ApplyHostOperation(
        CompositionOperation operation,
        Dictionary<string, byte[]> input,
        Dictionary<string, byte[]> mutableBuffers)
    {
        byte[] targetBuffer = mutableBuffers[operation.TargetSpaceId];
        Span<byte> targetSpan = targetBuffer.AsSpan((int)operation.TargetRange.Start, (int)operation.TargetRange.Length);

        if (operation.Kind is CompositionOperationKind.CopyRange or CompositionOperationKind.ReplaceRange)
        {
            ReadOperationSource(operation, input, mutableBuffers).CopyTo(targetSpan);
            return;
        }

        if (operation.Kind == CompositionOperationKind.FillRange)
        {
            targetSpan.Fill(operation.FillByte!.Value);
            return;
        }

        if (operation.Kind == CompositionOperationKind.PatchScalar)
        {
            operation.PatchBytes.Span.CopyTo(targetSpan);
            return;
        }

        throw new InvalidOperationException($"Unsupported operation kind '{operation.Kind}'.");
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
