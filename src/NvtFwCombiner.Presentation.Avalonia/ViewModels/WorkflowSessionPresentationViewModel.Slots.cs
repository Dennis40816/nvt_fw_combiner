namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class WorkflowSessionPresentationViewModel
{
    /// <summary>Removes a General mapping row through its owning workflow child.</summary>
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
            ShellPage.Home or ShellPage.Settings or ShellPage.HexEditor => false,
            _ => false,
        };
    }

    internal void ClearSelectedInputs(ShellPage page)
    {
        InvalidateFirmwareInspection(clearBaseCache: true, clearFileProjections: true);
        _replace.InvalidateCtrlRamFirmwareVersionContextState();
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

    private FirmwareSlotViewModel? SelectSlotFile(string slotId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        FirmwareSlotViewModel? slot = FindSlot(slotId);
        if (slot is null)
        {
            _ = TrySetGeneralMappingFile(slotId, path);
            return null;
        }

        if (_merge.IsNormalMergeModeSelected &&
            _merge.IsStandardMergeSlot(slot) &&
            !slot.CanSelectFile)
        {
            return null;
        }

        _replace.InvalidateCtrlRamFirmwareVersionContextState();
        InvalidateFirmwareInspection(clearBaseCache: slot.SlotId == _replace.ReplaceBaseSlot.SlotId);
        InspectionSession.RemoveProjection(slot.SlotId);
        InvalidateFirmwareIcMismatch();
        InvalidateFirmwareNumberMismatch();
        slot.FilePath = path;
        slot.SetFirmwareFacts([]);
        slot.ClearInputInspection();
        NotifySlotFileOutputNames();

        if (slot.SlotId == _replace.ReplaceBaseSlot.SlotId && _replace.IsCtrlRamReplaceModeSelected)
        {
            _replace.ClearCtrlRamInspectionDisplay();
        }
        else if ((_merge.IsNormalMergeModeSelected && _merge.IsStandardMergeSlot(slot)) ||
            (_merge.IsAbCodeMergeModeSelected && _merge.AbMergeAddressSpaceBySlotId.ContainsKey(slot.SlotId)))
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

    private bool TrySetGeneralMappingFile(string mappingId, string path)
    {
        return _merge.TrySetGeneralMappingFile(mappingId, path) ||
            _replace.TrySetGeneralMappingFile(mappingId, path);
    }

    private void NotifySlotFileOutputNames()
    {
        _merge.NotifyOutputFileNamesChanged();
        _replace.NotifyOutputFileNamesChanged();
    }

    private FirmwareSlotViewModel? FindSlot(string slotId)
    {
        return _merge.MergeSlots.Concat(_merge.StandardMergeSlots)
            .Concat(_replace.ReplaceSlots)
            .Concat([_replace.ReplaceBaseSlot])
            .FirstOrDefault(slot => string.Equals(slot.SlotId, slotId, StringComparison.Ordinal));
    }

    private static void ClearFirmwareSlot(FirmwareSlotViewModel slot)
    {
        slot.FilePath = null;
        slot.SetFirmwareFacts([]);
        slot.ClearInputInspection();
    }
}
