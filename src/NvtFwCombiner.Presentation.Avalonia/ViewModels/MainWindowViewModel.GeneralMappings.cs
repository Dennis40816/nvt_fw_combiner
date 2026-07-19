using System.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Removes a General mapping row while preserving its typed mapping list.</summary>
    public void RemoveGeneralMappingRow(GeneralMappingRowViewModel mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        bool removed = mapping switch
        {
            GeneralMergeMappingViewModel merge when GeneralMergeMappings.Count > 1 =>
                RemoveGeneralMapping(GeneralMergeMappings, merge, GeneralMergeMappingPropertyChanged),
            GeneralReplaceMappingViewModel replace when GeneralReplaceMappings.Count > 1 =>
                RemoveGeneralMapping(GeneralReplaceMappings, replace, GeneralReplaceMappingPropertyChanged),
            _ => false,
        };
        if (!removed)
        {
            return;
        }

        IEnumerable<GeneralMappingRowViewModel> remaining = mapping is GeneralMergeMappingViewModel
            ? GeneralMergeMappings
            : GeneralReplaceMappings;
        int index = 1;
        foreach (GeneralMappingRowViewModel row in remaining)
        {
            row.SetIndex(index++);
        }

        if (mapping is GeneralMergeMappingViewModel)
        {
            RefreshMemoryMapState();
        }
        RefreshCommandState();
    }

    private bool TrySetGeneralMappingFile(string mappingId, string path)
    {
        GeneralMappingRowViewModel? mapping = GeneralMergeMappings
            .Cast<GeneralMappingRowViewModel>()
            .Concat(GeneralReplaceMappings)
            .FirstOrDefault(row => string.Equals(row.MappingId, mappingId, StringComparison.Ordinal));
        if (mapping is null)
        {
            return false;
        }

        mapping.FilePath = path;
        if (mapping is GeneralMergeMappingViewModel)
        {
            RefreshMemoryMapState();
        }
        RefreshCommandState();
        return true;
    }

    private static bool RemoveGeneralMapping<T>(
        ICollection<T> mappings,
        T mapping,
        PropertyChangedEventHandler propertyChangedHandler)
        where T : GeneralMappingRowViewModel
    {
        mapping.PropertyChanged -= propertyChangedHandler;
        return mappings.Remove(mapping);
    }
}
