using System.Collections.ObjectModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MergePresentationViewModel
{
    internal void RefreshMergeMemoryMapState()
    {
        if (IsGeneralMergeModeSelected)
        {
            RefreshGeneralMergeAuthoringState();
        }

        long? selectedMergeDpInputLength = GetSelectedMergeDpInputLength();
        long? selectedAbMergeDpInputLength = GetSelectedAbMergeDpInputLength();
        (
            string rangeLabel,
            IReadOnlyList<MemoryMapRowViewModel> rows,
            IReadOnlyList<MemoryCoverageSegmentViewModel> coverageSegments) = SelectedMergeMode switch
            {
                GeneralMergeMode => GetGeneralMergeMemoryDisplay(),
                AbCodeMergeMode => UiCompositionRunner.GetAbMergeMemoryDisplay(
                    _compositionServices,
                    SelectedIc,
                    GetSelectedAbMergeTopologyToken(),
                    selectedAbMergeDpInputLength),
                _ => UiCompositionRunner.GetStandardMergeMemoryDisplay(
                    _compositionServices,
                    SelectedIc,
                    selectedMergeDpInputLength),
            };
        MergeMemoryRangeLabel = rangeLabel;
        ReplaceRows(MergeMemoryRows, rows);
        ReplaceRows(MergeCoverageSegments, coverageSegments);

        OnPropertyChanged(nameof(MergeMemoryRangeLabel));
        OnPropertyChanged(nameof(MergeMemorySummary));
    }

    private (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetGeneralMergeMemoryDisplay()
    {
        return TryResolveGeneralMergeOutputInitializer(out WorkbenchGeneralMergeInitializer? initializer)
            ? UiCompositionRunner.GetGeneralMergeMemoryDisplay(
                _compositionServices,
                SelectedIc,
                initializer!,
                _generalMergeAuthoringStates,
                _generalMergeAdmission)
            : UiCompositionRunner.GetGeneralMergeMemoryDisplay(
                _compositionServices,
                SelectedIc,
                GeneralMergeOutputLength,
                GeneralMergeOutputFillByte);
    }

    private long? GetSelectedMergeDpInputLength()
    {
        return _compositionServices.Capabilities.IsDpPerspectiveIc(SelectedIc) &&
            _stateBindings.GetInspectedFileLength(MergeDpSlot) is long length
                ? length
                : null;
    }

    private long? GetSelectedAbMergeDpInputLength()
    {
        WorkbenchAbMergeInputSlot? dpInput = _compositionServices.Authoring
            .GetAbMergeInputSlots(SelectedIc, GetSelectedAbMergeTopologyToken())
            .SingleOrDefault(static input => input.Role == WorkbenchAbMergeInputRole.DpAb);
        return dpInput is not null &&
            _abMergeSlotsByAddressSpace.TryGetValue(dpInput.AddressSpaceId, out FirmwareSlotViewModel? slot) &&
            _stateBindings.GetInspectedFileLength(slot) is long length
                ? length
                : null;
    }

    private static void ReplaceRows<T>(ObservableCollection<T> target, IEnumerable<T> rows)
    {
        target.Clear();
        foreach (T row in rows)
        {
            target.Add(row);
        }
    }
}
