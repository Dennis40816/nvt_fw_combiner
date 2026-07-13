using System.Globalization;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Owner-approved NT51950/NT51951 DP Perspective policy shared by Merge and Replace.</summary>
public static class DpPerspectiveCatalog
{
    /// <summary>Owner-approved IC ids that use the DP Perspective policy.</summary>
    public static IReadOnlyList<string> SupportedIcIds { get; } =
    [
        "NT51950",
        "NT51951",
    ];

    /// <summary>Maximum DP Perspective container length currently approved for NT51950/NT51951.</summary>
    public const long MaxContainerLength = 0x100000;

    /// <summary>Region id for the full DP Perspective output container.</summary>
    public const string ContainerRegionId = "dp-perspective-container";

    /// <summary>Standard Merge operation id that copies the selected DP Perspective container.</summary>
    public const string CopyDpContainerOperationId = "copy-dp-container";

    /// <summary>Standard Merge operation order for copying the selected DP Perspective container.</summary>
    public const int CopyDpContainerSequence = 100;

    /// <summary>Standard Merge operation id that overlays TP FW into the DP Perspective container.</summary>
    public const string OverlayTpOperationId = "overlay-tp";

    /// <summary>Standard Merge operation order for overlaying TP FW into the DP Perspective container.</summary>
    public const int OverlayTpSequence = 200;

    /// <summary>DP Replace operation id that replaces the selected DP Perspective container.</summary>
    public const string ReplaceDpContainerOperationId = "replace-dp-container";

    /// <summary>DP Replace operation order for replacing the selected DP Perspective container.</summary>
    public const int ReplaceDpContainerSequence = 100;

    /// <summary>DP Replace operation id that restores the original TP FW range from the base image.</summary>
    public const string RestoreBaseTpOperationId = "restore-base-tp";

    /// <summary>DP Replace operation order for restoring the original TP FW range from the base image.</summary>
    public const int RestoreBaseTpSequence = 200;

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

    /// <summary>Customer-information range. DP Replace retains replacement-DP bytes in this range.</summary>
    public static ByteRange CustomerInfoRange { get; } = new(0x37000, 0x1000);

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
            : throw new ArgumentException(
                $"'{icId}' is not a {FormatSupportedIcIds()} DP Perspective IC.",
                nameof(icId));
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

    /// <summary>Formats the supported DP Perspective IC ids for diagnostics and UI hints.</summary>
    public static string FormatSupportedIcIds()
    {
        return string.Join("/", SupportedIcIds);
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
        string candidate = $"NT{icNumber}";
        if (!SupportedIcIds.Contains(candidate, StringComparer.Ordinal))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }
}
