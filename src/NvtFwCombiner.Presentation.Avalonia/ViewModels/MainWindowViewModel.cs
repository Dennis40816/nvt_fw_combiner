using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>View model for the production-backed firmware workbench.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
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

        Replace.InvalidateCtrlRamFirmwareVersionContextState();
        WorkflowSession.InvalidateFirmwareInspection(
            clearBaseCache: slot.SlotId == Replace.ReplaceBaseSlot.SlotId);
        WorkflowSession.InspectionSession.RemoveProjection(slot.SlotId);
        WorkflowSession.InvalidateFirmwareIcMismatch();
        WorkflowSession.InvalidateFirmwareNumberMismatch();
        slot.FilePath = path;
        slot.SetFirmwareFacts([]);
        slot.ClearInputInspection();
        NotifySlotFileOutputNames();

        if (slot.SlotId == Replace.ReplaceBaseSlot.SlotId && Replace.IsCtrlRamReplaceModeSelected)
        {
            Replace.ClearCtrlRamInspectionDisplay();
        }
        else if (slot.SlotId == MergeDpSlotId ||
            (Merge.IsAbCodeMergeModeSelected && Merge.AbMergeAddressSpaceBySlotId.ContainsKey(slot.SlotId)))
        {
            Merge.RefreshMergeMemoryMapState();
        }
        else if (slot.SlotId == Replace.ReplaceBaseSlot.SlotId)
        {
            Replace.RefreshReplaceMemoryMapState();
        }
        else if (slot.RegionId is not null)
        {
            Replace.RefreshReplaceMemoryMapState();
        }

        RefreshCommandState();
        return slot;
    }

    private void NotifySlotFileOutputNames()
    {
        Merge.NotifyOutputFileNamesChanged();
        Replace.NotifyOutputFileNamesChanged();
    }

    private FirmwareSlotViewModel? FindSlot(string slotId)
    {
        return Merge.MergeSlots.Concat(Replace.ReplaceSlots)
            .Concat([Replace.ReplaceBaseSlot])
            .FirstOrDefault(slot => string.Equals(slot.SlotId, slotId, StringComparison.Ordinal));
    }

}

/// <summary>Top-level shell page state.</summary>
public enum ShellPage
{
    /// <summary>Clean home view with three entry cards.</summary>
    Home,

    /// <summary>Settings planning page.</summary>
    Settings,

    /// <summary>Merge planning page.</summary>
    Merge,

    /// <summary>Replace planning page.</summary>
    Replace,

    /// <summary>Independent raw-BIN utility page.</summary>
    HexEditor,
}
