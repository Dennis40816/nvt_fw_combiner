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
        ScalarTransform? scalarTransform,
        ExternalProcessorInvocation? externalProcessorInvocation,
        string reason,
        OperationProvenance? provenance)
    {
        OperationId = RequiredValue.NotBlank(operationId);
        TargetSpaceId = RequiredValue.NotBlank(targetSpaceId);
        Reason = RequiredValue.NotBlank(reason);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);

        Sequence = sequence;
        Kind = kind;
        SourceSpaceId = sourceSpaceId;
        SourceRange = sourceRange;
        TargetRange = targetRange;
        OverlapPolicy = overlapPolicy;
        FillByte = fillByte;
        _patchBytes = [.. patchBytes];
        ScalarTransform = scalarTransform;
        ExternalProcessorInvocation = externalProcessorInvocation;
        Provenance = provenance ?? OperationProvenance.BuiltInProfile;
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

    /// <summary>Checked scalar transform declaration for transform-scalar operations.</summary>
    public ScalarTransform? ScalarTransform { get; }

    /// <summary>External processor declaration for run-external-processor operations.</summary>
    public ExternalProcessorInvocation? ExternalProcessorInvocation { get; }

    /// <summary>Human-readable reason recorded in mutation traces.</summary>
    public string Reason { get; }

    /// <summary>Traceable origin for this operation.</summary>
    public OperationProvenance Provenance { get; }

    /// <summary>
    /// Exact half-open target ranges this operation is authorized to write.
    /// External processors expose their constrained allowed ranges; every
    /// other primitive exposes its single target range.
    /// </summary>
    public IReadOnlyList<ByteRange> DeclaredWriteRanges =>
        Kind == CompositionOperationKind.RunExternalProcessor
            ? ExternalProcessorInvocation!.AllowedWriteRanges
            : [TargetRange];

    internal bool DeclaredWritesOverlap(CompositionOperation other)
    {
        return StringComparer.Ordinal.Equals(TargetSpaceId, other.TargetSpaceId) &&
            DeclaredWriteRanges.Any(first =>
                other.DeclaredWriteRanges.Any(first.Overlaps));
    }

    internal string? GetProfileOverlapError(IReadOnlyList<CompositionOperation> priorWrites)
    {
        ArgumentNullException.ThrowIfNull(priorWrites);
        CompositionOperation[] overlaps = [.. priorWrites.Where(DeclaredWritesOverlap)];
        if (overlaps.Length == 0)
        {
            return OverlapPolicy == OverlapPolicy.ReplaceExisting
                ? $"Operation '{OperationId}' declares ReplaceExisting but has no earlier write covering its target range in target space '{TargetSpaceId}'."
                : null;
        }

        if (OverlapPolicy != OverlapPolicy.ReplaceExisting)
        {
            return $"Operation '{OperationId}' overlaps earlier operation '{overlaps[0].OperationId}' in target space '{TargetSpaceId}'.";
        }

        bool fullyCovered = Kind is CompositionOperationKind.CopyRange or CompositionOperationKind.RunExternalProcessor &&
            DeclaredWriteRanges.All(writeRange => overlaps.Any(candidate =>
                candidate.DeclaredWriteRanges.Any(candidateRange => candidateRange.Contains(writeRange))));
        return fullyCovered
            ? null
            : $"Operation '{OperationId}' declares ReplaceExisting but no earlier write fully covers its target range in target space '{TargetSpaceId}'.";
    }

    /// <summary>Creates a copy-range operation.</summary>
    public static CompositionOperation CopyRange(
        string operationId,
        int sequence,
        string sourceSpaceId,
        ByteRange sourceRange,
        string targetSpaceId,
        ByteRange targetRange,
        OverlapPolicy overlapPolicy,
        string reason,
        OperationProvenance? provenance = null)
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
            null,
            reason,
            provenance);
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
        string reason,
        OperationProvenance? provenance = null)
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
            null,
            reason,
            provenance);
    }

    /// <summary>Creates a fill-range operation.</summary>
    public static CompositionOperation FillRange(
        string operationId,
        int sequence,
        string targetSpaceId,
        ByteRange targetRange,
        byte fillByte,
        OverlapPolicy overlapPolicy,
        string reason,
        OperationProvenance? provenance = null)
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
            null,
            reason,
            provenance);
    }

    /// <summary>Creates a patch-scalar operation from exact bytes supplied by the profile.</summary>
    public static CompositionOperation PatchScalar(
        string operationId,
        int sequence,
        string targetSpaceId,
        ByteRange targetRange,
        IReadOnlyList<byte> patchBytes,
        OverlapPolicy overlapPolicy,
        string reason,
        OperationProvenance? provenance = null)
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
            null,
            reason,
            provenance);
    }

    /// <summary>Creates one generic checked scalar transform operation.</summary>
    public static CompositionOperation TransformScalar(
        string operationId,
        int sequence,
        string sourceSpaceId,
        ByteRange sourceRange,
        string targetSpaceId,
        ByteRange targetRange,
        ScalarTransform scalarTransform,
        OverlapPolicy overlapPolicy,
        string reason,
        OperationProvenance? provenance = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSpaceId);
        ArgumentNullException.ThrowIfNull(scalarTransform);
        EnsureEqualLength(sourceRange, targetRange, nameof(targetRange));
        return sourceRange.Length != scalarTransform.WidthBytes
            ? throw new ArgumentException(
                "Scalar transform source and target ranges must match the declared scalar width.",
                nameof(sourceRange))
            : new CompositionOperation(
            operationId,
            sequence,
            CompositionOperationKind.TransformScalar,
            sourceSpaceId,
            sourceRange,
            targetSpaceId,
            targetRange,
            overlapPolicy,
            null,
            [],
            scalarTransform,
            null,
            reason,
            provenance);
    }

    /// <summary>Creates a run-external-processor operation over a staged target image range.</summary>
    public static CompositionOperation RunExternalProcessor(
        string operationId,
        int sequence,
        string targetSpaceId,
        ByteRange targetRange,
        ExternalProcessorInvocation invocation,
        OverlapPolicy overlapPolicy,
        string reason,
        OperationProvenance? provenance = null)
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
            null,
            invocation,
            reason,
            provenance);
    }

    private static void EnsureEqualLength(ByteRange sourceRange, ByteRange targetRange, string parameterName)
    {
        _ = sourceRange.Length == targetRange.Length
            ? true
            : throw new ArgumentException("Source and target ranges must have the same length.", parameterName);
    }
}
