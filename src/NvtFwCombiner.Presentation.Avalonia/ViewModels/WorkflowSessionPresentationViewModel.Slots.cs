namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
    public void RemoveGeneralMappingRow(GeneralMappingRowViewModel mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        if (mapping is GeneralMergeMappingViewModel merge)
        {
            _ = _merge.RemoveGeneralMapping(merge);
            return;
        }

        _ = mapping switch
        {
            GeneralReplaceMappingViewModel replace => _replace.RemoveGeneralMapping(replace),
            _ => false,
        };
    }

    internal bool HasSelectedInputs(ShellPage page)
    {
        return page switch
        {
            ShellPage.Merge =>
                _merge.MergeDpSlot.HasFile ||
                _merge.MergeTpSlot.HasFile ||
                _merge.MergeLdcSlot.HasFile ||
                _merge.AbMergeSlots.Any(static slot => slot.HasFile) ||
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

        if (page == ShellPage.Merge)
        {
            _merge.ClearStandardMergeAuthoringSelections();
            foreach (FirmwareSlotViewModel slot in _merge.MergeSlots
                         .Concat(_merge.AbMergeSlots)
                         .Concat([_merge.MergeDpSlot, _merge.MergeTpSlot, _merge.MergeLdcSlot])
                         .Distinct())
            {
                ClearFirmwareSlot(slot);
            }

            foreach (GeneralMergeMappingViewModel mapping in _merge.GeneralMergeMappings)
            {
                mapping.FilePath = null;
            }

            _merge.RefreshMergeMemoryMapState();
        }
        else if (page == ShellPage.Replace)
        {
            foreach (FirmwareSlotViewModel slot in _replace.ReplaceSlots
                         .Concat([_replace.ReplaceBaseSlot])
                         .Distinct())
            {
                ClearFirmwareSlot(slot);
            }

            foreach (GeneralReplaceMappingViewModel mapping in _replace.GeneralReplaceMappings)
            {
                mapping.FilePath = null;
            }

            _replace.ClearCtrlRamInspectionDisplay();
            _replace.RefreshReplaceMemoryMapState();
        }

        NotifySlotFileOutputNames();
        _stateBindings.ResetRunResult();
        _stateBindings.RefreshCommandState();
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
        slot.ClearCurrentInspectionProjection();
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
    }
}
