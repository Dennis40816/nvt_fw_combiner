using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Typed General Merge source-to-target fields for one shared mapping row.</summary>
public sealed partial class GeneralMergeMappingViewModel : GeneralMappingRowViewModel
{
    /// <summary>Creates a mapping row.</summary>
    public GeneralMergeMappingViewModel(string mappingId, int index)
        : base(mappingId, index, "No source BIN selected")
    {
    }

    /// <summary>Source start address text inside the selected source BIN.</summary>
    [ObservableProperty]
    public partial string SourceStartAddress { get; set; } = "0x00000";

    /// <summary>Target start address text inside the output image.</summary>
    [ObservableProperty]
    public partial string TargetStartAddress { get; set; } = "0x00000";

    /// <summary>Byte length text copied from source to target.</summary>
    [ObservableProperty]
    public partial string Length { get; set; } = "0x00000";

}
