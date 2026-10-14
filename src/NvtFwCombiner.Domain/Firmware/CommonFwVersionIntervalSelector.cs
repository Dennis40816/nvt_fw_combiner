// Copyright (c) 2026 Dennis Liu. All rights reserved.
namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Shared Common FW interval rule for catalogs with entries ordered by effective version.</summary>
public static class CommonFwVersionIntervalSelector
{
    /// <summary>Selects the sole entry or the greatest effective version no higher than the input.</summary>
    public static SelectionResult Select<TProfile>(
        IReadOnlyList<TProfile> profiles,
        Func<TProfile, CommonFirmwareVersion> effectiveVersion,
        string? commonFwVersion,
        out int selectedIndex,
        out CommonFirmwareVersion version)
    {
        bool parsed = CommonFirmwareVersion.TryParse(commonFwVersion, out version);
        return Select(profiles, effectiveVersion, parsed ? version : null, out selectedIndex);
    }

    /// <summary>Selects an interval from an already decoded canonical Common FW value.</summary>
    public static SelectionResult Select<TProfile>(
        IReadOnlyList<TProfile> profiles,
        Func<TProfile, CommonFirmwareVersion> effectiveVersion,
        CommonFirmwareVersion? version,
        out int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(effectiveVersion);
        selectedIndex = -1;
        if (profiles.Count == 0) { return SelectionResult.NoEntries; }
        if (version is { } known && known < CommonFirmwareVersion.MinimumSupported)
        { return SelectionResult.BelowMinimum; }

        if (profiles.Count == 1)
        {
            selectedIndex = 0;
            return SelectionResult.Selected;
        }

        if (version is null)
        {
            return SelectionResult.VersionRequired;
        }

        for (int index = profiles.Count - 1; index >= 0; index--)
        {
            if (effectiveVersion(profiles[index]) <= version.Value)
            {
                selectedIndex = index;
                return SelectionResult.Selected;
            }
        }

        return SelectionResult.NoMatchingInterval;
    }

    /// <summary>Selection outcome rendered by each catalog's existing diagnostic adapter.</summary>
    public enum SelectionResult
    {
        /// <summary>An entry was selected.</summary>
        Selected,

        /// <summary>The requested IC has no entries.</summary>
        NoEntries,

        /// <summary>The readable version precedes the supported minimum.</summary>
        BelowMinimum,

        /// <summary>Multiple entries require a readable three-component version.</summary>
        VersionRequired,

        /// <summary>No effective entry begins at or before the readable version.</summary>
        NoMatchingInterval,
    }
}
