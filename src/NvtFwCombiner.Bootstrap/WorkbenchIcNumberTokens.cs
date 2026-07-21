using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>IC number selector tokens exposed through Bootstrap for UI and CLI adapters.</summary>
public static class WorkbenchIcNumberTokens
{
    /// <summary>Single-chip selector token.</summary>
    public const string SingleChip = IcNumberSelectionTokens.SingleChip;

    /// <summary>Cascade selector token.</summary>
    public const string Cascade = IcNumberSelectionTokens.Cascade;

    /// <summary>NT51930's currently supported multi-chip count-range selector.</summary>
    public const string CascadeTwoToThirteen = "cascade_2to13";

    /// <summary>Returns true when the value is the stable single-chip selector token.</summary>
    public static bool IsSingle(string? value)
    {
        return IcNumberSelectionTokens.IsSingle(value);
    }
}
