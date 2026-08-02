using System.ComponentModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class ReplacePresentationViewModel
{
    private GeneralMappingDraftState? _acceptedGeneralReplaceDraft;

    internal void AddGeneralReplaceMapping()
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

    internal bool TrySetGeneralMappingFile(string mappingId, string path)
    {
        GeneralMappingRowViewModel? mapping = GeneralReplaceMappings.FirstOrDefault(row =>
            string.Equals(row.MappingId, mappingId, StringComparison.Ordinal));
        if (mapping is null)
        {
            return false;
        }

        _acceptedGeneralReplaceDraft = null;
        mapping.FilePath = path;
        RefreshCommandState();
        return true;
    }

    internal bool RemoveGeneralMapping(GeneralReplaceMappingViewModel mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        if (GeneralReplaceMappings.Count <= 1)
        {
            return false;
        }

        mapping.PropertyChanged -= GeneralReplaceMappingPropertyChanged;
        if (!GeneralReplaceMappings.Remove(mapping))
        {
            return false;
        }

        int index = 1;
        foreach (GeneralMappingRowViewModel row in GeneralReplaceMappings)
        {
            row.SetIndex(index++);
        }

        _acceptedGeneralReplaceDraft = null;
        RefreshCommandState();
        return true;
    }

    private void GeneralReplaceMappingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _acceptedGeneralReplaceDraft = null;
        RefreshCommandState();
    }
}
