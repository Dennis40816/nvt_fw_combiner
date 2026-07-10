using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Uncommitted equal-length hexadecimal patch draft displayed by General Replace.</summary>
public sealed partial class GeneralReplacePatchDraftViewModel : ObservableObject
{
    /// <summary>Inclusive target start address.</summary>
    [ObservableProperty]
    public partial string StartAddress { get; set; } = "0x00000";

    /// <summary>Inclusive target end address.</summary>
    [ObservableProperty]
    public partial string EndAddress { get; set; } = "0x00000";

    /// <summary>Current patch operation mode.</summary>
    [ObservableProperty]
    public partial WorkbenchGeneralReplacePatchKind Kind { get; set; } = WorkbenchGeneralReplacePatchKind.Overwrite;

    /// <summary>Hexadecimal overwrite bytes or one fill byte.</summary>
    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;
}
