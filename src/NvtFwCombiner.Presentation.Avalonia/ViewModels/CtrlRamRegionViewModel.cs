namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Display row for an owner-provided CtrlRAM region from TP Overview.</summary>
internal sealed class CtrlRamRegionViewModel
{
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

    public string Name { get; }

    /// <summary>Inclusive TP position range from TP Overview.</summary>
    public string StartAddress { get; }

    public string SizeHex { get; }

    public bool IsDiffRegion { get; }
}
