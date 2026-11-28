using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MergePresentationViewModel
{
    private readonly Lock _memoryProjectionGate = new();

    internal void RefreshMergeMemoryMapState(bool refreshAuthoring = true)
    {
        PrepareMergeMemoryMapState(refreshAuthoring);
    }

    private void PrepareMergeMemoryMapState(bool refreshAuthoring = true)
    {
        lock (_memoryProjectionGate)
        {
            if (refreshAuthoring && IsGeneralMergeModeSelected)
            {
                RefreshGeneralMergeAuthoringState();
            }

            ActiveSessionSnapshot? acceptedSession = !HasSelectedIc ? null : SelectedMergeMode switch
            {
                GeneralMergeMode => _generalMergeSession.CurrentSnapshot,
                AbCodeMergeMode => _abMergeSession.CurrentSnapshot,
                _ => _standardMergeSession.CurrentSnapshot,
            };
            OutputLayout.Publish(
                _compositionServices, acceptedSession, Text,
                IsGeneralMergeModeSelected || !HasSelectedIc ? [] : MergeSlots,
                IsGeneralMergeModeSelected
                    ? MemoryPendingPrerequisite.GeneralMergeSourceMapping
                    : MemoryPendingPrerequisite.DpBin,
                IsGeneralMergeModeSelected ? _generalMergeAdmission : null,
                SelectedMergeMode == NormalMergeMode ? _appliedStandardMergeRequired : null,
                Text.GetMergeMemorySummary(SelectedMergeMode, IsStandardMergeSupported,
                    GeneralMergeMappings.Any(static mapping => mapping.HasFile)),
                IsGeneralMergeModeSelected ? BuildAvailability : null);
        }
    }
}
