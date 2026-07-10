using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>View model for the production-backed firmware workbench.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    /// <summary>Sets a local file path for a UI input slot.</summary>
    public void SetSlotFile(string slotId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        FirmwareSlotViewModel? slot = FindSlot(slotId);
        if (slot is null)
        {
            if (SetGeneralMergeMappingFile(slotId, path))
            {
                return;
            }

            SetGeneralReplaceMappingFile(slotId, path);
            return;
        }

        if (slot.SlotId == ReplaceBaseSlotId)
        {
            CaptureGeneralReplaceBaseSnapshot(path);
        }

        slot.FilePath = path;
        RefreshFirmwareFacts(slot);
        PromptForFirmwareIcMismatch(slot);
        if (!IsFirmwareIcMismatchModalOpen)
        {
            TryApplyVerifiedFirmwareContext(slot);
        }
        if (slot.SlotId == MergeTpSlotId && _mergeDpSlot.HasFile)
        {
            RefreshFirmwareFacts(_mergeDpSlot);
        }

        OnPropertyChanged(nameof(StandardMergeOutputFileName));
        OnPropertyChanged(nameof(GeneralMergeOutputFileName));
        OnPropertyChanged(nameof(MergeOutputFileName));
        OnPropertyChanged(nameof(ReplaceOutputFileName));
        if (slot.SlotId == ReplaceBaseSlotId && IsCtrlRamReplaceModeSelected)
        {
            RefreshCtrlRamRegions();
            RefreshReplaceModeState(preserveSlotFiles: true);
            RefreshMemoryMapState();
        }
        else if (slot.SlotId is MergeDpSlotId or ReplaceBaseSlotId)
        {
            RefreshMemoryMapState();
        }

        if (slot.SlotId == ReplaceBaseSlotId)
        {
            RefreshGeneralReplaceEditableRanges();
            RefreshGeneralReplaceHexViewport();
        }

        RefreshCommandState();
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

    /// <summary>Independent experimental hexadecimal patch-authoring page.</summary>
    HexEditor,
}
