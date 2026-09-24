using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
    public void RemoveGeneralMappingRow(GeneralMappingRowViewModel mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        if (mapping is GeneralMergeMappingViewModel merge)
        {
            if (_merge.RemoveGeneralMapping(merge))
            {
                InvalidatePickerSelection(mapping);
            }
            return;
        }

        bool removed = mapping switch
        {
            GeneralReplaceMappingViewModel replace => _replace.RemoveGeneralMapping(replace),
            _ => false,
        };
        if (removed)
        {
            InvalidatePickerSelection(mapping);
        }
    }

    internal bool HasSelectedInputs(ShellPage page)
    {
        return page switch
        {
            ShellPage.Merge =>
                _merge.MergeDpSlot.HasFile ||
                _merge.MergeTpSlot.HasFile ||
                _merge.MergeLdcSlot.HasFile ||
                _merge.AbMergeSlotsByAddressSpace.Values.Any(static slot => slot.HasFile) ||
                _merge.MergeSlots.Any(static slot => slot.HasFile) ||
                _merge.GeneralMergeMappings.Any(static mapping => mapping.HasFile),
            ShellPage.Replace =>
                _replace.ReplaceBaseSlot.HasFile ||
                _replace.ReplaceSlots.Any(static slot => slot.HasFile) ||
                _replace.GeneralReplaceMappings.Any(static mapping => mapping.HasFile),
            ShellPage.Home or ShellPage.HexEditor => false,
            _ => false,
        };
    }

    internal void ClearSelectedInputs(ShellPage page)
    {
        if (page is not (ShellPage.Merge or ShellPage.Replace))
        {
            return;
        }

        InvalidateFirmwareInspection(
            page == ShellPage.Merge
                ? WorkflowInspectionOwner.Merge
                : WorkflowInspectionOwner.Replace,
            clearBaseProjection: page == ShellPage.Replace,
            clearSlotProjections: true);
        if (page == ShellPage.Replace)
        {
            _replace.InvalidateCtrlRamFirmwareVersionContextState();
        }
        InvalidateFirmwareIcMismatch();
        InvalidateFirmwareNumberMismatch();

        bool clearsActivePage = ActiveWorkflowOwner == (page == ShellPage.Merge
            ? WorkflowInspectionOwner.Merge
            : WorkflowInspectionOwner.Replace);
        if (page == ShellPage.Merge)
        {
            _merge.ClearStandardMergeAuthoringSelections();
            FirmwareSlotViewModel[] mergeSlots =
            [
                .. _merge.MergeSlots
                    .Concat(_merge.AbMergeSlotsByAddressSpace.Values)
                    .Concat([_merge.MergeDpSlot, _merge.MergeTpSlot, _merge.MergeLdcSlot])
                    .Distinct(),
            ];
            foreach (FirmwareSlotViewModel slot in mergeSlots)
            {
                InvalidatePickerSelection(slot);
                ClearFirmwareSlot(slot);
            }

            foreach (GeneralMergeMappingViewModel mapping in _merge.GeneralMergeMappings)
            {
                InvalidatePickerSelection(mapping);
            }
            _merge.ClearGeneralMergeMappingFilesWithoutRefresh();
            if (clearsActivePage)
            {
                _merge.RefreshMergeMemoryMapState();
            }
            else
            {
                _mergeWorkflowContextNeedsRefresh = true;
            }
        }
        else if (page == ShellPage.Replace)
        {
            FirmwareSlotViewModel[] replaceSlots =
            [
                .. _replace.ReplaceSlots
                    .Concat([_replace.ReplaceBaseSlot])
                    .Distinct(),
            ];
            foreach (FirmwareSlotViewModel slot in replaceSlots)
            {
                InvalidatePickerSelection(slot);
                ClearFirmwareSlot(slot);
            }

            foreach (GeneralReplaceMappingViewModel mapping in _replace.GeneralReplaceMappings)
            {
                InvalidatePickerSelection(mapping);
            }
            _replace.ClearGeneralReplaceMappingFilesWithoutRefresh();
            _replace.ClearCtrlRamInspectionDisplay();
            if (clearsActivePage)
            {
                _replace.RefreshReplaceMemoryMapState();
            }
            else
            {
                _replaceWorkflowContextNeedsRefresh = true;
            }
        }

        if (clearsActivePage)
        {
            NotifySlotFileOutputNames();
        }
        ResetRunResults(page == ShellPage.Merge ? WorkflowInspectionOwner.Merge : WorkflowInspectionOwner.Replace, allModes: true);
        _stateBindings.RefreshCommandState();
    }

    /// <summary>Clears one selected fixed-workflow slot without mutating its source file.</summary>
    public async Task ClearSlotFileAsync(
        string slotId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        if (!IsWorkflowLoaded || ActiveInspectionContext is not { } context)
        {
            return;
        }

        FirmwareSlotViewModel? slot = FindInspectionSlot(context, slotId);
        if (slot is null || !slot.HasFile)
        {
            return;
        }

        FirmwareSlotViewModel[] slotsToClear = ResolveSlotsToClear(context, slot);
        bool clearsBase = slotsToClear.Contains(_replace.ReplaceBaseSlot);
        if (context.IsReplace)
        {
            _replace.InvalidateCtrlRamFirmwareVersionContextState();
        }
        InvalidateFirmwareInspection(context.Owner, clearBaseProjection: clearsBase);
        InvalidateFirmwareIcMismatch();
        InvalidateFirmwareNumberMismatch();
        foreach (FirmwareSlotViewModel retained in InspectionSlots(context).Where(static item => item.HasFile))
        {
            retained.SetFirmwareFacts([]);
            retained.ClearInputInspection();
            retained.ClearCurrentInspectionProjection();
        }
        foreach (FirmwareSlotViewModel selected in slotsToClear)
        {
            InvalidatePickerSelection(selected);
            ClearFirmwareSlot(selected);
        }

        if (context.IsMerge)
        {
            _merge.RefreshMergeMemoryMapState();
        }
        else
        {
            if (clearsBase && context.IsCtrlRamReplace)
            {
                _replace.ClearCtrlRamBaseSelectionState();
            }
            else
            {
                _replace.RefreshReplaceMemoryMapState();
            }
        }

        NotifySlotFileOutputNames();
        ResetRunResults(context.Owner, context.Mode);
        _stateBindings.RefreshCommandState();
        Task refresh = context.IsMerge
            ? RefreshSelectedMergeFirmwareInspectionsAsync(cancellationToken: cancellationToken)
            : RefreshSelectedReplaceFirmwareInspectionsAsync(null, cancellationToken);
        await refresh;
    }

    private Task ClearSlotFileFromCommandAsync(string? slotId)
    {
        return string.IsNullOrWhiteSpace(slotId)
            ? Task.CompletedTask
            : ClearSlotFileAsync(slotId);
    }

    private FirmwareSlotViewModel[] ResolveSlotsToClear(
        WorkflowInspectionContext context,
        FirmwareSlotViewModel slot)
    {
        bool clearsLinkedPair = context.IsAbMerge && _merge.UseSameTpForAbMerge &&
            (_merge.MirrorsAbTpSelection(slot.SlotId) ||
                _merge.BlocksIndependentAbTpSelection(slot.SlotId));
        return clearsLinkedPair
            ?
            [
                .. _merge.AbMergeSlotsByAddressSpace
                    .Where(static pair => pair.Key is
                        CompositionAddressSpaceIds.TpAInput or CompositionAddressSpaceIds.TpBInput)
                    .Select(static pair => pair.Value)
                    .Distinct(),
            ]
            : [slot];
    }

    private FirmwareSlotViewModel? SelectSlotFile(
        WorkflowInspectionContext context,
        string slotId,
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        FirmwareSlotViewModel? slot = FindInspectionSlot(context, slotId);
        if (slot is null)
        {
            return null;
        }

        if (context.IsStandardMerge &&
            _merge.IsStandardMergeSlot(slot) &&
            !slot.CanSelectFile)
        {
            return null;
        }

        if (context.IsReplace)
        {
            _replace.InvalidateCtrlRamFirmwareVersionContextState();
        }
        InvalidateFirmwareInspection(
            context.Owner,
            clearBaseProjection: slot.SlotId == _replace.ReplaceBaseSlot.SlotId);
        InvalidateFirmwareIcMismatch();
        InvalidateFirmwareNumberMismatch();
        slot.FilePath = path;
        slot.SetFirmwareFacts([]);
        slot.ClearInputInspection();
        NotifySlotFileOutputNames();

        if (slot.SlotId == _replace.ReplaceBaseSlot.SlotId && context.IsCtrlRamReplace)
        {
            _replace.ClearCtrlRamInspectionDisplay();
        }
        else if ((context.IsStandardMerge && _merge.IsStandardMergeSlot(slot)) ||
            (context.IsAbMerge && _merge.AbMergeAddressSpaceBySlotId.ContainsKey(slot.SlotId)))
        {
            _merge.RefreshMergeMemoryMapState();
        }
        else if (slot.SlotId == _replace.ReplaceBaseSlot.SlotId)
        {
            _replace.RefreshReplaceMemoryMapState();
        }
        else if (slot.RegionId is not null)
        {
            _replace.RefreshReplaceMemoryMapState();
        }

        _stateBindings.RefreshCommandState();
        return slot;
    }

    private void NotifySlotFileOutputNames()
    {
        _merge.NotifyOutputFileNamesChanged();
        _replace.NotifyOutputFileNamesChanged();
    }

    private static void ClearFirmwareSlot(FirmwareSlotViewModel slot)
    {
        slot.FilePath = null;
        slot.SetFirmwareFacts([]);
        slot.ClearInputInspection();
        slot.ClearCurrentInspectionProjection();
    }
}
