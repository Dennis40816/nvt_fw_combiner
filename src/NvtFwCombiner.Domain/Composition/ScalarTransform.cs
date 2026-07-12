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
        ExpectedBefore = expectedBefore;
        OverflowPolicy = overflowPolicy;
    }

    /// <summary>Unsigned scalar byte width.</summary>
    public ScalarTransformWidth Width { get; }

    /// <summary>Byte order used for both the source read and target write.</summary>
    public ScalarTransformByteOrder ByteOrder { get; }

    /// <summary>Checked signed value added to the source scalar.</summary>
    public BigInteger Addend { get; }

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
