namespace NvtFwCombiner.Application.ExternalTools;

public static partial class LegacyCombinerPostbuildCatalog
{
    /// <summary>NT51932 CtrlRAM postbuild profile.</summary>
    public static LegacyCombinerPostbuildProfile Nt51932 { get; } = new(
        "nfc.nt51932.ctrlram-postbuild-v1",
        "NT51932",
        ToolBindingId,
        "nt51932_fw.bin",
        [
            NtBasedCommand(
                "nt51932-single-merge-crc",
                "NT51932BASED_NORMAL_MODE",
                [
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x21B90, 18944),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x26590, 6496),
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x1FC00, 8080),
                    Fw("header-copy", 0x7000, 0x27EF0, 512),
                ]),
            NtBasedCommand(
                "nt51932-single-header-crc",
                "NT51932BASED_NORMAL_MODE",
                [Fw("header-copy-final", 0x7000, 0x27EF0, 512)]),
        ],
        [
            NtBasedCommand(
                "nt51932-cascade-merge-crc",
                "NT51932BASED_NORMAL_MODE",
                [
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x21B90, 18944),
                    Bin("diff", "DiffDLM.bin", 0x0, 0x2D100, 35840),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x26590, 6496),
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x1FC00, 8080),
                    Fw("header-copy", 0x7000, 0x27EF0, 512),
                ]),
            NtBasedCommand(
                "nt51932-cascade-header-crc",
                "NT51932BASED_NORMAL_MODE",
                [Fw("header-copy-final", 0x7000, 0x27EF0, 512)]),
        ],
        "IC FlashMap postbuild/PostbuildSetup_51932_2.0.0.bat",
        branchRules: SingleCascadeBranchRules(1, Enumerable.Range(2, 28)));

    /// <summary>NT51929 CtrlRAM postbuild profile using the owner-approved NT51932 reference flow.</summary>
    public static LegacyCombinerPostbuildProfile Nt51929 { get; } = new(
        "nfc.nt51929.ctrlram-postbuild-v1",
        "NT51929",
        ToolBindingId,
        "nt51929_fw.bin",
        AliasCommands("nt51932", "nt51929", Nt51932.SingleCommands),
        AliasCommands("nt51932", "nt51929", Nt51932.CascadeCommands),
        "IC FlashMap postbuild/PostbuildSetup_51932_2.0.0.bat; owner confirmation: NT51929 follows NT51932",
        branchRules: SingleCascadeBranchRules(1, Enumerable.Range(2, 28)));

    /// <summary>NT51919 CtrlRAM postbuild profile using the owner-approved NT51929 reference flow.</summary>
    public static LegacyCombinerPostbuildProfile Nt51919 { get; } = new(
        "nfc.nt51919.ctrlram-postbuild-v1",
        "NT51919",
        ToolBindingId,
        "nt51919_fw.bin",
        AliasCommands("nt51929", "nt51919", Nt51929.SingleCommands),
        AliasCommands("nt51929", "nt51919", Nt51929.CascadeCommands),
        "IC FlashMap postbuild/PostbuildSetup_51932_2.0.0.bat; owner confirmation: NT51919 follows NT51929",
        branchRules: SingleCascadeBranchRules(1, Enumerable.Range(2, 28)));
}
