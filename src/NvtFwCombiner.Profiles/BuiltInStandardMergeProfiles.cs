namespace NvtFwCombiner.Profiles;

/// <summary>Legacy Standard Merge profiles retained as executable plan and golden parity evidence.</summary>
public static partial class BuiltInStandardMergeProfiles
{
    /// <summary>All executable legacy Standard Merge profiles used by migration parity tests, never runtime routing.</summary>
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
