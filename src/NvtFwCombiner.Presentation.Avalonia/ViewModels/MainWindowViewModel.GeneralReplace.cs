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
    }

    /// <summary>Removes a General Replace mapping row from the editable UI list.</summary>
    public void RemoveGeneralReplaceMappingRow(GeneralReplaceMappingViewModel mapping)
    {
        RemoveGeneralReplaceMapping(mapping);
    }

    private void AddGeneralReplaceMapping()
    {
        _generalReplaceMappingCounter++;
        GeneralReplaceMappings.Add(new GeneralReplaceMappingViewModel(
            $"general-map-{_generalReplaceMappingCounter}",
            GeneralReplaceMappings.Count + 1));
    }

    private void RemoveGeneralReplaceMapping(GeneralReplaceMappingViewModel? mapping)
    {
        if (mapping is null || GeneralReplaceMappings.Count <= 1)
        {
            return;
        }

        _ = GeneralReplaceMappings.Remove(mapping);
        for (int index = 0; index < GeneralReplaceMappings.Count; index++)
        {
            GeneralReplaceMappings[index].SetIndex(index + 1);
        }
    }
}
