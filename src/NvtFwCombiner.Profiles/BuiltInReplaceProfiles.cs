namespace NvtFwCombiner.Profiles;

/// <summary>Built-in Replace profiles used as executable command and contract evidence.</summary>
public static partial class BuiltInReplaceProfiles
{
    /// <summary>All built-in Replace profiles in stable command display order.</summary>
    public static IReadOnlyList<CompositionProfileDefinition> All =>
    [
        SyntheticDpReplace,
        SyntheticCtrlRamReplace,
        SyntheticGeneralReplace,
    ];
}
