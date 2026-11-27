// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Buffers.Binary;
using NvtFwCombiner.Domain.Firmware.Integrity;

namespace NvtFwCombiner.Domain.Tests.Firmware.Integrity;

/// <summary>Checks the complete 16-byte footer and typed local-calendar admission.</summary>
public sealed class VendorFooterBuilderTests
{
    /// <summary>The owner's timestamp is encoded as BCD with a checksum over synthetic covered bytes only.</summary>
    [Fact]
    public void OwnerTimestampProducesTheCompleteFooterAndChecksumIdentity()
    {
        byte[] coveredBytes = [0x00, 0x01, 0x02, 0x80, 0xFF];
        uint byteSum = (uint)coveredBytes.Sum(static value => value);
        var localDateTime = new DateTime(2026, 10, 1, 19, 40, 15, DateTimeKind.Unspecified);

        VendorFooterBuildResult result = VendorFooterBuilder.Build(coveredBytes, 0x00, "checksum32", localDateTime);

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.Equal(Convert.FromHexString("20261001194015007EFEFFFF00000000"), result.Bytes.ToArray());
        VendorFooterCalculationResult calculation = Assert.IsType<VendorFooterCalculationResult>(result.Calculation);
        Assert.Equal(Convert.FromHexString("7EFEFFFF"), calculation.FullResult.ToArray());
        Assert.Equal(Convert.FromHexString("7EFEFFFF"), calculation.FooterValue.ToArray());
        Assert.Equal(0U, unchecked(byteSum + BinaryPrimitives.ReadUInt32LittleEndian(result.Bytes.Slice(8, 4))));
        Assert.Equal(new byte[] { 0x00, 0x01, 0x02, 0x80, 0xFF }, coveredBytes);
    }

    /// <summary>The caller may choose any method byte independently of the calculator name.</summary>
    [Theory]
    [InlineData((byte)0x00, "sha256", "2026100119401500BA7816BF00000000", "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD")]
    [InlineData((byte)0xA5, "crc32", "20261001194015A5C241243500000000", "C2412435")]
    [InlineData((byte)0xFF, "md5", "20261001194015FF9001509800000000", "900150983CD24FB0D6963F7D28E17F72")]
    [InlineData((byte)0x42, "sha1", "2026100119401542A9993E3600000000", "A9993E364706816ABA3E25717850C26C9CD0D89D")]
    [InlineData((byte)0xA5, "checksum32", "20261001194015A5DAFEFFFF00000000", "DAFEFFFF")]
    public void MethodCodeIsIndependentOfTheCalculatorAndTheFullResultIsRetained(
        byte methodCode,
        string calculatorName,
        string expectedFooterHex,
        string expectedFullHex)
    {
        byte[] coveredBytes = [0x61, 0x62, 0x63];
        var localDateTime = new DateTime(2026, 10, 1, 19, 40, 15, DateTimeKind.Unspecified);

        VendorFooterBuildResult result = VendorFooterBuilder.Build(coveredBytes, methodCode, calculatorName, localDateTime);

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.Equal(Convert.FromHexString(expectedFooterHex), result.Bytes.ToArray());
        VendorFooterCalculationResult calculation = Assert.IsType<VendorFooterCalculationResult>(result.Calculation);
        Assert.Equal(Convert.FromHexString(expectedFullHex), calculation.FullResult.ToArray());
        Assert.Equal(result.Bytes.Slice(8, 4).ToArray(), calculation.FooterValue.ToArray());
    }

