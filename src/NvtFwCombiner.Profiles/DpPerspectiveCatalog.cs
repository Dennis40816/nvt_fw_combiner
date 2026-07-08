using System.Globalization;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Owner-approved NT51950/NT51951 DP Perspective policy shared by Merge and Replace.</summary>
public static class DpPerspectiveCatalog
{
    /// <summary>Maximum DP Perspective container length currently approved for NT51950/NT51951.</summary>
    public const long MaxContainerLength = 0x100000;

    /// <summary>Supported exact DP/base lengths for DP Perspective workflows.</summary>
    public static IReadOnlyList<long> SupportedContainerLengths { get; } =
    [
        0x40000,
        0x80000,
        MaxContainerLength,
    ];

    /// <summary>TP overlay/restore range. Customer information starts after this range.</summary>
    public static ByteRange TpOverlayRange { get; } = ByteRange.FromStartEndExclusive(0x0A000, 0x37000);

    /// <summary>TP input length required to cover the overlay range.</summary>
    public static long TpInputLength => TpOverlayRange.EndExclusive;

    /// <summary>Customer information range preserved from the base/DP container.</summary>
    public static ByteRange CustomerInfoPreserveRange { get; } = new(0x37000, 0x1000);

    /// <summary>Returns true when an IC uses the DP Perspective policy.</summary>
    public static bool IsSupportedIc(string icId)
    {
        return TryNormalizeIcId(icId, out _);
    }

    /// <summary>Returns true when the supplied length is approved for DP Perspective workflows.</summary>
    public static bool IsSupportedContainerLength(long length)
    {
        return SupportedContainerLengths.Contains(length);
    }

    /// <summary>Normalizes a DP Perspective IC id to an NT-prefixed id or throws.</summary>
    public static string NormalizeIcId(string icId)
    {
        return TryNormalizeIcId(icId, out string? normalized)
            ? normalized!
            : throw new ArgumentException($"'{icId}' is not an NT51950/NT51951 DP Perspective IC.", nameof(icId));
    }

    /// <summary>Normalizes a DP Perspective IC id to the numeric IC token or throws.</summary>
    public static string NormalizeIcNumber(string icId)
    {
        return NormalizeIcId(icId)[2..];
    }

    /// <summary>Formats the supported DP Perspective lengths for diagnostics and UI hints.</summary>
    public static string FormatSupportedLengths()
    {
        return string.Join(" / ", SupportedContainerLengths.Select(length =>
            string.Create(CultureInfo.InvariantCulture, $"0x{length:X}")));
    }

    /// <summary>Formats a DP Perspective range with inclusive display end and half-open length.</summary>
    public static string FormatRange(ByteRange range)
    {
        return FormattableString.Invariant($"0x{range.Start:X5}-0x{range.EndExclusive - 1:X5} (len 0x{range.Length:X})");
    }

    private static bool TryNormalizeIcId(string icId, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(icId))
        {
            return false;
        }

        string trimmed = icId.Trim();
        string icNumber = trimmed.StartsWith("NT", StringComparison.OrdinalIgnoreCase)
            ? trimmed[2..]
            : trimmed;
        if (icNumber is not ("51950" or "51951"))
        {
            return false;
        }

        normalized = $"NT{icNumber}";
        return true;
    }
}
