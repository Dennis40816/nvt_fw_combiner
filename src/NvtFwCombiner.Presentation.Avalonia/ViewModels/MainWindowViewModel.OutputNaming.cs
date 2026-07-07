using System.Globalization;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string CreateFlashCodeOutputFileName()
    {
        string date = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string dpVersion = FindDpVersionToken() ?? "xx";
        string tpVersion = FindTpVersionToken() ?? "xx";
        return FormattableString.Invariant($"{SelectedIc}_FlashCode_D{dpVersion}T{tpVersion}_{date}.bin");
    }

    private string? FindDpVersionToken()
    {
        foreach (FirmwareSlotViewModel slot in EnumerateVersionCandidateSlots()
            .Where(slot => slot.SlotKind == FirmwareSlotKind.Dp))
        {
            string? token = UiCompositionRunner.TryGetDpVersionToken(SelectedIc, slot.FilePath);
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        return null;
    }

    private string? FindTpVersionToken()
    {
        foreach (FirmwareSlotViewModel slot in EnumerateVersionCandidateSlots())
        {
            string? token = UiCompositionRunner.TryGetFirmwareVersionToken(SelectedIc, slot.FilePath);
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        return null;
    }

    private IEnumerable<FirmwareSlotViewModel> EnumerateVersionCandidateSlots()
    {
        return MergeSlots
            .Concat(ReplaceSlots)
            .Concat([ReplaceBaseSlot])
            .OrderBy(GetVersionCandidatePriority);
    }

    private static int GetVersionCandidatePriority(FirmwareSlotViewModel slot)
    {
        return slot.SlotKind == FirmwareSlotKind.Tp
            ? 0
            : slot.SlotKind == FirmwareSlotKind.CtrlRam
                ? 1
                : slot.SlotKind == FirmwareSlotKind.Base ? 2 : 3;
    }
}