    /// <summary>Four year digits include leading zeros, year zero and the largest admitted year.</summary>
    [Theory]
    [InlineData(0, 1, 1, 0, 0, 0, "00000101000000000000000000000000")]
    [InlineData(1, 2, 3, 4, 5, 6, "00010203040506000000000000000000")]
    [InlineData(99, 12, 31, 23, 59, 59, "00991231235959000000000000000000")]
    [InlineData(100, 1, 1, 0, 0, 0, "01000101000000000000000000000000")]
    [InlineData(9999, 12, 31, 23, 59, 59, "99991231235959000000000000000000")]
    [InlineData(0, 2, 29, 0, 0, 0, "00000229000000000000000000000000")]
    [InlineData(2000, 2, 29, 0, 0, 0, "20000229000000000000000000000000")]
    public void CalendarBoundariesUseFourYearDigitsAndPackedBcd(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second,
        string expectedHex)
    {
        var timestamp = new VendorFooterTimestamp(year, month, day, hour, minute, second);

        VendorFooterBuildResult result = VendorFooterBuilder.Build([], 0x00, "checksum32", timestamp);

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.Equal(Convert.FromHexString(expectedHex), result.Bytes.ToArray());
    }

    /// <summary>Years outside four BCD digits fail as a typed error before any footer is produced.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(10000)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void OutOfRangeYearReturnsATypedErrorAndNoFooter(int year)
    {
        var timestamp = new VendorFooterTimestamp(year, 10, 1, 19, 40, 15);

        VendorFooterBuildResult result = VendorFooterBuilder.Build([], 0x00, "checksum32", timestamp);

        Assert.False(result.Succeeded);
        Assert.Equal(VendorFooterError.YearOutOfRange, result.Error);
        Assert.Empty(result.Bytes.ToArray());
        Assert.Null(result.Calculation);
    }

    /// <summary>Invalid calendar or clock fields cannot become malformed BCD or a misleading date.</summary>
    [Theory]
    [InlineData(2026, 0, 1, 0, 0, 0)]
    [InlineData(2026, 13, 1, 0, 0, 0)]
    [InlineData(2026, 1, 0, 0, 0, 0)]
    [InlineData(2026, 1, 32, 0, 0, 0)]
    [InlineData(2026, 4, 31, 0, 0, 0)]
    [InlineData(2026, 2, 29, 0, 0, 0)]
    [InlineData(1900, 2, 29, 0, 0, 0)]
    [InlineData(2026, 1, 1, -1, 0, 0)]
    [InlineData(2026, 1, 1, 24, 0, 0)]
    [InlineData(2026, 1, 1, 0, -1, 0)]
    [InlineData(2026, 1, 1, 0, 60, 0)]
    [InlineData(2026, 1, 1, 0, 0, -1)]
    [InlineData(2026, 1, 1, 0, 0, 60)]
    public void InvalidLocalDateTimeReturnsATypedErrorAndNoFooter(int year, int month, int day, int hour, int minute, int second)
    {
        var timestamp = new VendorFooterTimestamp(year, month, day, hour, minute, second);

        VendorFooterBuildResult result = VendorFooterBuilder.Build([], 0x00, "checksum32", timestamp);

        Assert.False(result.Succeeded);
        Assert.Equal(VendorFooterError.InvalidLocalDateTime, result.Error);
        Assert.Empty(result.Bytes.ToArray());
        Assert.Null(result.Calculation);
    }

    /// <summary>An unrecognized calculator fails without an output footer or a partial calculation.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    public void UnknownCalculatorReturnsATypedErrorAndNoFooter(string? calculatorName)
    {
        var localDateTime = new DateTime(2026, 10, 1, 19, 40, 15, DateTimeKind.Unspecified);

        VendorFooterBuildResult result = VendorFooterBuilder.Build([], 0x00, calculatorName, localDateTime);

        Assert.False(result.Succeeded);
        Assert.Equal(VendorFooterError.UnknownCalculator, result.Error);
        Assert.Empty(result.Bytes.ToArray());
        Assert.Null(result.Calculation);
    }

    /// <summary>The supplied calendar fields are unchanged by the machine time zone or DateTime kind.</summary>
    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    public void SuppliedClockFieldsAreEncodedWithoutTimeZoneConversion(DateTimeKind kind)
    {
        var localDateTime = new DateTime(2026, 10, 1, 0, 4, 5, 999, kind);

        VendorFooterBuildResult result = VendorFooterBuilder.Build([], 0x00, "checksum32", localDateTime);

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.Equal(Convert.FromHexString("20261001000405000000000000000000"), result.Bytes.ToArray());
    }
}
