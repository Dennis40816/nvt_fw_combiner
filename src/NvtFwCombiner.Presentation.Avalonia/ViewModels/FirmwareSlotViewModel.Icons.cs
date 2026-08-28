namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class FirmwareSlotViewModel
{
    private const string BaseIconPathData =
        "M4,5 L16,5 L16,15 L4,15 Z M7,3 L7,5 M10,3 L10,5 M13,3 L13,5 M7,15 L7,17 M10,15 L10,17 M13,15 L13,17 M7,8 L13,8 M7,11 L13,11";
    private const string DpIconPathData =
        "M3,4 L17,4 L17,14 L3,14 Z M8,18 L12,18 M10,14 L10,18";
    private const string TpIconPathData =
        "M8,10 L8,5 L10,5 L10,11 L11,10 L12,11 L13,10 L14,12 L14,17 L8,17 L5,13 L6,12 L8,14";
    private const string CtrlRamIconPathData =
        "M6,5 L14,5 L15,6 L15,14 L14,15 L6,15 L5,14 L5,6 Z M8,8 L12,8 L12,12 L8,12 Z M8,2 L8,5 M12,2 L12,5 M8,15 L8,18 M12,15 L12,18 M2,8 L5,8 M2,12 L5,12 M15,8 L18,8 M15,12 L18,12";
    private const string BinIconPathData =
        "M5,3 L15,3 L15,17 L5,17 Z M7,7 L13,7 M7,10 L13,10 M7,13 L11,13";
    public string SlotIconPathData => SlotKind switch
    {
        FirmwareSlotKind.Unknown => BinIconPathData,
        FirmwareSlotKind.Base => BaseIconPathData,
        FirmwareSlotKind.Dp => DpIconPathData,
        FirmwareSlotKind.Tp => TpIconPathData,
        FirmwareSlotKind.CtrlRam => CtrlRamIconPathData,
        _ => BinIconPathData,
    };

    public string SlotIconTooltip => SlotKind switch
    {
        FirmwareSlotKind.Unknown => "BIN input",
        FirmwareSlotKind.Base => "Reference firmware input",
        FirmwareSlotKind.Dp => "DP BIN",
        FirmwareSlotKind.Tp => "TP BIN",
        FirmwareSlotKind.CtrlRam => "CtrlRAM BIN",
        _ => "BIN input",
    };

}
