// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Normalized three-component Common FW version used for firmware interval selection.</summary>
public readonly record struct CommonFirmwareVersion(byte Major, byte Minor, byte Additional)
    : IComparable<CommonFirmwareVersion>
{
    /// <summary>Lowest Common FW version covered by production postbuild profiles.</summary>
    public static CommonFirmwareVersion MinimumSupported { get; } = new(1, 0, 0);

    /// <summary>Parses the exact FWConfig three-component representation.</summary>
    public static bool TryParse(string? value, out CommonFirmwareVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] parts = value.Trim().Split('.');
        if (parts.Length != 3 ||
            !byte.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out byte major) ||
            !byte.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out byte minor) ||
            !byte.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out byte additional))
        {
            return false;
        }

        version = new CommonFirmwareVersion(major, minor, additional);
        return true;
    }

    /// <inheritdoc />
    public int CompareTo(CommonFirmwareVersion other)
    {
        int major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        int minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Additional.CompareTo(other.Additional);
    }

    /// <summary>Returns whether the left version precedes the right version.</summary>
    public static bool operator <(
        CommonFirmwareVersion left,
        CommonFirmwareVersion right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>Returns whether the left version does not follow the right version.</summary>
    public static bool operator <=(
        CommonFirmwareVersion left,
        CommonFirmwareVersion right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>Returns whether the left version follows the right version.</summary>
    public static bool operator >(
        CommonFirmwareVersion left,
        CommonFirmwareVersion right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>Returns whether the left version does not precede the right version.</summary>
    public static bool operator >=(
        CommonFirmwareVersion left,
        CommonFirmwareVersion right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Major}.{Minor}.{Additional}");
    }
}
