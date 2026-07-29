using System.ComponentModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private GeneralMappingDraftState? _acceptedGeneralMergeDraft;

    private void AddGeneralMergeMapping()
    {
        _acceptedGeneralMergeDraft = null;
        _generalMergeMappingCounter++;
        var mapping = new GeneralMergeMappingViewModel(
            $"general-merge-map-{_generalMergeMappingCounter}",
            GeneralMergeMappings.Count + 1);
        mapping.PropertyChanged += GeneralMergeMappingPropertyChanged;
        GeneralMergeMappings.Add(mapping);
        RefreshMergeMemoryMapState();
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
            _acceptedGeneralMergeDraft = null;
            RefreshMergeMemoryMapState();
            ResetRunResultForContextChange();
            RefreshCommandState();
        }
    }
}
