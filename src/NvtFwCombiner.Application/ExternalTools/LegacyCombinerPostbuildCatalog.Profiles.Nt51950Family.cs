namespace NvtFwCombiner.Application.ExternalTools;

public static partial class LegacyCombinerPostbuildCatalog
{
    /// <summary>NT51950 CtrlRAM postbuild profile.</summary>
    public static LegacyCombinerPostbuildProfile Nt51950 { get; } = new(
        "nfc.nt51950.ctrlram-postbuild-v1",
        "NT51950",
        ToolBindingId,
        "nt51950_fw.bin",
        [
            NtBasedCommand(
                "nt51950-single-merge-crc",
                "NT51950BASED_NORMAL_MODE",
                [
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x25610, 23552),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x2B210, 8444),
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x22C00, 10768),
                    Fw("header-copy", 0xA000, 0x2D30C, 512),
                ]),
            NtBasedCommand(
                "nt51950-single-header-crc",
                "NT51950BASED_NORMAL_MODE",
                [Fw("header-copy-final", 0xA000, 0x2D30C, 512)]),
        ],
        [
            NtBasedCommand(
                "nt51950-cascade-merge-crc",
                "NT51950BASED_NORMAL_MODE",
                [
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x25610, 23552),
                    Bin("diff", "DiffDLM.bin", 0x0, 0x33200, 5120),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x2B210, 8444),
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x22C00, 10768),
                    Fw("header-copy", 0xA000, 0x2D30C, 512),
                ]),
            NtBasedCommand(
                "nt51950-cascade-header-crc",
                "NT51950BASED_NORMAL_MODE",
                [Fw("header-copy-final", 0xA000, 0x2D30C, 512)]),
        ],
        "IC FlashMap postbuild/PostbuildSetup_51950_2.0.0.bat",
        branchRules: SingleCascadeBranchRules(1, [2]));

    /// <summary>NT51951 CtrlRAM postbuild profile using the owner-approved NT51950 reference flow.</summary>
    public static LegacyCombinerPostbuildProfile Nt51951 { get; } = new(
        "nfc.nt51951.ctrlram-postbuild-v1",
        "NT51951",
        ToolBindingId,
        "nt51951_fw.bin",
        AliasCommands("nt51950", "nt51951", Nt51950.SingleCommands),
        AliasCommands("nt51950", "nt51951", Nt51950.CascadeCommands),
        "IC FlashMap postbuild/PostbuildSetup_51950_2.0.0.bat; owner confirmation: NT51951 follows NT51950",
        branchRules: SingleCascadeBranchRules(1, [2]));
}
