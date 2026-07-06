using System.ComponentModel;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Sets the selected source BIN file for a General Merge mapping row.</summary>
    public bool SetGeneralMergeMappingFile(string mappingId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mappingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        GeneralMergeMappingViewModel? mapping = GeneralMergeMappings.FirstOrDefault(row =>
            string.Equals(row.MappingId, mappingId, StringComparison.Ordinal));
        if (mapping is null)
        {
            return false;
        }

        mapping.FilePath = path;
        RefreshMemoryMapState();
        RefreshCommandState();
        return true;
    }

    /// <summary>Removes a General Merge mapping row from the UI list.</summary>
    public void RemoveGeneralMergeMappingRow(GeneralMergeMappingViewModel mapping)
    {
        RemoveGeneralMergeMapping(mapping);
    }

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

    private void RemoveGeneralMergeMapping(GeneralMergeMappingViewModel? mapping)
    {
        if (mapping is null || GeneralMergeMappings.Count <= 1)
        {
            return;
        }

        mapping.PropertyChanged -= GeneralMergeMappingPropertyChanged;
        _ = GeneralMergeMappings.Remove(mapping);
        for (int index = 0; index < GeneralMergeMappings.Count; index++)
        {
            GeneralMergeMappings[index].SetIndex(index + 1);
        }

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
