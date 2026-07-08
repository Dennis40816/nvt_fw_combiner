namespace NvtFwCombiner.Profiles;

/// <summary>Built-in standard merge profiles used as executable contract evidence.</summary>
public static partial class BuiltInStandardMergeProfiles
{
    /// <summary>All executable built-in standard merge profiles exposed by CLI/UI.</summary>
    public static IReadOnlyList<CompositionProfileDefinition> ExecutableStandardMergeProfiles =>
    [
        .. GenFlashStandardMergeProfiles,
        .. OwnerConfirmedAliasStandardMergeProfiles,
        .. FlashMapStandardMergeProfiles,
        .. DpPerspectiveStandardMergeProfiles,
    ];

    /// <summary>All built-in standard merge profiles, including synthetic and reference-derived cases.</summary>
    public static IReadOnlyList<CompositionProfileDefinition> All =>
    [
        SyntheticStandardMerge,
        .. ExecutableStandardMergeProfiles,
    ];
}
