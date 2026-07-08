using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal static class FirmwareSlotKindResolver
{
    private const string BaseTitleTerm = "Base";
    private const string CtrlRamTitleTerm = "CtrlRAM";
    private const string TpTitleTerm = "TP ";
    private const string DpTitleTerm = "DP ";
    private const string LdTitleTerm = "LD ";
    private const string LdcSlotTerm = "ldc";

    public static FirmwareSlotKind Resolve(string slotId, string title)
    {
        return true switch
        {
            _ when IsSlot(slotId, WorkbenchSlotIds.ReplaceBase) => FirmwareSlotKind.Base,
            _ when slotId.StartsWith(WorkbenchSlotIds.ReplaceCtrlRamPrefix, StringComparison.OrdinalIgnoreCase) =>
                FirmwareSlotKind.CtrlRam,
            _ when IsSlot(slotId, WorkbenchSlotIds.MergeTp) => FirmwareSlotKind.Tp,
            _ when IsSlot(slotId, WorkbenchSlotIds.MergeDp) ||
                IsSlot(slotId, WorkbenchSlotIds.ReplaceDp) ||
                IsSlot(slotId, WorkbenchSlotIds.MergeLd) => FirmwareSlotKind.Dp,
            _ => ResolveFallback(title, slotId),
        };
    }

    private static FirmwareSlotKind ResolveFallback(string title, string slotId)
    {
        return true switch
        {
            _ when Contains(title, BaseTitleTerm) => FirmwareSlotKind.Base,
            _ when Contains(title, CtrlRamTitleTerm) => FirmwareSlotKind.CtrlRam,
            _ when Contains(title, TpTitleTerm) => FirmwareSlotKind.Tp,
            _ when Contains(title, DpTitleTerm) ||
                Contains(title, LdTitleTerm) ||
                Contains(slotId, LdcSlotTerm) => FirmwareSlotKind.Dp,
            _ => FirmwareSlotKind.Unknown,
        };
    }

    private static bool IsSlot(string value, string expected)
    {
        return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string value, string expected)
    {
        return value.Contains(expected, StringComparison.OrdinalIgnoreCase);
    }
}
