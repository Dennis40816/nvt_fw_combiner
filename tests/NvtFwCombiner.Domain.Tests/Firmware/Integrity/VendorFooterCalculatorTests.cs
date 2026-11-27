// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Buffers.Binary;
using System.Text;
using NvtFwCombiner.Domain.Firmware.Integrity;

namespace NvtFwCombiner.Domain.Tests.Firmware.Integrity;

/// <summary>Checks pure footer calculations against public vectors and modular-sum invariants.</summary>
public sealed class VendorFooterCalculatorTests
{
    /// <summary>Empty input has a zero byte sum and a zero two's-complement checksum.</summary>
    [Fact]
    public void EmptyChecksumIsZero()
    {
        VendorFooterCalculationResult result = VendorFooterCalculator.Calculate("checksum32", []);

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.Equal(new byte[4], result.FullResult.ToArray());
        Assert.Equal(new byte[4], result.FooterValue.ToArray());
    }

    /// <summary>One byte is negated modulo 2^32 and stored little-endian.</summary>
    [Theory]
    [InlineData((byte)0x00, "00000000")]
    [InlineData((byte)0x01, "FFFFFFFF")]
    [InlineData((byte)0xFF, "01FFFFFF")]
    public void SingleByteChecksumCompletesTheModularSum(byte value, string expectedHex)
    {
        VendorFooterCalculationResult result = VendorFooterCalculator.Calculate("checksum32", [value]);

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.Equal(Convert.FromHexString(expectedHex), result.FullResult.ToArray());
        Assert.Equal(Convert.FromHexString(expectedHex), result.FooterValue.ToArray());
        Assert.Equal(0U, unchecked(value + BinaryPrimitives.ReadUInt32LittleEndian(result.FooterValue)));
    }

    /// <summary>A synthetic sum larger than uint.MaxValue wraps without throwing or losing the checksum identity.</summary>
    [Fact]
    public void ChecksumWrapsWhenTheCoveredByteSumOverflows32Bits()
    {
        byte[] coveredBytes = new byte[0x01010102];
        Array.Fill(coveredBytes, byte.MaxValue);
        ulong byteSum = (ulong)coveredBytes.Length * byte.MaxValue;

        VendorFooterCalculationResult result = VendorFooterCalculator.Calculate("checksum32", coveredBytes);

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.True(byteSum > uint.MaxValue);
        Assert.Equal(Convert.FromHexString("02FFFFFF"), result.FullResult.ToArray());
        Assert.Equal(Convert.FromHexString("02FFFFFF"), result.FooterValue.ToArray());
        Assert.Equal(0UL, (byteSum + BinaryPrimitives.ReadUInt32LittleEndian(result.FooterValue)) % (1UL << 32));
    }

    /// <summary>CRC integers use little-endian bytes; digests retain their full digest order and first four bytes.</summary>
    [Theory]
    [InlineData("crc32", "", "00000000", "00000000")]
    [InlineData("crc32", "abc", "C2412435", "C2412435")]
    [InlineData("crc32", "123456789", "2639F4CB", "2639F4CB")]
    [InlineData("md5", "", "D41D8CD98F00B204E9800998ECF8427E", "D41D8CD9")]
    [InlineData("md5", "abc", "900150983CD24FB0D6963F7D28E17F72", "90015098")]
    [InlineData("sha1", "", "DA39A3EE5E6B4B0D3255BFEF95601890AFD80709", "DA39A3EE")]
    [InlineData("sha1", "abc", "A9993E364706816ABA3E25717850C26C9CD0D89D", "A9993E36")]
    [InlineData("sha256", "", "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", "E3B0C442")]
    [InlineData("sha256", "abc", "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", "BA7816BF")]
    public void PublicVectorsPreserveTheFullResultAndFooterByteOrder(
        string calculatorName,
        string input,
        string expectedFullHex,
        string expectedFooterHex)
    {
        // CRC-32/ISO-HDLC (also zlib), RFC 1321, FIPS 180-4 and RFC 6234.
        byte[] coveredBytes = Encoding.ASCII.GetBytes(input);

        VendorFooterCalculationResult result = VendorFooterCalculator.Calculate(calculatorName, coveredBytes);

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.Equal(Convert.FromHexString(expectedFullHex), result.FullResult.ToArray());
        Assert.Equal(Convert.FromHexString(expectedFooterHex), result.FooterValue.ToArray());
    }

    /// <summary>Only the supplied span is covered, and calculating does not modify its backing array.</summary>
    [Theory]
    [InlineData("checksum32", "DAFEFFFF")]
    [InlineData("crc32", "C2412435")]
    [InlineData("md5", "900150983CD24FB0D6963F7D28E17F72")]
    [InlineData("sha1", "A9993E364706816ABA3E25717850C26C9CD0D89D")]
    [InlineData("sha256", "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD")]
    public void CalculatorsCoverOnlyTheSuppliedSpanWithoutMutatingInput(string calculatorName, string expectedHex)
    {
        byte[] backingBytes = [0xFF, 0x61, 0x62, 0x63, 0xFF];

        VendorFooterCalculationResult result = VendorFooterCalculator.Calculate(calculatorName, backingBytes.AsSpan(1, 3));

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.Equal(Convert.FromHexString(expectedHex), result.FullResult.ToArray());
        Assert.Equal(new byte[] { 0xFF, 0x61, 0x62, 0x63, 0xFF }, backingBytes);
    }

    /// <summary>Names are exact identifiers; missing, unknown or differently cased names fail without result bytes.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("CRC32")]
    [InlineData(" sha256 ")]
    public void UnknownCalculatorReturnsATypedErrorAndNoBytes(string? calculatorName)
    {
        VendorFooterCalculationResult result = VendorFooterCalculator.Calculate(calculatorName, []);

        Assert.False(result.Succeeded);
        Assert.Equal(VendorFooterError.UnknownCalculator, result.Error);
        Assert.Empty(result.FullResult.ToArray());
        Assert.Empty(result.FooterValue.ToArray());
    }
}
