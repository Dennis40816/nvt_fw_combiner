namespace NvtFwCombiner.Profiles;

/// <summary>Legacy synthetic Replace profiles used as executable command and contract evidence.</summary>
public static partial class BuiltInReplaceProfiles
{
    /// <summary>All legacy synthetic Replace profiles in stable command display order.</summary>
    public static IReadOnlyList<CompositionProfileDefinition> All =>
    [
        SyntheticDpReplace,
        SyntheticCtrlRamReplace,
        SyntheticGeneralReplace,
    ];
}
