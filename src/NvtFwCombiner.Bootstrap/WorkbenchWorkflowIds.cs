using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Workflow identifiers exposed through the Bootstrap facade for UI adapters.</summary>
public static class WorkbenchWorkflowIds
{
    /// <summary>Standard Merge workflow id.</summary>
    public const string StandardMerge = IcWorkflowIds.StandardMerge;

    /// <summary>General Merge workflow id.</summary>
    public const string GeneralMerge = IcWorkflowIds.GeneralMerge;

    /// <summary>DP Replace workflow id.</summary>
    public const string DpReplace = IcWorkflowIds.DpReplace;

    /// <summary>CtrlRAM Replace workflow id.</summary>
    public const string CtrlRamReplace = IcWorkflowIds.CtrlRamReplace;

    /// <summary>General Replace workflow id.</summary>
    public const string GeneralReplace = IcWorkflowIds.GeneralReplace;
}
