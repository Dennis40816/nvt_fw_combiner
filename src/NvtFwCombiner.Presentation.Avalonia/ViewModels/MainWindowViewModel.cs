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

        InvalidateCtrlRamFirmwareVersionContext();
        InvalidateFirmwareInspection(clearBaseCache: slot.SlotId == ReplaceBaseSlotId);
        _ = _firmwareFileProjections.Remove(slot.SlotId);
        InvalidateFirmwareIcMismatch();
        InvalidateFirmwareNumberMismatch();
        slot.FilePath = path;
        slot.SetFirmwareFacts([]);
        NotifySlotFileOutputNames();

        if (slot.SlotId == ReplaceBaseSlotId && IsCtrlRamReplaceModeSelected)
        {
            ClearCtrlRamInspectionDisplay();
        }
        else if (slot.SlotId == MergeDpSlotId)
        {
            RefreshMergeMemoryMapState();
        }
        else if (slot.SlotId == ReplaceBaseSlotId)
        {
            RefreshReplaceMemoryMapState();
        }

        RefreshCommandState();
        return slot;
    }

    private void NotifySlotFileOutputNames()
    {
        OnPropertyChanged(nameof(StandardMergeOutputFileName));
        OnPropertyChanged(nameof(GeneralMergeOutputFileName));
        OnPropertyChanged(nameof(MergeOutputFileName));
        OnPropertyChanged(nameof(ReplaceOutputFileName));
    }

    private FirmwareSlotViewModel? FindSlot(string slotId)
    {
        return MergeSlots.Concat(ReplaceSlots)
            .Concat([ReplaceBaseSlot])
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
