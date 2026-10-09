using NvtFwCombiner.Application.ExternalTools;

namespace NvtFwCombiner.Infrastructure;

/// <summary>Shared Common FW interval rule for catalogs with entries ordered by effective version.</summary>
internal static class CommonFwVersionIntervalSelector
{
    /// <summary>Selects the sole entry or the greatest effective version no higher than the input.</summary>
    internal static SelectionResult Select<TProfile>(
        IReadOnlyList<TProfile> profiles,
        Func<TProfile, LegacyCombinerCommonFwVersion> effectiveVersion,
        string? commonFwVersion,
        out int selectedIndex,
        out LegacyCombinerCommonFwVersion version)
    {
        selectedIndex = -1;
        version = default;
        if (profiles.Count == 0)
        {
            return SelectionResult.NoEntries;
        }

        bool hasVersion = LegacyCombinerCommonFwVersion.TryParse(commonFwVersion, out version);
        if (hasVersion && version < LegacyCombinerCommonFwVersion.MinimumSupported)
        {
            return SelectionResult.BelowMinimum;
        }

        if (profiles.Count == 1)
        {
            selectedIndex = 0;
            return SelectionResult.Selected;
        }

        if (!hasVersion)
        {
            return SelectionResult.VersionRequired;
        }

        for (int index = profiles.Count - 1; index >= 0; index--)
        {
            if (effectiveVersion(profiles[index]) <= version)
            {
                selectedIndex = index;
                return SelectionResult.Selected;
            }
        }

        return SelectionResult.NoMatchingInterval;
    }

    /// <summary>Selection outcome rendered by each catalog's existing diagnostic adapter.</summary>
    internal enum SelectionResult
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
