using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets output address coverage text for the selected Standard Merge profile.</summary>
    public static string GetStandardMergeMemoryRangeLabel(string icId)
    {
        return GetStandardMergeMemoryRangeLabel(icId, dpInputLength: null);
    }

    /// <summary>Gets output address coverage text for the selected Standard Merge profile and DP input length.</summary>
    public static string GetStandardMergeMemoryRangeLabel(string icId, long? dpInputLength)
    {
        return !StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile)
            ? "No Standard Merge profile"
            : IsDpPerspectiveLengthPending(profile, dpInputLength)
                ? "Selected DP BIN length pending"
                : TryResolveStandardMergeProfileForDisplay(profile, dpInputLength, out profile, out string profileIssue)
                    ? FormatFullRange(profile.Initialization.Capacity)
                    : profileIssue;
    }

    private static bool TryResolveStandardMergeProfileForDisplay(
        CompositionProfileDefinition profile,
        long? dpInputLength,
        out CompositionProfileDefinition resolvedProfile,
        out string profileIssue)
    {
        resolvedProfile = profile;
        profileIssue = string.Empty;
        if (dpInputLength is null ||
            !BuiltInStandardMergeProfiles.IsDpPerspectiveStandardMergeProfile(profile))
        {
            return true;
        }

        try
        {
            resolvedProfile = BuiltInStandardMergeProfiles.CreateDpPerspectiveProfileForInputLength(
                profile.IcId,
                dpInputLength.Value);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            profileIssue = FormattableString.Invariant(
                $"Selected DP BIN length 0x{dpInputLength.Value:X} is unsupported; expected {DpPerspectiveCatalog.FormatSupportedLengths()}.");
            return false;
        }
    }

    private static bool IsDpPerspectiveLengthPending(
        CompositionProfileDefinition profile,
        long? dpInputLength)
    {
        return BuiltInStandardMergeProfiles.IsDpPerspectiveStandardMergeProfile(profile) &&
            dpInputLength is null;
    }

    private static string FormatStandardMergeInitializationRangeLabel(
        CompositionProfileDefinition profile,
        long? dpInputLength)
    {
        return IsDpPerspectiveLengthPending(profile, dpInputLength)
            ? "Selected DP BIN length pending"
            : FormatFullRange(profile.Initialization.Capacity);
    }

    private static string FormatStandardMergeInitializationDetail(
        CompositionProfileDefinition profile,
        long? dpInputLength)
    {
        return !BuiltInStandardMergeProfiles.IsDpPerspectiveStandardMergeProfile(profile)
            ? "Start with the initialized image. Unlisted ranges keep this value until a later operation writes them."
            : dpInputLength is null
                ? $"Start with the initialized image after selecting a DP BIN. Supported DP lengths are {DpPerspectiveCatalog.FormatSupportedLengths()}."
                : "Start with the initialized image using the selected DP BIN length. Unlisted ranges keep this value until a later operation writes them.";
    }
}
