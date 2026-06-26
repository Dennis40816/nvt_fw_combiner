namespace NvtFwCombiner.Domain.Composition;

/// <summary>Names the operator audience allowed to author an experience.</summary>
public enum AudienceKind
{
    /// <summary>System-owned workflow with fixed policy.</summary>
    System,

    /// <summary>Display firmware operator workflow.</summary>
    Display,

    /// <summary>Touch-panel hardware operator workflow.</summary>
    TpHardware,

    /// <summary>Touch-panel firmware operator workflow.</summary>
    TpFirmware,

    /// <summary>Advanced workflow for explicit mappings.</summary>
    Advanced,
}
