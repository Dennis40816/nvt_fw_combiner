using System.Numerics;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Closed normalized profile operation kind.</summary>
internal enum CompositionProfileOperationKind
{
    CopyRange,
    ReplaceRange,
    FillRange,
    PatchScalar,
    TransformScalar,
    RunProcessor,
}

/// <summary>Immutable exact byte value used by profile patches and assertions.</summary>
internal sealed class CompositionProfileByteValue : IEquatable<CompositionProfileByteValue>
{
    private readonly byte[] _bytes;

    internal CompositionProfileByteValue(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            throw new ArgumentException("Profile byte values cannot be empty.", nameof(bytes));
        }

        _bytes = bytes.ToArray();
        Hex = Convert.ToHexString(_bytes).ToLowerInvariant();
    }

    internal int Length => _bytes.Length;

    internal string Hex { get; }

    public bool Equals(CompositionProfileByteValue? other)
    {
        return other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);
    }

    public override bool Equals(object? obj)
    {
        return obj is CompositionProfileByteValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (byte value in _bytes)
            {
                hash = (hash * 31) + value;
            }

            return hash;
        }
    }

    public override string ToString()
    {
        return Hex;
    }
}

/// <summary>Base value for one ordered normalized profile operation.</summary>
internal abstract record CompositionProfileOperation
{
    protected CompositionProfileOperation(
        string operationId,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        string reason,
        CompositionProfileOperationKind kind)
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

    internal CompositionProfileOperationKind Kind { get; }
}

/// <summary>Copies or replaces one source logical view into one target logical view.</summary>
internal sealed record CopyOrReplaceProfileOperation : CompositionProfileOperation
{
    internal CopyOrReplaceProfileOperation(
        string operationId,
        BigInteger sequence,
        OverlapPolicy overlapPolicy,
        string reason,
        CompositionProfileOperationKind kind,
        string sourceViewId,
        string targetViewId)
        : base(operationId, sequence, overlapPolicy, reason, ValidateKind(kind))
    {
        SourceViewId = CompositionProfileValueRules.RequireId(sourceViewId, nameof(sourceViewId));
        TargetViewId = CompositionProfileValueRules.RequireId(targetViewId, nameof(targetViewId));
    }

    internal string SourceViewId { get; }

    internal string TargetViewId { get; }

    private static CompositionProfileOperationKind ValidateKind(CompositionProfileOperationKind kind)
    {
        return kind is CompositionProfileOperationKind.CopyRange or CompositionProfileOperationKind.ReplaceRange
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
            CompositionProfileOperationKind.FillRange)
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
        CompositionProfileByteValue value)
        : base(
            operationId,
            sequence,
            overlapPolicy,
            reason,
            CompositionProfileOperationKind.PatchScalar)
    {
        TargetViewId = CompositionProfileValueRules.RequireId(targetViewId, nameof(targetViewId));
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    internal string TargetViewId { get; }

    internal CompositionProfileByteValue Value { get; }
}

/// <summary>Closed scalar width supported by bounded relocation transforms.</summary>
internal enum CompositionProfileScalarWidth
{
    OneByte = 1,
    TwoBytes = 2,
    FourBytes = 4,
    EightBytes = 8,
}

/// <summary>Closed byte order for one unsigned scalar transform.</summary>
internal enum CompositionProfileScalarByteOrder
{
    LittleEndian,
    BigEndian,
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
        CompositionProfileScalarWidth width,
        CompositionProfileScalarByteOrder byteOrder,
        BigInteger addend,
        ulong? expectedBefore)
        : base(
            operationId,
            sequence,
            overlapPolicy,
            reason,
            CompositionProfileOperationKind.TransformScalar)
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

        Width = width;
        ByteOrder = byteOrder;
        Addend = addend;
        ExpectedBefore = expectedBefore;
    }

    internal string SourceViewId { get; }

    internal string TargetViewId { get; }

    internal CompositionProfileScalarWidth Width { get; }

    internal CompositionProfileScalarByteOrder ByteOrder { get; }

    internal BigInteger Addend { get; }

    internal ulong? ExpectedBefore { get; }

    private static bool CanRepresent(CompositionProfileScalarWidth width, ulong value)
    {
        return width switch
        {
            CompositionProfileScalarWidth.OneByte => value <= byte.MaxValue,
            CompositionProfileScalarWidth.TwoBytes => value <= ushort.MaxValue,
            CompositionProfileScalarWidth.FourBytes => value <= uint.MaxValue,
            CompositionProfileScalarWidth.EightBytes => true,
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
            CompositionProfileOperationKind.RunProcessor)
    {
        ProcessorStageId = CompositionProfileValueRules.RequireId(
            processorStageId,
            nameof(processorStageId));
    }

    internal string ProcessorStageId { get; }
}
