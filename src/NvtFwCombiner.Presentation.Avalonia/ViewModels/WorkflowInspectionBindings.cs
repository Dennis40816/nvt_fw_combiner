namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Explicit page callbacks consumed by the shared firmware-inspection presentation child.</summary>
internal sealed record WorkflowInspectionBindings(
    Action EnsureWorkflowLoaded,
    Func<bool> IsWorkflowLoaded,
    Func<bool> IsCtrlRamReplaceModeSelected,
    Func<bool> IsDpReplaceContext,
    Func<bool> IsNumberSelectorVisible,
    Func<bool> IsAbCodeMergeModeSelected,
    Func<bool> HasAbMergeTopologyChoices,
    Func<string> SelectedMergeMode,
    Func<string> SelectedReplaceMode,
    Func<FirmwareSlotViewModel> MergeDpSlot,
    Func<FirmwareSlotViewModel> MergeTpSlot,
    Func<FirmwareSlotViewModel> ReplaceBaseSlot,
    Func<IEnumerable<FirmwareSlotViewModel>> MergeSlots,
    Func<IEnumerable<FirmwareSlotViewModel>> ReplaceSlots,
    Func<IEnumerable<FirmwareSlotViewModel>> AbMergeSlots,
    Func<IReadOnlyDictionary<string, string>> AbMergeAddressSpaceBySlotId,
    Func<string?> SelectedAbMergeTopologyToken,
    Func<string, string, FirmwareSlotViewModel?> SelectSlotFile,
    Func<string, FirmwareSlotViewModel?> FindSlot,
    Action<NvtFwCombiner.Bootstrap.WorkbenchCtrlRamInspectionDisplay> ApplyCtrlRamDisplay,
    Action RefreshMergeMemoryMap,
    Action RefreshReplaceMemoryMap,
    Action RefreshCommandState,
    Action NotifySlotFileOutputNames);
