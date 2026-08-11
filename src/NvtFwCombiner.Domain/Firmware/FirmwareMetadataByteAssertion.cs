using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Immutable structure-relative exact or partial-mask metadata byte assertion.</summary>
public sealed class FirmwareMetadataByteAssertion
{
    private FirmwareMetadataByteAssertion(
        long offset,
        FirmwareMetadataBytes expectedBytes,
        FirmwareMetadataBytes maskBytes)
    {
        Range = new ByteRange(offset, expectedBytes.Length);
        ExpectedBytes = expectedBytes;
        MaskBytes = maskBytes;
    }

    /// <summary>Checked structure-relative assertion range.</summary>
    public ByteRange Range { get; }

    /// <summary>Canonical expected bytes with masked-off bits already zero.</summary>
    public FirmwareMetadataBytes ExpectedBytes { get; }

    /// <summary>Normalized mask bytes; exact assertions use all <c>ff</c>.</summary>
    public FirmwareMetadataBytes MaskBytes { get; }

    /// <summary>Creates the canonical exact-match form with an omitted source mask.</summary>
    public static FirmwareMetadataByteAssertion Exact(long offset, ReadOnlySpan<byte> expectedBytes)
    {
        var expected = new FirmwareMetadataBytes(expectedBytes);
        byte[] mask = new byte[expected.Length];
        Array.Fill(mask, byte.MaxValue);
        return new FirmwareMetadataByteAssertion(
            offset,
            expected,
            new FirmwareMetadataBytes(mask));
    }

    /// <summary>Creates a canonical nontrivial partial-mask assertion.</summary>
    public static FirmwareMetadataByteAssertion Masked(
        long offset,
        ReadOnlySpan<byte> expectedBytes,
        ReadOnlySpan<byte> maskBytes)
    {
        DomainInvariant.Reject(
            expectedBytes.IsEmpty,
            "Firmware metadata assertions cannot be empty.", nameof(expectedBytes));

        DomainInvariant.Reject(
            maskBytes.Length != expectedBytes.Length,
            "Firmware metadata assertion masks must match expected length.", nameof(maskBytes));

        bool hasSetBit = false;
        bool hasClearedBit = false;
        for (int index = 0; index < maskBytes.Length; index++)
        {
            byte mask = maskBytes[index];
            hasSetBit |= mask != 0;
            hasClearedBit |= mask != byte.MaxValue;
            DomainInvariant.Reject(
                (expectedBytes[index] & ~mask) != 0,
                "Firmware metadata assertion expected bits outside the mask must be zero.",
                nameof(expectedBytes));
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
            new FirmwareMetadataBytes(maskBytes));
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
