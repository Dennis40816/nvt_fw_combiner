using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Canonical workflow identifiers used by compiled capability routes.</summary>
public static class IcWorkflowIds
{
    /// <summary>Profile-backed normal Standard Merge.</summary>
    public const string StandardMerge = ExperienceIds.StandardMerge;

    /// <summary>Owner-approved fixed-layout A/B Merge workflow.</summary>
    public const string AbMerge = ExperienceIds.AbMerge;

    /// <summary>DP Replace workflow.</summary>
    public const string DpReplace = ExperienceIds.DpReplace;

    /// <summary>CtrlRAM Replace workflow.</summary>
    public const string CtrlRamReplace = ExperienceIds.CtrlRamReplace;

    /// <summary>General Merge workflow.</summary>
    public const string GeneralMerge = ExperienceIds.GeneralMerge;

    /// <summary>General Replace workflow.</summary>
    public const string GeneralReplace = ExperienceIds.GeneralReplace;
}
