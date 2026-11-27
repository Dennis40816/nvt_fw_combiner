// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace NvtFwCombiner.Domain.Firmware.Integrity;

/// <summary>Builds a footer from supplied values without reading a clock, choosing method codes or appending output.</summary>
internal static class VendorFooterBuilder
{
    /// <summary>Uses the supplied calendar fields to second precision, without time-zone conversion.</summary>
    internal static VendorFooterBuildResult Build(
        ReadOnlySpan<byte> coveredBytes,
        byte methodCode,
        string? calculatorName,
        DateTime localDateTime)
    {
        return Build(coveredBytes, methodCode, calculatorName, new VendorFooterTimestamp(
            localDateTime.Year,
            localDateTime.Month,
            localDateTime.Day,
            localDateTime.Hour,
            localDateTime.Minute,
            localDateTime.Second));
    }

    /// <summary>Admits years 0000 through 9999 and returns exactly 16 bytes, or a typed error with no footer.</summary>
    internal static VendorFooterBuildResult Build(
        ReadOnlySpan<byte> coveredBytes,
        byte methodCode,
        string? calculatorName,
        VendorFooterTimestamp localDateTime)
    {
        if (localDateTime.Year is < 0 or > 9999)
        {
            return VendorFooterBuildResult.Failure(VendorFooterError.YearOutOfRange);
        }

        if (!IsValidLocalDateTime(localDateTime))
        {
            return VendorFooterBuildResult.Failure(VendorFooterError.InvalidLocalDateTime);
        }

        VendorFooterCalculationResult calculation = VendorFooterCalculator.Calculate(calculatorName, coveredBytes);
        if (calculation.Error is { } error)
        {
            return VendorFooterBuildResult.Failure(error);
        }

        byte[] footer = new byte[16];
        footer[0] = Bcd(localDateTime.Year / 100);
        footer[1] = Bcd(localDateTime.Year % 100);
        footer[2] = Bcd(localDateTime.Month);
        footer[3] = Bcd(localDateTime.Day);
        footer[4] = Bcd(localDateTime.Hour);
        footer[5] = Bcd(localDateTime.Minute);
        footer[6] = Bcd(localDateTime.Second);
        footer[7] = methodCode;
        calculation.FooterValue.CopyTo(footer.AsSpan(8, 4));
        return VendorFooterBuildResult.Success(footer, calculation);
    }

    private static bool IsValidLocalDateTime(VendorFooterTimestamp value)
    {
        // Gregorian leap years repeat every 400 years; DateTime itself cannot accept year zero.
        return value.Month is >= 1 and <= 12 &&
            value.Day >= 1 &&
            value.Day <= DateTime.DaysInMonth(value.Year == 0 ? 400 : value.Year, value.Month) &&
            value.Hour is >= 0 and < 24 &&
            value.Minute is >= 0 and < 60 &&
            value.Second is >= 0 and < 60;
    }

    private static byte Bcd(int value)
    {
        return (byte)(((value / 10) << 4) | (value % 10));
    }
}
