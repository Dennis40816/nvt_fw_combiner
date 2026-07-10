using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One committed virtual hexadecimal patch ready for the shared General Replace request.</summary>
public sealed class GeneralReplacePatchViewModel
{
    /// <summary>Creates a committed patch row.</summary>
    public GeneralReplacePatchViewModel(
        string patchId,
        int index,
        string startAddress,
        string endAddress,
        WorkbenchGeneralReplacePatchKind kind,
        string value)
    {
        PatchId = patchId;
        Index = index;
        StartAddress = startAddress;
        EndAddress = endAddress;
        Kind = kind;
        Value = value;
    }

    /// <summary>Stable patch id used by the report and virtual artifact binding.</summary>
    public string PatchId { get; }

    /// <summary>One-based display order.</summary>
    public int Index { get; private set; }

    /// <summary>Inclusive target start address.</summary>
    public string StartAddress { get; }

    /// <summary>Inclusive target end address.</summary>
    public string EndAddress { get; }

    /// <summary>Requested equal-length patch operation.</summary>
    public WorkbenchGeneralReplacePatchKind Kind { get; }

    /// <summary>Hexadecimal input kept for the shared Bootstrap request.</summary>
    public string Value { get; }

    /// <summary>Compact target range display.</summary>
    public string RangeLabel => $"{StartAddress} - {EndAddress}";

    /// <summary>Operation display token.</summary>
    public string KindLabel => Kind.ToString();

    /// <summary>Updates display index after undo/redo operations.</summary>
    public void SetIndex(int index)
    {
        Index = index;
    }
}
