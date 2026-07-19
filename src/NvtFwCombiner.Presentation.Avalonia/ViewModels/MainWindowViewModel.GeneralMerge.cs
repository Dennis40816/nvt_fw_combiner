using System.ComponentModel;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void AddGeneralMergeMapping()
    {
        _generalMergeMappingCounter++;
        var mapping = new GeneralMergeMappingViewModel(
            $"general-merge-map-{_generalMergeMappingCounter}",
            GeneralMergeMappings.Count + 1);
        mapping.PropertyChanged += GeneralMergeMappingPropertyChanged;
        GeneralMergeMappings.Add(mapping);
        RefreshMemoryMapState();
        RefreshCommandState();
    }

    private IReadOnlyList<WorkbenchGeneralMergeMappingInput> CreateGeneralMergeMappingInputs()
    {
        return
        [
            .. GeneralMergeMappings
                .Where(mapping => mapping.HasFile)
                .Select(mapping => new WorkbenchGeneralMergeMappingInput(
                    mapping.MappingId,
                    mapping.FilePath!,
                    mapping.SourceStartAddress,
                    mapping.TargetStartAddress,
                    mapping.Length)),
        ];
    }

    private void GeneralMergeMappingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GeneralMergeMappingViewModel.SourceStartAddress) or
            nameof(GeneralMergeMappingViewModel.TargetStartAddress) or
            nameof(GeneralMergeMappingViewModel.Length) or
            nameof(GeneralMergeMappingViewModel.FilePath))
        {
            RefreshMemoryMapState();
            ResetRunResultForContextChange();
            RefreshCommandState();
        }
    }
}
