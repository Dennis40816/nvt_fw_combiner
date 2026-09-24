using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
    internal bool IsAbDummyDpTransitionInProgress { get; private set; }

    internal async Task SetAbDummyDpModeAsync(bool enabled, CancellationToken cancellationToken)
    {
        if (!_merge.CanChangeAbDummyDp || ActiveInspectionContext is not { IsAbMerge: true } context)
        {
            return;
        }
        IsAbDummyDpTransitionInProgress = true;
        try
        {
            _merge.NotifyAbDummyDpCommandStateChanged();
            InvalidateFirmwareInspection(context.Owner, clearBaseProjection: false);
            InvalidateFirmwareIcMismatch();
            InvalidateFirmwareNumberMismatch();
            if (enabled && _merge.AbMergeSlotsByAddressSpace.TryGetValue(
                    CompositionAddressSpaceIds.DpAbInput, out FirmwareSlotViewModel? dp))
            {
                ClearFirmwareSlot(dp);
            }
            _merge.ApplyAbDummyDpMode(enabled);
            _merge.RefreshMergeMemoryMapState();
            NotifySlotFileOutputNames();
            ResetRunResults(context.Owner, ExperienceIds.AbMerge);
            _stateBindings.RefreshCommandState();
            await RefreshSelectedMergeFirmwareInspectionsAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            IsAbDummyDpTransitionInProgress = false;
            _merge.NotifyAbDummyDpCommandStateChanged();
        }
    }
}
