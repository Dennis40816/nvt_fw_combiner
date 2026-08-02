using System.ComponentModel;
using System.Collections.ObjectModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Removes a General mapping row while preserving its typed mapping list.</summary>
    public void RemoveGeneralMappingRow(GeneralMappingRowViewModel mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        if (mapping is GeneralMergeMappingViewModel merge)
        {
            _ = Merge.RemoveGeneralMapping(merge);
            return;
        }

        bool removed = mapping switch
        {
            GeneralReplaceMappingViewModel replace when GeneralReplaceMappings.Count > 1 =>
                RemoveGeneralMapping(GeneralReplaceMappings, replace, GeneralReplaceMappingPropertyChanged),
            _ => false,
        };
        if (!removed)
        {
            return;
        }

        IEnumerable<GeneralMappingRowViewModel> remaining = GeneralReplaceMappings;
        int index = 1;
        foreach (GeneralMappingRowViewModel row in remaining)
        {
            row.SetIndex(index++);
        }

        _acceptedGeneralReplaceDraft = null;
        RefreshCommandState();
    }

    private bool TrySetGeneralMappingFile(string mappingId, string path)
    {
        if (Merge.TrySetGeneralMappingFile(mappingId, path))
        {
            return true;
        }

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

    private static bool RemoveGeneralMapping(
        ObservableCollection<GeneralReplaceMappingViewModel> mappings,
        GeneralReplaceMappingViewModel mapping,
        PropertyChangedEventHandler propertyChangedHandler)
    {
        mapping.PropertyChanged -= propertyChangedHandler;
        return mappings.Remove(mapping);
    }
}
