namespace NvtFwCombiner.Domain.Composition;

/// <summary>Names the operator audience allowed to author an experience.</summary>
public enum AudienceKind
{
    /// <summary>System-owned workflow with fixed policy.</summary>
    System,

    /// <summary>DP replace operator workflow.</summary>
    Dp,

    /// <summary>CtrlRAM replace operator workflow.</summary>
    CtrlRam,

    /// <summary>Advanced workflow for explicit mappings.</summary>
    Advanced,
}
