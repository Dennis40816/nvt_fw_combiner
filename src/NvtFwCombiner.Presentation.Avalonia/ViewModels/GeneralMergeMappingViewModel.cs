namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Typed General Merge source-to-target fields for one shared mapping row.</summary>
public sealed class GeneralMergeMappingViewModel : GeneralMappingRowViewModel
{
    /// <summary>Creates a mapping row.</summary>
    public GeneralMergeMappingViewModel(
        string mappingId,
        int index,
        ShellTextResources? text = null)
        : base(mappingId, index, "No source BIN selected", text)
    {
    }

}
