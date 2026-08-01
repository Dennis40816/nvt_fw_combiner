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

    /// <summary>Immutable LDC input address space used by Standard Merge.</summary>
    public const string LdcInput = "ldc-input";

    /// <summary>Immutable complete DP A/B container used by AB Merge.</summary>
    public const string DpAbInput = "dp-ab-input";

    /// <summary>Immutable Touch A source used by AB Merge.</summary>
    public const string TpAInput = "tp-a-input";

    /// <summary>Immutable Touch B source used by AB Merge.</summary>
    public const string TpBInput = "tp-b-input";

    /// <summary>Host-owned mutable Touch B relocation buffer used by AB Merge.</summary>
    public const string TpBWork = "tp-b-work";

    /// <summary>Immutable DP replacement address space used by DP Replace.</summary>
    public const string DpReplacement = "dp-replacement";

    /// <summary>Immutable Initial Code replacement address space used when DP and LDC are independently selectable.</summary>
    public const string InitialCodeReplacement = "initial-code-replacement";

    /// <summary>Immutable LDC replacement address space used by DP Replace.</summary>
    public const string LdcReplacement = "ldc-replacement";

    /// <summary>Immutable CtrlRAM replacement address space used by CtrlRAM Replace.</summary>
    public const string CtrlRamReplacement = "ctrlram-replacement";

    /// <summary>Prefix for dynamic CtrlRAM replacement input address spaces.</summary>
    public const string DynamicCtrlRamReplacementPrefix = "replace-ctrlram-";
}
