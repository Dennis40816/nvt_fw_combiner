using System.ComponentModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private GeneralMappingDraftState? _acceptedGeneralReplaceDraft;

    private void AddGeneralReplaceMapping()
    {
        _acceptedGeneralReplaceDraft = null;
        _generalReplaceMappingCounter++;
        var mapping = new GeneralReplaceMappingViewModel(
            $"general-map-{_generalReplaceMappingCounter}",
            GeneralReplaceMappings.Count + 1);
        mapping.PropertyChanged += GeneralReplaceMappingPropertyChanged;
        GeneralReplaceMappings.Add(mapping);
        RefreshCommandState();
    }

    private IReadOnlyList<WorkbenchGeneralReplaceMappingInput> CreateGeneralReplaceMappingInputs()
    {
        return
        [
            .. GeneralReplaceMappings
                .Where(mapping => mapping.HasFile)
                .Select(mapping => new WorkbenchGeneralReplaceMappingInput(
                    mapping.MappingId,
                    mapping.FilePath!,
                    mapping.StartAddress,
                    mapping.EndAddress)),
        ];
    }

    private void ShowHexEditor()
    {
        _ = HexEditorWorkspace;
        OnPropertyChanged(nameof(LoadedHexEditorWorkspace));
        NavigateToPage(ShellPage.HexEditor);
    }

    private void GeneralReplaceMappingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _acceptedGeneralReplaceDraft = null;
        RefreshCommandState();
    }
}
