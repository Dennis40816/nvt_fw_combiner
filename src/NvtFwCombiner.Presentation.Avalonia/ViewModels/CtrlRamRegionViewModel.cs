namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Display row for an owner-provided CtrlRAM region from TP Overview.</summary>
public sealed class CtrlRamRegionViewModel
{
    /// <summary>Creates a CtrlRAM display row.</summary>
    public CtrlRamRegionViewModel(string name, string startAddress, string sizeHex, bool isDiffRegion = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(startAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(sizeHex);

        Name = name;
        StartAddress = startAddress;
        SizeHex = sizeHex;
        IsDiffRegion = isDiffRegion;
    }

    /// <summary>Region label from TP Overview.</summary>
    public string Name { get; }

    /// <summary>Flash start address from TP Overview.</summary>
    public string StartAddress { get; }

    /// <summary>Declared region size from TP Overview.</summary>
    public string SizeHex { get; }

    /// <summary>True when the row represents DIFF/DLM content hidden for single by default.</summary>
    public bool IsDiffRegion { get; }
}
