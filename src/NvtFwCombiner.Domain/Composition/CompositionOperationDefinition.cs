using System.Numerics;

namespace NvtFwCombiner.Domain.Composition;

/// <summary>One immutable canonical operation definition before logical views resolve to ranges.</summary>
internal sealed class CompositionOperationDefinition
{
    private CompositionOperationDefinition(
        string operationId,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        string reason,
        CompositionOperationKind kind)
    {
        OperationId = CanonicalPolicyValueRules.RequireCanonicalId(operationId, nameof(operationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Sequence = sequence.Sign >= 0
            ? sequence
            : throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Operation sequence cannot be negative.");
        OverlapPolicy = ClosedEnum.IsDefined(overlapPolicy)
            ? overlapPolicy
            : throw new ArgumentOutOfRangeException(nameof(overlapPolicy), overlapPolicy, "Unknown overlap policy.");
        Reason = reason;
        Kind = kind;
    }

    internal string OperationId { get; }

    internal BigInteger Sequence { get; }

    internal OverlapPolicy OverlapPolicy { get; }

    internal string Reason { get; }

    internal CompositionOperationKind Kind { get; }

    internal string SourceViewId { get; private init; } = null!;

    internal string TargetViewId { get; private init; } = null!;

    internal byte FillByte { get; private init; }

    internal CompiledValidationBytes PatchBytes { get; private init; } = null!;

    internal ScalarTransformWidth TransformWidth { get; private init; }

    internal ScalarTransformByteOrder TransformByteOrder { get; private init; }

    internal BigInteger Addend => FixedAddend
        ?? throw new InvalidOperationException("A region-instance delta addend must be resolved against one firmware map.");

    internal ScalarTransformAddendSource AddendSource { get; private init; } = null!;

    internal ulong? ExpectedBefore { get; private init; }

    internal string ProcessorStageId { get; private init; } = null!;

    private BigInteger? FixedAddend { get; init; }

    internal static CompositionOperationDefinition FillRange(
        string operationId,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        string reason,
        string targetViewId,
        byte fillByte)
    {
        return new(operationId, sequence, overlapPolicy, reason, CompositionOperationKind.FillRange)
        {
            TargetViewId = RequireId(targetViewId, nameof(targetViewId)),
            FillByte = fillByte,
        };
    }

    internal static CompositionOperationDefinition PatchScalar(
        string operationId,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        string reason,
        string targetViewId,
        CompiledValidationBytes patchBytes)
    {
        ArgumentNullException.ThrowIfNull(patchBytes);
        return new(operationId, sequence, overlapPolicy, reason, CompositionOperationKind.PatchScalar)
        {
            TargetViewId = RequireId(targetViewId, nameof(targetViewId)),
            PatchBytes = patchBytes,
        };
    }

    internal static CompositionOperationDefinition TransformScalar(
        string operationId,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        string reason,
        string sourceViewId,
        string targetViewId,
        ScalarTransformWidth width,
        ScalarTransformByteOrder byteOrder,
        BigInteger? fixedAddend,
        ScalarTransformAddendSource addendSource,
        ulong? expectedBefore)
    {
        ClosedEnum.ThrowIfUndefined(width, "Unknown scalar width.");
        ClosedEnum.ThrowIfUndefined(byteOrder, "Unknown scalar byte order.");

        if (expectedBefore is { } expected &&
            expected > (BigInteger.One << (checked((int)width) * 8)) - BigInteger.One)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedBefore),
                expectedBefore,
                "Expected scalar does not fit the declared width.");
        }

        ArgumentNullException.ThrowIfNull(addendSource);
        if (addendSource.Kind == ScalarTransformAddendSourceKind.Fixed != fixedAddend.HasValue)
        {
            throw new ArgumentException(
                "Fixed scalar addends require one value; region-instance deltas must remain unresolved.",
                nameof(fixedAddend));
        }

        if (addendSource.Kind == ScalarTransformAddendSourceKind.RegionInstanceDelta)
        {
            _ = RequireId(addendSource.SourceRegionInstanceId!, nameof(addendSource.SourceRegionInstanceId));
            _ = RequireId(addendSource.TargetRegionInstanceId!, nameof(addendSource.TargetRegionInstanceId));
        }

        return new(operationId, sequence, overlapPolicy, reason, CompositionOperationKind.TransformScalar)
        {
            SourceViewId = RequireId(sourceViewId, nameof(sourceViewId)),
            TargetViewId = RequireId(targetViewId, nameof(targetViewId)),
            TransformWidth = width,
            TransformByteOrder = byteOrder,
            FixedAddend = fixedAddend,
            AddendSource = addendSource,
            ExpectedBefore = expectedBefore,
        };
    }

    internal static CompositionOperationDefinition RunProcessor(
        string operationId,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        string reason,
        string processorStageId)
    {
        return new(
                operationId,
                sequence,
                overlapPolicy,
                reason,
                CompositionOperationKind.RunExternalProcessor)
        {
            ProcessorStageId = RequireId(processorStageId, nameof(processorStageId)),
        };
    }

    internal static CompositionOperationDefinition CopyOrReplace(
        string operationId,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        string reason,
        CompositionOperationKind kind,
        string sourceViewId,
        string targetViewId)
    {
        return kind is CompositionOperationKind.CopyRange or CompositionOperationKind.ReplaceRange
            ? new(operationId, sequence, overlapPolicy, reason, kind)
            {
                SourceViewId = RequireId(sourceViewId, nameof(sourceViewId)),
                TargetViewId = RequireId(targetViewId, nameof(targetViewId)),
            }
            : throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Copy operations must copy or replace a range.");
    }

    private static string RequireId(string value, string parameterName)
    {
        return CanonicalPolicyValueRules.RequireCanonicalId(value, parameterName);
    }

}
