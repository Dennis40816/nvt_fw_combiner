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

        _ = mapping switch
        {
            GeneralReplaceMappingViewModel replace => Replace.RemoveGeneralMapping(replace),
            _ => false,
        };
    }

    private bool TrySetGeneralMappingFile(string mappingId, string path)
    {
        return Merge.TrySetGeneralMappingFile(mappingId, path) ||
            Replace.TrySetGeneralMappingFile(mappingId, path);
    }
}
