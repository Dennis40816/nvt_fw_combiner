using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Address-space identifiers exposed through Bootstrap for UI adapters.</summary>
public static class WorkbenchAddressSpaceIds
{
    /// <summary>Mutable output image address space.</summary>
    public const string OutputImage = CompositionAddressSpaceIds.OutputImage;

    /// <summary>Immutable reference/base firmware address space used by Replace workflows.</summary>
    public const string ReferenceBase = CompositionAddressSpaceIds.ReferenceBase;

    /// <summary>Immutable DP input address space used by Standard Merge.</summary>
    public const string DpInput = CompositionAddressSpaceIds.DpInput;

    /// <summary>Immutable TP input address space used by Standard Merge.</summary>
    public const string TpInput = CompositionAddressSpaceIds.TpInput;

    /// <summary>Immutable LDC input address space used by Standard Merge.</summary>
    public const string LdcInput = CompositionAddressSpaceIds.LdcInput;

    /// <summary>Immutable DP replacement address space used by DP Replace.</summary>
    public const string DpReplacement = CompositionAddressSpaceIds.DpReplacement;

    /// <summary>Immutable LDC replacement address space used by DP Replace.</summary>
    public const string LdcReplacement = CompositionAddressSpaceIds.LdcReplacement;

    /// <summary>Immutable CtrlRAM replacement address space used by CtrlRAM Replace.</summary>
    public const string CtrlRamReplacement = CompositionAddressSpaceIds.CtrlRamReplacement;

    /// <summary>Prefix for dynamic CtrlRAM replacement input address spaces.</summary>
    public const string DynamicCtrlRamReplacementPrefix = CompositionAddressSpaceIds.DynamicCtrlRamReplacementPrefix;
}
