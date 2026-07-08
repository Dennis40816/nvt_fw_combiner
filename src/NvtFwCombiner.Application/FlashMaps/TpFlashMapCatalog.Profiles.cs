using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.FlashMaps;

public static partial class TpFlashMapCatalog
{
    private static IReadOnlyList<TpFlashMapProfile> BuildProfiles()
    {
        TpFlashMapRegion[] nt51927Regions = Nt51927Regions();
        TpFlashMapRegion[] nt51929Regions = Nt51929Regions();
        TpFlashMapRegion[] nt51950Regions = Nt51950Regions();
        return
        [
            Profile("NT51917", "51927 & 51928 (Not NB)", 0x16000, nt51927Regions, "Owner confirmation: NT51917 follows NT51927."),
            Profile("NT51919", "51929 & 51932", 0x1F200, nt51929Regions, "Owner confirmation: NT51919 follows NT51929."),
            Profile("NT51920", "51920", 0x22000, Nt51920Regions()),
            Profile("NT51923", "51923", 0x22000, Nt51923Regions()),
            Profile("NT51926", "51926", 0x22000, Nt51926Regions()),
            Profile("NT51927", "51927 & 51928 (Not NB)", 0x16000, nt51927Regions),
            Profile("NT51928", "51927 & 51928 (Not NB)", 0x16000, nt51927Regions, "NT51928 non-NB follows NT51927. NT51928 NB is not covered."),
            Profile("NT51929", "51929 & 51932", 0x1F200, nt51929Regions, "Owner confirmation: NT51929 follows NT51932 postbuild."),
            Profile("NT51930", "51930", 0x1F200, Nt51930Regions()),
            Profile("NT51931", "51931", 0x16000, Nt51931Regions()),
            Profile("NT51932", "51929 & 51932", 0x1F200, nt51929Regions),
            Profile("NT51950", "51950 (No Backup EN)", 0x22200, nt51950Regions),
            Profile("NT51951", "51951 (No Back Up EN)", 0x22200, nt51950Regions, "Owner confirmation: NT51951 follows NT51950 postbuild."),
        ];
    }

    private static TpFlashMapProfile Profile(
        string icId,
        string overviewSource,
        long firmwareConfigStart,
        IEnumerable<TpFlashMapRegion> regions,
        string? note = null)
    {
        string evidence = string.IsNullOrWhiteSpace(note)
            ? "IC_FlashMap.xlsx TP Overview and postbuild naming."
            : $"IC_FlashMap.xlsx TP Overview and postbuild naming. {note}";
        return new TpFlashMapProfile(icId, overviewSource, firmwareConfigStart, regions, evidence);
    }

