using System.ComponentModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MergePresentationViewModel
{
    internal GeneralMappingDraftState? AcceptedGeneralMergeDraft { get; set; }

    internal void AddGeneralMergeMapping()
    {
        AcceptedGeneralMergeDraft = null;
        _generalMergeMappingCounter++;
        var mapping = new GeneralMergeMappingViewModel(
            $"general-merge-map-{_generalMergeMappingCounter}",
            GeneralMergeMappings.Count + 1);
        mapping.PropertyChanged += GeneralMergeMappingPropertyChanged;
        GeneralMergeMappings.Add(mapping);
        RefreshMergeMemoryMapState();
        RefreshCommandState();
    }

    internal IReadOnlyList<WorkbenchGeneralMergeMappingInput> CreateGeneralMergeMappingInputs()
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

    internal bool RemoveGeneralMapping(GeneralMergeMappingViewModel mapping)
    {
        if (GeneralMergeMappings.Count <= 1)
        {
            return false;
        }

        mapping.PropertyChanged -= GeneralMergeMappingPropertyChanged;
        if (!GeneralMergeMappings.Remove(mapping))
        {
            return false;
        }

        int index = 1;
        foreach (GeneralMergeMappingViewModel row in GeneralMergeMappings)
        {
            row.SetIndex(index++);
        }

        AcceptedGeneralMergeDraft = null;
        RefreshMergeMemoryMapState();
        RefreshCommandState();
        return true;
    }

    internal bool TrySetGeneralMappingFile(string mappingId, string path)
    {
        GeneralMergeMappingViewModel? mapping = GeneralMergeMappings.FirstOrDefault(row =>
            string.Equals(row.MappingId, mappingId, StringComparison.Ordinal));
        if (mapping is null)
        {
            return false;
        }

        AcceptedGeneralMergeDraft = null;
        mapping.FilePath = path;
        RefreshMergeMemoryMapState();
        RefreshCommandState();
        return true;
    }

    private void GeneralMergeMappingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GeneralMergeMappingViewModel.SourceStartAddress) or
            nameof(GeneralMergeMappingViewModel.TargetStartAddress) or
            nameof(GeneralMergeMappingViewModel.Length) or
            nameof(GeneralMergeMappingViewModel.FilePath))
        {
            AcceptedGeneralMergeDraft = null;
            RefreshMergeMemoryMapState();
            _stateBindings.ResetRunResult();
            RefreshCommandState();
        }
    }
}
