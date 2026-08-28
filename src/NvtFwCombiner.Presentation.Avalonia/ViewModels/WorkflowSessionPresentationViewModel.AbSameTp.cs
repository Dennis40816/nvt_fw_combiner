using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
    /// <summary>Selects one immutable TP source into both independent AB logical bindings.</summary>
    internal async Task SetAbSameTpFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!IsWorkflowLoaded)
        {
            EnsureWorkflowLoaded();
            RefreshContextState();
        }

        if (ActiveInspectionContext is not { IsAbMerge: true } context ||
            !_merge.UseSameTpForAbMerge ||
            !_merge.AbMergeSlotsByAddressSpace.TryGetValue(
                CompositionAddressSpaceIds.TpAInput,
                out FirmwareSlotViewModel? tpA) ||
            !_merge.AbMergeSlotsByAddressSpace.TryGetValue(
                CompositionAddressSpaceIds.TpBInput,
                out FirmwareSlotViewModel? tpB))
        {
            return;
        }

        FirmwareSlotViewModel? selectedA = SelectSlotFile(context, tpA.SlotId, path);
        FirmwareSlotViewModel? selectedB = SelectSlotFile(context, tpB.SlotId, path);
        if (selectedA is null || selectedB is null)
        {
            return;
        }

        await RefreshSelectedMergeFirmwareInspectionsAsync(selectedA.SlotId, cancellationToken);
        RecordInputSelected(context, selectedA.SlotId);
        RecordInputSelected(context, selectedB.SlotId);
    }
}
