using System.Numerics;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Base value for one ordered normalized profile operation.</summary>
internal abstract record CompositionProfileOperation
{
    protected CompositionProfileOperation(
        string operationId,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        string reason,
        CompositionOperationKind kind)
    {
        OperationId = CompositionProfileValueRules.RequireId(operationId, nameof(operationId));
        if (sequence.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Operation sequence cannot be negative.");
        }
        if (!Enum.IsDefined(overlapPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(overlapPolicy), overlapPolicy, "Unknown overlap policy.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown profile operation kind.");
        }

        Sequence = sequence;
        OverlapPolicy = overlapPolicy;
        Reason = reason;
        Kind = kind;
    }

    internal string OperationId { get; }

    internal BigInteger Sequence { get; }

    internal OverlapPolicy OverlapPolicy { get; }

    internal string Reason { get; }

    internal CompositionOperationKind Kind { get; }
}

/// <summary>Copies or replaces one source logical view into one target logical view.</summary>
internal sealed record CopyOrReplaceProfileOperation : CompositionProfileOperation
{
    internal CopyOrReplaceProfileOperation(
        string operationId,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        string reason,
        CompositionOperationKind kind,
        string sourceViewId,
        string targetViewId)
        : base(operationId, sequence, overlapPolicy, reason, ValidateKind(kind))
    {
        SourceViewId = CompositionProfileValueRules.RequireId(sourceViewId, nameof(sourceViewId));
        TargetViewId = CompositionProfileValueRules.RequireId(targetViewId, nameof(targetViewId));
    }

    internal string SourceViewId { get; }

    internal string TargetViewId { get; }

    private static CompositionOperationKind ValidateKind(CompositionOperationKind kind)
    {
        return kind is CompositionOperationKind.CopyRange or CompositionOperationKind.ReplaceRange
            ? kind
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Copy operations must copy or replace a range.");
    }
}

/// <summary>Fills one target logical view with an exact byte.</summary>
internal sealed record FillRangeProfileOperation : CompositionProfileOperation
{
    internal FillRangeProfileOperation(
        string operationId,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        string reason,
        string targetViewId,
        byte fillByte)
        : base(
            operationId,
            sequence,
            overlapPolicy,
            reason,
            CompositionOperationKind.FillRange)
    {
        TargetViewId = CompositionProfileValueRules.RequireId(targetViewId, nameof(targetViewId));
        FillByte = fillByte;
    }

    internal string TargetViewId { get; }

    internal byte FillByte { get; }
}

/// <summary>Writes exact profile-owned bytes to one target logical view.</summary>
internal sealed record PatchScalarProfileOperation : CompositionProfileOperation
{
    internal PatchScalarProfileOperation(
        string operationId,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        string reason,
        string targetViewId,
        CompiledValidationBytes value)
        : base(
            operationId,
            sequence,
            overlapPolicy,
            reason,
            CompositionOperationKind.PatchScalar)
    {
        TargetViewId = CompositionProfileValueRules.RequireId(targetViewId, nameof(targetViewId));
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    internal string TargetViewId { get; }

    internal CompiledValidationBytes Value { get; }
}

/// <summary>Reads an unsigned scalar, adds a checked signed value, and writes one target view.</summary>
internal sealed record TransformScalarProfileOperation : CompositionProfileOperation
{
    internal TransformScalarProfileOperation(
        string operationId,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        string reason,
        string sourceViewId,
        string targetViewId,
        ScalarTransformWidth width,
        ScalarTransformByteOrder byteOrder,
        BigInteger addend,
        ulong? expectedBefore)
        : this(
            operationId,
            sequence,
            overlapPolicy,
            reason,
            sourceViewId,
            targetViewId,
            width,
            byteOrder,
            addend,
            ScalarTransformAddendSource.Fixed,
            expectedBefore)
    {
    }

    internal TransformScalarProfileOperation(
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
        : base(
            operationId,
            sequence,
            overlapPolicy,
            reason,
            CompositionOperationKind.TransformScalar)
    {
        SourceViewId = CompositionProfileValueRules.RequireId(sourceViewId, nameof(sourceViewId));
        TargetViewId = CompositionProfileValueRules.RequireId(targetViewId, nameof(targetViewId));
        if (!Enum.IsDefined(width))
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Unknown scalar width.");
        }

        if (!Enum.IsDefined(byteOrder))
        {
            throw new ArgumentOutOfRangeException(nameof(byteOrder), byteOrder, "Unknown scalar byte order.");
        }

        if (expectedBefore is { } expected && !CanRepresent(width, expected))
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
            _ = CanonicalPolicyValueRules.RequireCanonicalId(
                addendSource.SourceRegionInstanceId!,
                nameof(addendSource.SourceRegionInstanceId));
            _ = CanonicalPolicyValueRules.RequireCanonicalId(
                addendSource.TargetRegionInstanceId!,
                nameof(addendSource.TargetRegionInstanceId));
        }

        Width = width;
        ByteOrder = byteOrder;
        FixedAddend = fixedAddend;
        AddendSource = addendSource;
        ExpectedBefore = expectedBefore;
    }

    internal string SourceViewId { get; }

    internal string TargetViewId { get; }

    internal ScalarTransformWidth Width { get; }

    internal ScalarTransformByteOrder ByteOrder { get; }

    internal BigInteger Addend => FixedAddend is { } fixedAddend
        ? fixedAddend
        : throw new InvalidOperationException(
            "A region-instance delta addend must be resolved against one firmware map.");

    internal ScalarTransformAddendSource AddendSource { get; }

    internal ulong? ExpectedBefore { get; }

    private BigInteger? FixedAddend { get; }

    private static bool CanRepresent(ScalarTransformWidth width, ulong value)
    {
        return width switch
        {
            ScalarTransformWidth.OneByte => value <= byte.MaxValue,
            ScalarTransformWidth.TwoBytes => value <= ushort.MaxValue,
            ScalarTransformWidth.FourBytes => value <= uint.MaxValue,
            ScalarTransformWidth.EightBytes => true,
            _ => false,
        };
    }
}

/// <summary>Invokes one separately declared closed processor stage.</summary>
internal sealed record RunProcessorProfileOperation : CompositionProfileOperation
{
    internal RunProcessorProfileOperation(
        string operationId,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        string reason,
        string processorStageId)
        : base(
            operationId,
            sequence,
            overlapPolicy,
            reason,
            CompositionOperationKind.RunExternalProcessor)
    {
        ProcessorStageId = CompositionProfileValueRules.RequireId(
            processorStageId,
            nameof(processorStageId));
    }

    internal string ProcessorStageId { get; }
}
