using NvtFwCombiner.Domain.Memory;

namespace NvtFwCombiner.Domain.Composition;

/// <summary>Profile-approved explicit byte mapping between named address spaces.</summary>
public sealed record ExplicitMapping
{
    /// <summary>Creates a validated explicit mapping with equal source and target lengths.</summary>
    public ExplicitMapping(
        string mappingId,
        int sequence,
        ExplicitMappingOperationKind operationKind,
        string sourceBindingId,
        ByteRange sourceRange,
        string targetSpaceId,
        ByteRange targetRange,
        OverlapPolicy overlapPolicy,
        int alignment,
        string reason,
        string? targetRegionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mappingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceBindingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSpaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        if (alignment <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Alignment must be positive.");
        }

        if (sourceRange.Length != targetRange.Length)
        {
            throw new ArgumentException("Source and target mapping lengths must match.", nameof(targetRange));
        }

        if (targetRange.Start.Value % alignment != 0)
        {
            throw new ArgumentException("Target start must satisfy the mapping alignment.", nameof(targetRange));
        }

        MappingId = mappingId;
        Sequence = sequence;
        OperationKind = operationKind;
        SourceBindingId = sourceBindingId;
        SourceRange = sourceRange;
        TargetSpaceId = targetSpaceId;
        TargetRegionId = string.IsNullOrWhiteSpace(targetRegionId) ? null : targetRegionId;
        TargetRange = targetRange;
        OverlapPolicy = overlapPolicy;
        Alignment = alignment;
        Reason = reason;
    }

    /// <summary>Stable identifier for this mapping.</summary>
    public string MappingId { get; }

    /// <summary>Operation ordering within the compiled composition plan.</summary>
    public int Sequence { get; }

    /// <summary>Operation to perform for this mapping.</summary>
    public ExplicitMappingOperationKind OperationKind { get; }

    /// <summary>Source binding that provides the bytes.</summary>
    public string SourceBindingId { get; }

    /// <summary>Half-open source range read from the source binding.</summary>
    public ByteRange SourceRange { get; }

    /// <summary>Target address space that receives the bytes.</summary>
    public string TargetSpaceId { get; }

    /// <summary>Optional target region that owns the write range.</summary>
    public string? TargetRegionId { get; }

    /// <summary>Half-open target range written by the mapping.</summary>
    public ByteRange TargetRange { get; }

    /// <summary>Overlap behavior permitted for this mapping.</summary>
    public OverlapPolicy OverlapPolicy { get; }

    /// <summary>Required byte alignment for the target start offset.</summary>
    public int Alignment { get; }

    /// <summary>Human-readable evidence for why the explicit mapping is allowed.</summary>
    public string Reason { get; }
}
