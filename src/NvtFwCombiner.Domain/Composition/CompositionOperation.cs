namespace NvtFwCombiner.Domain.Composition;

/// <summary>One deterministic byte operation in a composition plan.</summary>
public sealed class CompositionOperation
{
    private readonly byte[] _patchBytes;

    private CompositionOperation(
        string operationId,
        int sequence,
        CompositionOperationKind kind,
        string? sourceSpaceId,
        ByteRange? sourceRange,
        string targetSpaceId,
        ByteRange targetRange,
        OverlapPolicy overlapPolicy,
        byte? fillByte,
        byte[] patchBytes,
        ExternalProcessorInvocation? externalProcessorInvocation,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSpaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);

        OperationId = operationId;
        Sequence = sequence;
        Kind = kind;
        SourceSpaceId = sourceSpaceId;
        SourceRange = sourceRange;
        TargetSpaceId = targetSpaceId;
        TargetRange = targetRange;
        OverlapPolicy = overlapPolicy;
        FillByte = fillByte;
        _patchBytes = [.. patchBytes];
        ExternalProcessorInvocation = externalProcessorInvocation;
        Reason = reason;
    }

    /// <summary>Stable operation id used in traces and issues.</summary>
    public string OperationId { get; }

    /// <summary>Operation order declared by the compiled profile.</summary>
    public int Sequence { get; }

    /// <summary>Operation primitive kind.</summary>
    public CompositionOperationKind Kind { get; }

    /// <summary>Source address space for copy-like operations.</summary>
    public string? SourceSpaceId { get; }

    /// <summary>Source range for copy-like operations.</summary>
    public ByteRange? SourceRange { get; }

    /// <summary>Mutable target address space.</summary>
    public string TargetSpaceId { get; }

    /// <summary>Target range written by this operation.</summary>
    public ByteRange TargetRange { get; }

    /// <summary>Declared policy for overlaps with earlier writes.</summary>
    public OverlapPolicy OverlapPolicy { get; }

    /// <summary>Fill byte for fill operations.</summary>
    public byte? FillByte { get; }

    /// <summary>Patch bytes for patch-scalar operations.</summary>
    public ReadOnlyMemory<byte> PatchBytes => _patchBytes;

    /// <summary>External processor declaration for run-external-processor operations.</summary>
    public ExternalProcessorInvocation? ExternalProcessorInvocation { get; }

    /// <summary>Human-readable reason recorded in mutation traces.</summary>
    public string Reason { get; }

    /// <summary>Creates a copy-range operation.</summary>
    public static CompositionOperation CopyRange(
        string operationId,
        int sequence,
        string sourceSpaceId,
        ByteRange sourceRange,
        string targetSpaceId,
        ByteRange targetRange,
        OverlapPolicy overlapPolicy,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSpaceId);
        EnsureEqualLength(sourceRange, targetRange, nameof(targetRange));
        return new CompositionOperation(
            operationId,
            sequence,
            CompositionOperationKind.CopyRange,
            sourceSpaceId,
            sourceRange,
            targetSpaceId,
            targetRange,
            overlapPolicy,
            null,
            [],
            null,
            reason);
    }

    /// <summary>Creates a replace-range operation.</summary>
    public static CompositionOperation ReplaceRange(
        string operationId,
        int sequence,
        string sourceSpaceId,
        ByteRange sourceRange,
        string targetSpaceId,
        ByteRange targetRange,
        OverlapPolicy overlapPolicy,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSpaceId);
        EnsureEqualLength(sourceRange, targetRange, nameof(targetRange));
        return new CompositionOperation(
            operationId,
            sequence,
            CompositionOperationKind.ReplaceRange,
            sourceSpaceId,
            sourceRange,
            targetSpaceId,
            targetRange,
            overlapPolicy,
            null,
            [],
            null,
            reason);
    }

    /// <summary>Creates a fill-range operation.</summary>
    public static CompositionOperation FillRange(
        string operationId,
        int sequence,
        string targetSpaceId,
        ByteRange targetRange,
        byte fillByte,
        OverlapPolicy overlapPolicy,
        string reason)
    {
        return new CompositionOperation(
            operationId,
            sequence,
            CompositionOperationKind.FillRange,
            null,
            null,
            targetSpaceId,
            targetRange,
            overlapPolicy,
            fillByte,
            [],
            null,
            reason);
    }

    /// <summary>Creates a patch-scalar operation from exact bytes supplied by the profile.</summary>
    public static CompositionOperation PatchScalar(
        string operationId,
        int sequence,
        string targetSpaceId,
        ByteRange targetRange,
        IReadOnlyList<byte> patchBytes,
        OverlapPolicy overlapPolicy,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(patchBytes);
        _ = patchBytes.Count == targetRange.Length
            ? true
            : throw new ArgumentException("Patch byte count must match target range length.", nameof(patchBytes));

        return new CompositionOperation(
            operationId,
            sequence,
            CompositionOperationKind.PatchScalar,
            null,
            null,
            targetSpaceId,
            targetRange,
            overlapPolicy,
            null,
            [.. patchBytes],
            null,
            reason);
    }

    /// <summary>Creates a run-external-processor operation over a staged target image range.</summary>
    public static CompositionOperation RunExternalProcessor(
        string operationId,
        int sequence,
        string targetSpaceId,
        ByteRange targetRange,
        ExternalProcessorInvocation invocation,
        OverlapPolicy overlapPolicy,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return new CompositionOperation(
            operationId,
            sequence,
            CompositionOperationKind.RunExternalProcessor,
            null,
            null,
            targetSpaceId,
            targetRange,
            overlapPolicy,
            null,
            [],
            invocation,
            reason);
    }

    private static void EnsureEqualLength(ByteRange sourceRange, ByteRange targetRange, string parameterName)
    {
        _ = sourceRange.Length == targetRange.Length
            ? true
            : throw new ArgumentException("Source and target ranges must have the same length.", parameterName);
    }
}
