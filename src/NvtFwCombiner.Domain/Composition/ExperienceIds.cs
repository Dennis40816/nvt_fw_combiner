namespace NvtFwCombiner.Domain.Composition;

/// <summary>Stable identifiers for approved composition experiences.</summary>
public static class ExperienceIds
{
    /// <summary>Fixed merge workflow for standard firmware composition.</summary>
    public const string StandardMerge = "standard-merge";

    /// <summary>Fixed merge workflow for A/B firmware composition.</summary>
    public const string AbMerge = "ab-merge";

    /// <summary>Advanced merge workflow using explicit mappings over a blank image.</summary>
    public const string GeneralMerge = "general-merge";

    /// <summary>DP replace workflow over a reference image.</summary>
    public const string DpReplace = "dp-replace";

    /// <summary>CtrlRAM replace workflow over a reference image.</summary>
    public const string CtrlRamReplace = "ctrlram-replace";

    /// <summary>Advanced replace workflow using explicit mappings over a reference image.</summary>
    public const string GeneralReplace = "general-replace";
}
