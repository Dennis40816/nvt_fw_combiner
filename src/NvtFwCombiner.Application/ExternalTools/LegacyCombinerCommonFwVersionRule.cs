namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Common FW category rule for ICs with versioned postbuild scripts.</summary>
public sealed class LegacyCombinerCommonFwVersionRule
{
    /// <summary>Creates an exact Common FW category rule.</summary>
    public static LegacyCombinerCommonFwVersionRule Exact(
        string version,
        string postbuildSetupFileName)
    {
        return new LegacyCombinerCommonFwVersionRule(
            LegacyCombinerCommonFwVersionMatchKind.Exact,
            version,
            $"Common FW {version} => {postbuildSetupFileName}");
    }

    /// <summary>Creates a major-version Common FW category rule.</summary>
    public static LegacyCombinerCommonFwVersionRule Major(
        string majorVersion,
        string displayVersion,
        string postbuildSetupFileName)
    {
        return new LegacyCombinerCommonFwVersionRule(
            LegacyCombinerCommonFwVersionMatchKind.Major,
            majorVersion,
            $"Common FW {displayVersion} => {postbuildSetupFileName}");
    }

    /// <summary>Creates a Common FW category rule from owner-approved postbuild evidence.</summary>
    public LegacyCombinerCommonFwVersionRule(
        LegacyCombinerCommonFwVersionMatchKind matchKind,
        string pattern,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        MatchKind = matchKind;
        Pattern = pattern.Trim();
        Description = description.Trim();
    }

    /// <summary>Version matching strategy.</summary>
    public LegacyCombinerCommonFwVersionMatchKind MatchKind { get; }

    /// <summary>Exact version or major-version token used by the strategy.</summary>
    public string Pattern { get; }

    /// <summary>User-facing supported-category description.</summary>
    public string Description { get; }

    /// <summary>Returns whether the rule applies to a FWConfig Common FW version string.</summary>
    public bool Matches(string? commonFwVersion)
    {
        if (string.IsNullOrWhiteSpace(commonFwVersion))
        {
            return false;
        }

        string version = commonFwVersion.Trim();
        return MatchKind switch
        {
            LegacyCombinerCommonFwVersionMatchKind.Exact => string.Equals(version, Pattern, StringComparison.Ordinal),
            LegacyCombinerCommonFwVersionMatchKind.Major =>
                string.Equals(version, Pattern, StringComparison.Ordinal) ||
                version.StartsWith(Pattern + ".", StringComparison.Ordinal),
            _ => false,
        };
    }
}