    private static TpFlashMapRegion[] Nt51920Regions()
    {
        return
        [
            Ctrl("normal-master", "Normal CtrlRAM (Master)", 0x22780, 0x02800, "Normal_Ctrlram.bin"),
            Ctrl("mp-master", "MP CtrlRAM (Master)", 0x24F80, 0x01700, "MP_Ctrlram.bin"),
            Ctrl("normal-slave", "Normal CtrlRAM (Slave)", 0x26780, 0x02800, "Normal_Ctrlram_S.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["slave"]),
            Ctrl("mp-slave", "MP CtrlRAM (Slave)", 0x28F80, 0x01700, "MP_Ctrlram_S.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["slave"]),
            Ctrl("nf", "NF CtrlRAM", 0x2A780, 0x01F90, "NF_Ctrlram.bin"),
            Ctrl("vn", "VN CtrlRAM", 0x2C710, 0x01018, "VN_Ctrlram.bin"),
            Ctrl("vector", "Vector CtrlRAM", 0x2D728, 0x00258, "Vector_Ctrlram.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["vector"]),
            Region("fw-config-backup", "FW Config Backup", TpFlashMapRegionKind.Other, 0x2F000, 0x00780, tags: ["backup", "postbuild"]),
            Region("dp", "DP Region", TpFlashMapRegionKind.Dp, 0x3E000, 0x02000),
        ];
    }

    private static TpFlashMapRegion[] Nt51923Regions()
    {
        return
        [
            Ctrl("normal", "Normal CtrlRAM", 0x22800, 0x03800, "Normal_Ctrlram.bin"),
            Ctrl("mp", "MP CtrlRAM", 0x26000, 0x02800, "MP_Ctrlram.bin"),
            Ctrl("diff", "DIFF CtrlRAM", 0x28800, 0x01800, "DiffDLM.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["diff"]),
            Ctrl("nf", "NF CtrlRAM", 0x2A000, 0x044B0, "NF_Ctrlram.bin"),
            Ctrl("vn", "VN CtrlRAM", 0x2E800, 0x01660, "VN_Ctrlram.bin"),
            Region("fw-config-backup", "FW Config", TpFlashMapRegionKind.Other, 0x3B000, 0x00800, tags: ["fw-config", "postbuild", "workbook-label-fw-config"]),
            Region("project-id", "Project ID", TpFlashMapRegionKind.ProjectId, 0x3C000, 0x01000),
            Region("customer-info", "Customer information", TpFlashMapRegionKind.CustomerInfo, 0x3D000, 0x01000, tags: ["preserve"]),
            Region("dp", "DP Region", TpFlashMapRegionKind.Dp, 0x3E000, 0x02000),
        ];
    }

    private static TpFlashMapRegion[] Nt51926Regions()
    {
        return
        [
            Ctrl("normal", "Normal CtrlRAM", 0x22800, 0x02C00, "Normal_Ctrlram.bin"),
            Ctrl("mp", "MP CtrlRAM", 0x25400, 0x02400, "MP_Ctrlram.bin"),
            Ctrl("diff", "DIFF CtrlRAM", 0x27800, 0x02800, "DiffDLM.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["diff"]),
            Ctrl("nf", "NF CtrlRAM", 0x2C800, 0x02DD0, "NF_Ctrlram.bin"),
            Ctrl("vn", "VN CtrlRAM", 0x315D0, 0x0149E, "VN_Ctrlram.bin"),
            Region("fw-config-backup", "FW Config Backup", TpFlashMapRegionKind.Other, 0x3B000, 0x00780, tags: ["backup", "postbuild"]),
            Region("project-id", "Project ID", TpFlashMapRegionKind.ProjectId, 0x3C000, 0x01000),
            Region("customer-info", "Customer information", TpFlashMapRegionKind.CustomerInfo, 0x3D000, 0x01000, tags: ["preserve"]),
            Region("dp", "DP Region", TpFlashMapRegionKind.Dp, 0x3E000, 0x02000),
        ];
    }

    private static TpFlashMapRegion[] Nt51927Regions()
    {
        return
        [
            Ctrl("nf-master", "NF CtrlRAM (Master)", 0x16800, 0x00FD0, "NF_Ctrlram.bin"),
            Ctrl("normal-master", "Normal CtrlRAM (Master)", 0x177D0, 0x03000, "Normal_Ctrlram.bin"),
            Ctrl("mp-master", "MP CtrlRAM (Master)", 0x1A7D0, 0x02400, "MP_Ctrlram.bin"),
            Ctrl("vn-master", "VN CtrlRAM (Master)", 0x1CBD0, 0x01660, "VN_Ctrlram.bin"),
            Ctrl("nf-slave-r", "NF CtrlRAM (Slave R)", 0x1F800, 0x00FD0, "NF_Ctrlram.bin", TpFlashMapRegionVisibility.TwoChipAndAbove, ["slave"]),
            Ctrl("normal-slave-r", "Normal CtrlRAM (Slave R)", 0x207D0, 0x03000, "Normal_Ctrlram_R.bin", TpFlashMapRegionVisibility.TwoChipAndAbove, ["slave"]),
            Ctrl("mp-slave-r", "MP CtrlRAM (Slave R)", 0x237D0, 0x02400, "MP_Ctrlram_R.bin", TpFlashMapRegionVisibility.TwoChipAndAbove, ["slave"]),
            Ctrl("vn-slave-r", "VN CtrlRAM (Slave R)", 0x25BD0, 0x01660, "VN_Ctrlram.bin", TpFlashMapRegionVisibility.TwoChipAndAbove, ["slave"]),
            Ctrl("nf-slave-l", "NF CtrlRAM (Slave L)", 0x28800, 0x00FD0, "NF_Ctrlram.bin", TpFlashMapRegionVisibility.ThreeChipAndAbove, ["slave"]),
            Ctrl("normal-slave-l", "Normal CtrlRAM (Slave L)", 0x297D0, 0x03000, "Normal_Ctrlram_L.bin", TpFlashMapRegionVisibility.ThreeChipAndAbove, ["slave"]),
            Ctrl("mp-slave-l", "MP CtrlRAM (Slave L)", 0x2C7D0, 0x02400, "MP_Ctrlram_L.bin", TpFlashMapRegionVisibility.ThreeChipAndAbove, ["slave"]),
            Ctrl("vn-slave-l", "VN CtrlRAM (Slave L)", 0x2EBD0, 0x01660, "VN_Ctrlram.bin", TpFlashMapRegionVisibility.ThreeChipAndAbove, ["slave"]),
            Region("header-backup", "FW Header Backup", TpFlashMapRegionKind.Other, 0x32DC0, 0x00460, tags: ["backup", "postbuild"]),
            Region("fw-config-reg-backup", "FW Config/Reg Backup", TpFlashMapRegionKind.Other, 0x34000, 0x00800, tags: ["backup", "postbuild"]),
            Region("customer-info", "Customer information", TpFlashMapRegionKind.CustomerInfo, 0x3B000, 0x01000, tags: ["preserve"]),
            Region("dp-initial-code", "DP Region (Initial Code)", TpFlashMapRegionKind.Dp, 0x3C000, 0x04000),
            Region("dp-ldc-51928", "DP Region (LDC) - NT51928 only", TpFlashMapRegionKind.Dp, 0x40000, 0x22000),
        ];
    }

    private static TpFlashMapRegion[] Nt51929Regions()
    {
        return
        [
            Region("dp", "DP Region", TpFlashMapRegionKind.Dp, 0x00000, 0x06000),
            Region("customer-info", "Customer information", TpFlashMapRegionKind.CustomerInfo, 0x06000, 0x01000, tags: ["preserve"]),
            Ctrl("nf", "NF CtrlRAM", 0x1FC00, 0x01F90, "NF_Ctrlram.bin"),
            Ctrl("normal", "Normal CtrlRAM", 0x21B90, 0x04A00, "Normal_Ctrlram.bin"),
            Ctrl("vn", "VN CtrlRAM", 0x26590, 0x01960, "VN_Ctrlram.bin"),
            Ctrl("diff", "DIFF CtrlRAM", 0x2D100, 0x08C00, "DiffDLM.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["diff"]),
            Region("fw-information", "FW Information", TpFlashMapRegionKind.Other, 0x3F000, 0x00FFC, tags: ["fw-information", "protected"]),
        ];
    }

    private static TpFlashMapRegion[] Nt51930Regions()
    {
        return
        [
            Region("dp", "DP Region", TpFlashMapRegionKind.Dp, 0x00000, 0x06000),
            Region("customer-info", "Customer information", TpFlashMapRegionKind.CustomerInfo, 0x06000, 0x01000, tags: ["preserve"]),
            Ctrl("normal", "Normal CtrlRAM", 0x21650, 0x02C00, "Normal_Ctrlram.bin"),
            Ctrl("mp", "MP CtrlRAM", 0x24250, 0x03400, "MP_Ctrlram.bin", tags: ["overview-only"]),
            Ctrl("vn", "VN CtrlRAM", 0x27650, 0x01960, "VN_Ctrlram.bin"),
            Ctrl("diff", "DIFF CtrlRAM", 0x2F200, 0x0FE00, "DiffDLM.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["diff", "<=13ic"]),
            Ctrl("nf", "NF CtrlRAM", 0x1FC00, 0x01A50, "NF_Ctrlram.bin"),
            Region("fw-information-host", "FW Information For Host", TpFlashMapRegionKind.Other, 0x3F000, 0x00FFC, tags: ["fw-information", "host", "protected"]),
        ];
    }

    private static TpFlashMapRegion[] Nt51931Regions()
    {
        return
        [
            Ctrl("nf", "NF CtrlRAM", 0x16800, 0x00FD0, "NF_Ctrlram.bin"),
            Ctrl("normal", "Normal CtrlRAM", 0x177D0, 0x02800, "Normal_Ctrlram.bin"),
            Ctrl("mp", "MP CtrlRAM", 0x19FD0, 0x02400, "MP_Ctrlram.bin"),
            Ctrl("vn", "VN CtrlRAM", 0x1C3D0, 0x01660, "VN_Ctrlram.bin"),
            Ctrl("dlm", "DLM CtrlRAM", 0x22800, 0x17C00, "DiffDLM.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["diff", "dlm"]),
            Region("fw-config-backup", "FW Config Backup", TpFlashMapRegionKind.Other, 0x3B000, 0x00800, tags: ["backup", "postbuild"]),
            Region("dp", "DP Region", TpFlashMapRegionKind.Dp, 0x3E000, 0x02000),
        ];
    }

    private static TpFlashMapRegion[] Nt51950Regions()
    {
        return
        [
            Region("dp-initial-code", "DP Region (Initial Code)", TpFlashMapRegionKind.Dp, 0x00000, 0x0A000),
            Ctrl("nf", "NF CtrlRAM", 0x22C00, 0x02A10, "NF_Ctrlram.bin"),
            Ctrl("normal", "Normal CtrlRAM", 0x25610, 0x05C00, "Normal_Ctrlram.bin"),
            Ctrl("vn", "VN CtrlRAM", 0x2B210, 0x020FC, "VN_Ctrlram.bin"),
            Ctrl("diff", "DIFF CtrlRAM", 0x33200, 0x01400, "DiffDLM.bin", TpFlashMapRegionVisibility.MultiChipOnly, ["diff"]),
            Region("fw-information-host", "FW Information for Host", TpFlashMapRegionKind.Other, 0x36000, 0x00FFC, tags: ["fw-information", "host", "protected"]),
            // 0x37000-0x37FFF is customer information. Merge/Replace must preserve it unless
            // a future explicit customer-info workflow authorizes writes to this region.
            Region("customer-info", "Customer information", TpFlashMapRegionKind.CustomerInfo, 0x37000, 0x01000, tags: ["preserve"]),
            Region("dp-1-or-2ic", "DP Region (1IC or 2IC)", TpFlashMapRegionKind.Dp, 0x38000, 0x08000),
            Region("dp-2ic-only", "DP Region (2IC only)", TpFlashMapRegionKind.Dp, 0x40000, 0x0A000, TpFlashMapRegionVisibility.TwoChipAndAbove),
            Region("dp-ldc-51951", "DP Region (LDC) - NT51951", TpFlashMapRegionKind.Dp, 0x41000, 0x1C000, TpFlashMapRegionVisibility.TwoChipAndAbove),
        ];
    }

    private static TpFlashMapRegion Ctrl(
        string id,
        string displayName,
        long start,
        long length,
        string postbuildFileName,
        TpFlashMapRegionVisibility visibility = TpFlashMapRegionVisibility.Always,
        IReadOnlyList<string>? tags = null)
    {
        return Region(id, displayName, TpFlashMapRegionKind.CtrlRam, start, length, visibility, postbuildFileName, tags);
    }

    private static TpFlashMapRegion Region(
        string id,
        string displayName,
        TpFlashMapRegionKind kind,
        long start,
        long length,
        TpFlashMapRegionVisibility visibility = TpFlashMapRegionVisibility.Always,
        string? postbuildFileName = null,
        IReadOnlyList<string>? tags = null)
    {
        return new TpFlashMapRegion(
            id,
            displayName,
            kind,
            new ByteRange(start, length),
            visibility,
            postbuildFileName,
            tags);
    }
}
