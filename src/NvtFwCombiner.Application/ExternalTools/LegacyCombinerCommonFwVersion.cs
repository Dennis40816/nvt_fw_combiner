using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Normalized three-component Common FW version used for postbuild interval selection.</summary>
public readonly record struct LegacyCombinerCommonFwVersion(byte Major, byte Minor, byte Additional)
    : IComparable<LegacyCombinerCommonFwVersion>
{
    /// <summary>Lowest Common FW version covered by production postbuild profiles.</summary>
    public static LegacyCombinerCommonFwVersion MinimumSupported { get; } = new(CommonFirmwareVersion.MinimumSupported);

    private LegacyCombinerCommonFwVersion(CommonFirmwareVersion version)
        : this(version.Major, version.Minor, version.Additional) { }

    /// <summary>The shared firmware-semantic value used by interval selection.</summary>
    public CommonFirmwareVersion CanonicalVersion => new(Major, Minor, Additional);

    /// <summary>Parses the exact FWConfig three-component representation.</summary>
    public static bool TryParse(string? value, out LegacyCombinerCommonFwVersion version)
    {
        bool parsed = CommonFirmwareVersion.TryParse(value, out CommonFirmwareVersion canonical);
        version = new(canonical);
        return parsed;
    }

    /// <inheritdoc />
    public int CompareTo(LegacyCombinerCommonFwVersion other)
    {
        return CanonicalVersion.CompareTo(other.CanonicalVersion);
    }

    /// <summary>Returns whether the left version precedes the right version.</summary>
    public static bool operator <(
        LegacyCombinerCommonFwVersion left,
        LegacyCombinerCommonFwVersion right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>Returns whether the left version does not follow the right version.</summary>
    public static bool operator <=(
        LegacyCombinerCommonFwVersion left,
        LegacyCombinerCommonFwVersion right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>Returns whether the left version follows the right version.</summary>
    public static bool operator >(
        LegacyCombinerCommonFwVersion left,
        LegacyCombinerCommonFwVersion right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>Returns whether the left version does not precede the right version.</summary>
    public static bool operator >=(
        LegacyCombinerCommonFwVersion left,
        LegacyCombinerCommonFwVersion right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return CanonicalVersion.ToString();
    }
}
