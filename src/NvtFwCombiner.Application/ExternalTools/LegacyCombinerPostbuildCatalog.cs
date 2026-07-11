namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Owner-provided CtrlRAM postbuild command profiles normalized from postbuild scripts.</summary>
public static partial class LegacyCombinerPostbuildCatalog
{
    private const string ToolBindingId = "legacy-combiner-1.13.0";

    /// <summary>Supported postbuild profiles in stable IC order.</summary>
    public static IReadOnlyList<LegacyCombinerPostbuildProfile> All =>
    [
        Nt51917,
        Nt51919,
        Nt51920,
        Nt51923,
        Nt51926,
        Nt51926CommonFw141,
        Nt51927,
        Nt51928,
        Nt51929,
        Nt51930,
        Nt51930CommonFw1x,
        Nt51931,
        Nt51932,
        Nt51950,
        Nt51951,
    ];

    /// <summary>Gets every approved postbuild profile for an IC, including codebase-version variants.</summary>
    public static IReadOnlyList<LegacyCombinerPostbuildProfile> GetProfiles(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        return [
            .. All.Where(profile => string.Equals(profile.IcId, icId, StringComparison.Ordinal)),
        ];
    }

    /// <summary>Gets the default postbuild profile used for catalog-only display when no base image is available.</summary>
    public static bool TryGetDefaultProfile(
        string icId,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = GetProfiles(icId);
        postbuildProfile = profiles.Count == 0 ? null : profiles[0];
        return profiles.Count > 0;
    }

    /// <summary>Selects the postbuild category for a base image Common FW version.</summary>
    public static bool TrySelectProfileForCommonFwVersion(
        string icId,
        string? commonFwVersion,
        out LegacyCombinerPostbuildProfile? postbuildProfile,
        out string? issue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = GetProfiles(icId);
        if (profiles.Count == 0)
        {
            postbuildProfile = null;
            issue = $"No legacy Combiner postbuild profile is registered for {icId}.";
            return false;
        }

        if (profiles.Count == 1)
        {
            postbuildProfile = profiles[0];
            issue = null;
            return true;
        }

        if (TryResolveVersionedProfile(profiles, commonFwVersion, out postbuildProfile))
        {
            issue = null;
            return true;
        }

        issue = string.IsNullOrWhiteSpace(commonFwVersion)
            ? $"{icId} has multiple postbuild categories; base FWConfig Common FW version is required."
            : $"{icId} Common FW {commonFwVersion} has no approved postbuild category. Supported categories: {DescribeSupportedCategories(profiles)}.";
        return false;
    }

    private static bool TryResolveVersionedProfile(
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles,
        string? commonFwVersion,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        postbuildProfile = null;
        if (string.IsNullOrWhiteSpace(commonFwVersion))
        {
            return false;
        }

        LegacyCombinerPostbuildProfile[] matches =
        [
            .. profiles.Where(profile => profile.CommonFwVersionRule?.Matches(commonFwVersion) == true),
        ];
        if (matches.Length != 1)
        {
            return false;
        }

        postbuildProfile = matches[0];
        return true;
    }

    private static string DescribeSupportedCategories(IReadOnlyList<LegacyCombinerPostbuildProfile> profiles)
    {
        string[] descriptions =
        [
            .. profiles
                .Select(profile => profile.CommonFwVersionRule?.Description)
                .Where(description => !string.IsNullOrWhiteSpace(description))
                .Cast<string>(),
        ];

        return descriptions.Length == 0
            ? "no versioned postbuild categories declared"
            : string.Join("; ", descriptions);
    }

}
