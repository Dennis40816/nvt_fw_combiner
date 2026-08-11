namespace NvtFwCombiner.Application.Composition;

/// <summary>Stable client slot identifiers retained until every picker uses compiler slot ids directly.</summary>
public static class CompositionSlotIds
{
    /// <summary>DP input in the Standard Merge picker.</summary>
    public const string MergeDp = "merge-dp";

    /// <summary>TP input in the Standard Merge picker.</summary>
    public const string MergeTp = "merge-tp";

    /// <summary>LDC input in the Standard Merge picker.</summary>
    public const string MergeLdc = "merge-ldc";

    /// <summary>Reference input shared by Replace pickers.</summary>
    public const string ReplaceBase = "replace-base";

    /// <summary>DP input in the DP Replace picker.</summary>
    public const string ReplaceDp = "replace-dp";

    /// <summary>LDC input in the DP Replace picker.</summary>
    public const string ReplaceLdc = "replace-ldc";
}
