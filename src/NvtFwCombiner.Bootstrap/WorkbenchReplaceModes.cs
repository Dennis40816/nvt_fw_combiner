namespace NvtFwCombiner.Bootstrap;

/// <summary>Workbench Replace mode ids surfaced by UI and CLI adapters.</summary>
public static class WorkbenchReplaceModes
{
    /// <summary>Display/DP replacement mode.</summary>
    public const string Dp = "DP";

    /// <summary>CtrlRAM replacement mode.</summary>
    public const string CtrlRam = "CtrlRAM";

    /// <summary>General explicit-range replacement mode.</summary>
    public const string General = "General";

    /// <summary>All workbench Replace modes in stable display order.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Dp,
        CtrlRam,
        General,
    ];
}
