namespace NvtFwCombiner.Domain.Composition;

/// <summary>Stable address-space identifiers used by built-in composition profiles and workbench adapters.</summary>
public static class CompositionAddressSpaceIds
{
    /// <summary>Mutable output image address space.</summary>
    public const string OutputImage = "output-image";

    /// <summary>Immutable reference/base firmware address space used by Replace workflows.</summary>
    public const string ReferenceBase = "reference-base";

    /// <summary>Immutable DP input address space used by Standard Merge.</summary>
    public const string DpInput = "dp-input";

    /// <summary>Immutable TP input address space used by Standard Merge.</summary>
    public const string TpInput = "tp-input";

    /// <summary>Immutable LD input address space used by Standard Merge.</summary>
    public const string LdInput = "ld-input";

    /// <summary>Immutable complete DP A/B container used by AB Merge.</summary>
    public const string DpAbInput = "dp-ab-input";

    /// <summary>Immutable Touch A source used by AB Merge.</summary>
    public const string TpAInput = "tp-a-input";

    /// <summary>Immutable Touch B source used by AB Merge.</summary>
    public const string TpBInput = "tp-b-input";

    /// <summary>Immutable DP replacement address space used by DP Replace.</summary>
    public const string DpReplacement = "dp-replacement";

    /// <summary>Immutable LD replacement address space used by DP Replace.</summary>
    public const string LdReplacement = "ld-replacement";

    /// <summary>Immutable CtrlRAM replacement address space used by CtrlRAM Replace.</summary>
    public const string CtrlRamReplacement = "ctrlram-replacement";

    /// <summary>Prefix for dynamic CtrlRAM replacement input address spaces.</summary>
    public const string DynamicCtrlRamReplacementPrefix = "replace-ctrlram-";
}
