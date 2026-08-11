using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MergePresentationViewModel
{
    private readonly Lock _memoryProjectionGate = new();

    internal void RefreshMergeMemoryMapState(bool refreshAuthoring = true)
    {
        lock (_memoryProjectionGate)
        {
            if (refreshAuthoring && IsGeneralMergeModeSelected)
            {
                RefreshGeneralMergeAuthoringState();
            }

            ActiveSessionSnapshot? acceptedSession = SelectedMergeMode switch
            {
                GeneralMergeMode => _generalMergeSession.CurrentSnapshot,
                AbCodeMergeMode => _abMergeSession.CurrentSnapshot,
                _ => _standardMergeSession.CurrentSnapshot,
            };
            (
                string rangeLabel,
                IReadOnlyList<MemoryMapRowViewModel> rows,
                IReadOnlyList<MemoryCoverageSegmentViewModel> coverageSegments) =
                    acceptedSession?.ExactCapability is null
                    ? UiCompositionRunner.GetPendingMemoryDisplay(
                        "Select and inspect the required inputs to resolve the compiled memory layout.")
                    : UiCompositionRunner.GetMemoryDisplay(
                        _compositionServices,
                        acceptedSession,
                        Text,
                        IsGeneralMergeModeSelected ? _generalMergeAdmission : null);
            MergeMemoryRangeLabel = rangeLabel;
            ReplaceRows(MergeMemoryRows, rows);
            ReplaceRows(MergeCoverageSegments, coverageSegments);

            OnPropertyChanged(nameof(MergeMemoryRangeLabel));
            OnPropertyChanged(nameof(MergeMemorySummary));
        }
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
