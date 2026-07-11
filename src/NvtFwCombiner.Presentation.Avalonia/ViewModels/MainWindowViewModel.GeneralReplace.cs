using System.ComponentModel;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Sets a local file path for a General Replace mapping row.</summary>
    public void SetGeneralReplaceMappingFile(string mappingId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mappingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        GeneralReplaceMappingViewModel? mapping = GeneralReplaceMappings.FirstOrDefault(row =>
            string.Equals(row.MappingId, mappingId, StringComparison.Ordinal));
        if (mapping is null)
        {
            return;
        }

        mapping.FilePath = path;
        RefreshCommandState();
    }

    /// <summary>Removes a General Replace mapping row from the editable UI list.</summary>
    public void RemoveGeneralReplaceMappingRow(GeneralReplaceMappingViewModel mapping)
    {
        RemoveGeneralReplaceMapping(mapping);
    }

    private void AddGeneralReplaceMapping()
    {
        _generalReplaceMappingCounter++;
        var mapping = new GeneralReplaceMappingViewModel(
            $"general-map-{_generalReplaceMappingCounter}",
            GeneralReplaceMappings.Count + 1);
        mapping.PropertyChanged += GeneralReplaceMappingPropertyChanged;
        GeneralReplaceMappings.Add(mapping);
        RefreshCommandState();
    }

    private void RemoveGeneralReplaceMapping(GeneralReplaceMappingViewModel? mapping)
    {
        if (mapping is null || GeneralReplaceMappings.Count <= 1)
        {
            return;
        }

        mapping.PropertyChanged -= GeneralReplaceMappingPropertyChanged;
        _ = GeneralReplaceMappings.Remove(mapping);
        for (int index = 0; index < GeneralReplaceMappings.Count; index++)
        {
            GeneralReplaceMappings[index].SetIndex(index + 1);
        }

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
        SetSelectedPage(ShellPage.HexEditor);
    }

    private void GeneralReplaceMappingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshCommandState();
    }
}
