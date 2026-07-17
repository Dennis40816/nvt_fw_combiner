using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Typed General Replace target-range fields for one shared mapping row.</summary>
public sealed partial class GeneralReplaceMappingViewModel : GeneralMappingRowViewModel
{
    /// <summary>Creates a mapping row.</summary>
    public GeneralReplaceMappingViewModel(string mappingId, int index)
        : base(mappingId, index, "No replacement BIN selected")
    {
    }

    /// <summary>Target start address text.</summary>
    [ObservableProperty]
    public partial string StartAddress { get; set; } = "0x00000";

    /// <summary>Target end address text.</summary>
    [ObservableProperty]
    public partial string EndAddress { get; set; } = "0x00000";

}
