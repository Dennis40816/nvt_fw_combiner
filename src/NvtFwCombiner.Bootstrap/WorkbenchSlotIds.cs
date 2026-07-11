using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Stable slot identifiers used by CLI and UI workbench adapters.</summary>
public static class WorkbenchSlotIds
{
    /// <summary>Workbench DP input slot used by Standard Merge.</summary>
    public const string MergeDp = "merge-dp";

    /// <summary>Workbench TP input slot used by Standard Merge.</summary>
    public const string MergeTp = "merge-tp";

    /// <summary>Workbench LD input slot used by Standard Merge.</summary>
    public const string MergeLd = "merge-ld";

    /// <summary>Workbench base/reference image slot used by Replace workflows.</summary>
    public const string ReplaceBase = "replace-base";

    /// <summary>Workbench DP replacement slot used by DP Replace.</summary>
    public const string ReplaceDp = "replace-dp";

    /// <summary>Prefix for dynamic workbench CtrlRAM replacement slots.</summary>
    public const string ReplaceCtrlRamPrefix = CompositionAddressSpaceIds.DynamicCtrlRamReplacementPrefix;

    /// <summary>Creates a dynamic workbench CtrlRAM replacement slot id from a profile region id.</summary>
    public static string CreateReplaceCtrlRam(string regionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        return string.Concat(ReplaceCtrlRamPrefix, regionId);
    }

    /// <summary>Formats a dynamic CtrlRAM replacement slot id into a human-readable report label.</summary>
    public static bool TryFormatReplaceCtrlRamLabel(string slotId, out string label)
    {
        return DynamicCtrlRamReplacementIds.TryFormatDisplayLabel(slotId, out label);
    }
}
