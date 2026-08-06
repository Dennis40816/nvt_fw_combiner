using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Workflow identifiers exposed through the Bootstrap facade for UI adapters.</summary>
public static class WorkbenchWorkflowIds
{
    /// <summary>Standard Merge workflow id.</summary>
    public const string StandardMerge = ExperienceIds.StandardMerge;

    /// <summary>AB Code Merge workflow id.</summary>
    public const string AbMerge = ExperienceIds.AbMerge;

    /// <summary>General Merge workflow id.</summary>
    public const string GeneralMerge = ExperienceIds.GeneralMerge;

    /// <summary>DP Replace workflow id.</summary>
    public const string DpReplace = ExperienceIds.DpReplace;

    /// <summary>CtrlRAM Replace workflow id.</summary>
    public const string CtrlRamReplace = ExperienceIds.CtrlRamReplace;

    /// <summary>General Replace workflow id.</summary>
    public const string GeneralReplace = ExperienceIds.GeneralReplace;
}
