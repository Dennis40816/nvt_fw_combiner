using System.Numerics;

namespace NvtFwCombiner.Domain.Composition;

/// <summary>Closed byte widths supported by one generic scalar transform.</summary>
public enum ScalarTransformWidth
{
    /// <summary>One unsigned byte.</summary>
    OneByte = 1,

    /// <summary>Two unsigned bytes.</summary>
    TwoBytes = 2,

    /// <summary>Four unsigned bytes.</summary>
    FourBytes = 4,

    /// <summary>Eight unsigned bytes.</summary>
    EightBytes = 8,
}

/// <summary>Closed byte order used to read and write one scalar transform.</summary>
public enum ScalarTransformByteOrder
{
    /// <summary>Least-significant byte at the lower address.</summary>
    LittleEndian,

    /// <summary>Most-significant byte at the lower address.</summary>
    BigEndian,
}

/// <summary>Closed overflow behavior for one scalar transform.</summary>
public enum ScalarTransformOverflowPolicy
{
    /// <summary>Fail the composition without writing when the transformed value is out of range.</summary>
    Reject,
}

/// <summary>Closed source of one already resolved scalar-transform addend.</summary>
public enum ScalarTransformAddendSourceKind
{
    /// <summary>The addend is a fixed profile-owned integer.</summary>
    Fixed,

    /// <summary>The addend is derived from two declared region-instance bases.</summary>
    RegionInstanceDelta,
}

/// <summary>Immutable identity of the authority that produced a compiled scalar addend.</summary>
public sealed record ScalarTransformAddendSource
{
    private ScalarTransformAddendSource(
        ScalarTransformAddendSourceKind kind,
        string? sourceRegionInstanceId,
        string? targetRegionInstanceId)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown scalar addend source kind.");
        }

        if (kind == ScalarTransformAddendSourceKind.Fixed)
        {
            if (sourceRegionInstanceId is not null || targetRegionInstanceId is not null)
            {
                throw new ArgumentException("Fixed scalar addends cannot name region instances.");
            }
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceRegionInstanceId);
            ArgumentException.ThrowIfNullOrWhiteSpace(targetRegionInstanceId);
        }

        Kind = kind;
        SourceRegionInstanceId = sourceRegionInstanceId;
        TargetRegionInstanceId = targetRegionInstanceId;
    }

    /// <summary>Singleton identity for one fixed numeric addend.</summary>
    public static ScalarTransformAddendSource Fixed { get; } = new(
        ScalarTransformAddendSourceKind.Fixed,
        null,
        null);

    /// <summary>Creates one resolved region-instance delta identity.</summary>
    public static ScalarTransformAddendSource RegionInstanceDelta(
        string sourceRegionInstanceId,
        string targetRegionInstanceId)
    {
        return new ScalarTransformAddendSource(
            ScalarTransformAddendSourceKind.RegionInstanceDelta,
            sourceRegionInstanceId,
            targetRegionInstanceId);
    }

    /// <summary>Kind of authority that produced the resolved numeric addend.</summary>
    public ScalarTransformAddendSourceKind Kind { get; }

    /// <summary>Source instance for a region-instance delta.</summary>
    public string? SourceRegionInstanceId { get; }

    /// <summary>Target instance for a region-instance delta.</summary>
    public string? TargetRegionInstanceId { get; }
}

/// <summary>Immutable generic unsigned scalar relocation transform.</summary>
public sealed class ScalarTransform
{
    /// <summary>Creates a checked scalar transform with an optional expected source value.</summary>
    public ScalarTransform(
        ScalarTransformWidth width,
        ScalarTransformByteOrder byteOrder,
        BigInteger addend,
        ulong? expectedBefore,
        ScalarTransformOverflowPolicy overflowPolicy)
        : this(
            width,
            byteOrder,
            addend,
            expectedBefore,
            overflowPolicy,
            ScalarTransformAddendSource.Fixed)
    {
    }

    /// <summary>Creates a checked scalar transform retaining its resolved addend authority.</summary>
    public ScalarTransform(
        ScalarTransformWidth width,
        ScalarTransformByteOrder byteOrder,
        BigInteger addend,
        ulong? expectedBefore,
        ScalarTransformOverflowPolicy overflowPolicy,
        ScalarTransformAddendSource addendSource)
    {
        if (!Enum.IsDefined(width))
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Unknown scalar transform width.");
        }

        if (!Enum.IsDefined(byteOrder))
        {
            throw new ArgumentOutOfRangeException(nameof(byteOrder), byteOrder, "Unknown scalar transform byte order.");
        }

        if (!Enum.IsDefined(overflowPolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(overflowPolicy),
                overflowPolicy,
                "Unknown scalar transform overflow policy.");
        }

        ArgumentNullException.ThrowIfNull(addendSource);
        BigInteger maximumValue = GetMaximumValue(width);
        if (addend < -maximumValue || addend > maximumValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(addend),
                addend,
                "Scalar transform addend cannot be represented for any value of the declared width.");
        }

        if (expectedBefore is { } expected)
        {
            if (expected > maximumValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedBefore),
                    expectedBefore,
                    "Expected scalar value does not fit the declared width.");
            }

            BigInteger expectedAfter = expected + addend;
            if (expectedAfter < BigInteger.Zero || expectedAfter > maximumValue)
            {
                throw new ArgumentException(
                    "Expected scalar value and addend must not overflow the declared width.",
                    nameof(addend));
            }
        }

        Width = width;
        ByteOrder = byteOrder;
        Addend = addend;
        AddendSource = addendSource;
        ExpectedBefore = expectedBefore;
        OverflowPolicy = overflowPolicy;
    }

    /// <summary>Unsigned scalar byte width.</summary>
    public ScalarTransformWidth Width { get; }

    /// <summary>Byte order used for both the source read and target write.</summary>
    public ScalarTransformByteOrder ByteOrder { get; }

    /// <summary>Checked signed value added to the source scalar.</summary>
    public BigInteger Addend { get; }

    /// <summary>Authority that produced the already resolved numeric addend.</summary>
    public ScalarTransformAddendSource AddendSource { get; }

    /// <summary>Optional exact source scalar expected before the transform may write.</summary>
    public ulong? ExpectedBefore { get; }

    /// <summary>Declared checked-overflow behavior.</summary>
    public ScalarTransformOverflowPolicy OverflowPolicy { get; }

    /// <summary>Width expressed as a byte count.</summary>
    public int WidthBytes => (int)Width;

    internal BigInteger MaximumValue => GetMaximumValue(Width);

    private static BigInteger GetMaximumValue(ScalarTransformWidth width)
    {
        return (BigInteger.One << (checked((int)width) * 8)) - BigInteger.One;
    }
}
