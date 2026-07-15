using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Immutable structure-relative exact or partial-mask metadata byte assertion.</summary>
public sealed class FirmwareMetadataByteAssertion
{
    private FirmwareMetadataByteAssertion(
        long offset,
        FirmwareMetadataBytes expectedBytes,
        FirmwareMetadataBytes maskBytes,
        bool isExact)
    {
        Range = new ByteRange(offset, expectedBytes.Length);
        ExpectedBytes = expectedBytes;
        MaskBytes = maskBytes;
        IsExact = isExact;
    }

    /// <summary>Checked structure-relative assertion range.</summary>
    public ByteRange Range { get; }

    /// <summary>Canonical expected bytes with masked-off bits already zero.</summary>
    public FirmwareMetadataBytes ExpectedBytes { get; }

    /// <summary>Normalized mask bytes; exact assertions use all <c>ff</c>.</summary>
    public FirmwareMetadataBytes MaskBytes { get; }

    /// <summary>Whether the source declaration canonically omitted its all-<c>ff</c> mask.</summary>
    public bool IsExact { get; }

    /// <summary>Creates the canonical exact-match form with an omitted source mask.</summary>
    public static FirmwareMetadataByteAssertion Exact(long offset, ReadOnlySpan<byte> expectedBytes)
    {
        var expected = new FirmwareMetadataBytes(expectedBytes);
        byte[] mask = new byte[expected.Length];
        Array.Fill(mask, byte.MaxValue);
        return new FirmwareMetadataByteAssertion(
            offset,
            expected,
            new FirmwareMetadataBytes(mask),
            isExact: true);
    }

    /// <summary>Creates a canonical nontrivial partial-mask assertion.</summary>
    public static FirmwareMetadataByteAssertion Masked(
        long offset,
        ReadOnlySpan<byte> expectedBytes,
        ReadOnlySpan<byte> maskBytes)
    {
        if (expectedBytes.IsEmpty)
        {
            throw new ArgumentException("Firmware metadata assertions cannot be empty.", nameof(expectedBytes));
        }

        if (maskBytes.Length != expectedBytes.Length)
        {
            throw new ArgumentException("Firmware metadata assertion masks must match expected length.", nameof(maskBytes));
        }

        bool hasSetBit = false;
        bool hasClearedBit = false;
        for (int index = 0; index < maskBytes.Length; index++)
        {
            byte mask = maskBytes[index];
            hasSetBit |= mask != 0;
            hasClearedBit |= mask != byte.MaxValue;
            if ((expectedBytes[index] & ~mask) != 0)
            {
                throw new ArgumentException(
                    "Firmware metadata assertion expected bits outside the mask must be zero.",
                    nameof(expectedBytes));
            }
        }

        return !hasSetBit
            ? throw new ArgumentException("Firmware metadata assertion masks cannot be all zero.", nameof(maskBytes))
            : !hasClearedBit
            ? throw new ArgumentException(
                "Exact firmware metadata assertions must omit an all-ff mask.",
                nameof(maskBytes))
            : new FirmwareMetadataByteAssertion(
            offset,
            new FirmwareMetadataBytes(expectedBytes),
            new FirmwareMetadataBytes(maskBytes),
            isExact: false);
    }

    /// <summary>Evaluates bytes already sliced from this assertion's resolved structure range.</summary>
    public bool Matches(ReadOnlySpan<byte> actualBytes)
    {
        if (actualBytes.Length != ExpectedBytes.Length)
        {
            return false;
        }

        ReadOnlySpan<byte> expected = ExpectedBytes.Bytes;
        ReadOnlySpan<byte> mask = MaskBytes.Bytes;
        for (int index = 0; index < actualBytes.Length; index++)
        {
            if ((actualBytes[index] & mask[index]) != expected[index])
            {
                return false;
            }
        }

        return true;
    }
}
