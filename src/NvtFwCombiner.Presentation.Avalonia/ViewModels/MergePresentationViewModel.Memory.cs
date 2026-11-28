using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MergePresentationViewModel
{
    private readonly Lock _memoryProjectionGate = new();

    internal void RefreshMergeMemoryMapState(bool refreshAuthoring = true)
    {
        PrepareMergeMemoryMapState(refreshAuthoring);
        OnPropertyChanged(nameof(MergeMemorySummary));
    }

    private void PrepareMergeMemoryMapState(bool refreshAuthoring = true)
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
            OutputLayout.Publish(
                _compositionServices, acceptedSession, Text,
                IsGeneralMergeModeSelected ? [] : MergeSlots,
                IsGeneralMergeModeSelected
                    ? MemoryPendingPrerequisite.GeneralMergeSourceMapping
                    : MemoryPendingPrerequisite.DpBin,
                IsGeneralMergeModeSelected ? _generalMergeAdmission : null,
                SelectedMergeMode == NormalMergeMode ? _appliedStandardMergeRequired : null);
        }
    }

    private void NotifyOutputLayoutChanged()
    {
        OnPropertyChanged(nameof(MergeMemoryRangeLabel));
        OnPropertyChanged(nameof(MergeMemoryRows));
        OnPropertyChanged(nameof(MergeCoverageSegments));
        OnPropertyChanged(nameof(MergeCoverageRows));
        OnPropertyChanged(nameof(MergeStartAddress));
        OnPropertyChanged(nameof(MergeEndAddress));
    }
}
