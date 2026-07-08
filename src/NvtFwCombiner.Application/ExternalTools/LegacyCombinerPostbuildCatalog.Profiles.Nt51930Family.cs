namespace NvtFwCombiner.Application.ExternalTools;

public static partial class LegacyCombinerPostbuildCatalog
{
    /// <summary>NT51930 CtrlRAM postbuild profile.</summary>
    public static LegacyCombinerPostbuildProfile Nt51930 { get; } = new(
        "nfc.nt51930.ctrlram-postbuild-v1",
        "NT51930",
        ToolBindingId,
        "NT51930_fw.bin",
        [
            NtBasedCommand(
                "nt51930-single-merge-crc",
                "NT51930BASED_NORMAL_MODE",
                [
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x21650, 11264),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x27650, 6496),
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x1FC00, 6736),
                    Fw("header-copy", 0x7000, 0x28FB0, 512),
                ]),
            NtBasedCommand(
                "nt51930-single-header-crc",
                "NT51930BASED_NORMAL_MODE",
                [Fw("header-copy-final", 0x7000, 0x28FB0, 512)]),
        ],
        [
            NtBasedCommand(
                "nt51930-cascade-merge-crc",
                "NT51930BASED_NORMAL_MODE",
                [
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x21650, 11264),
                    Bin("diff", "DiffDLM.bin", 0x0, 0x2F200, 65024),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x27650, 6496),
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x1FC00, 6736),
                    Fw("header-copy", 0x7000, 0x28FB0, 512),
                ]),
            NtBasedCommand(
                "nt51930-cascade-header-crc",
                "NT51930BASED_NORMAL_MODE",
                [Fw("header-copy-final", 0x7000, 0x28FB0, 512)]),
        ],
        "IC FlashMap postbuild/PostbuildSetup_51930_2.0.0.bat",
        branchRules: SingleCascadeBranchRules(1, Enumerable.Range(2, 28)),
        commonFwVersionRule: LegacyCombinerCommonFwVersionRule.Exact("2.0.0", "PostbuildSetup_51930_2.0.0"));

    /// <summary>NT51930 Common FW 1.x CtrlRAM postbuild profile.</summary>
    public static LegacyCombinerPostbuildProfile Nt51930CommonFw1x { get; } = new(
        "nfc.nt51930.ctrlram-postbuild-fw1.x",
        "NT51930",
        ToolBindingId,
        "nt51930_fw.bin",
        [
            NtBasedCommand(
                "nt51930-fw1x-single-merge-crc",
                "NT51930BASED_NORMAL_MODE",
                [
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x1FC00, 6736),
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x21650, 11264),
                    Bin("mp", "MP_Ctrlram.bin", 0x0, 0x24250, 13312),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x27650, 6494),
                    Fw("header-copy", 0x7000, 0x28FB0, 256),
                ]),
        ],
        [
            NtBasedCommand(
                "nt51930-fw1x-cascade-merge-crc",
                "NT51930BASED_NORMAL_MODE",
                [
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x1FC00, 6736),
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x21650, 11264),
                    Bin("mp", "MP_Ctrlram.bin", 0x0, 0x24250, 13312),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x27650, 6494),
                    Fw("header-copy", 0x7000, 0x28FB0, 256),
                    Bin("diff", "DiffDLM.bin", 0x0, 0x2F200, 65024),
                ]),
        ],
        "IC FlashMap postbuild/PostbuildSetup_51930_1.4.0.bat",
        cascadeExtendedCommands:
        [
            NtBasedCommand(
                "nt51930-fw1x-cascade-extend-merge-crc",
                "NT51930BASED_NORMAL_MODE",
                [
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x1FC00, 6736),
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x21650, 11264),
                    Bin("mp", "MP_Ctrlram.bin", 0x0, 0x24250, 13312),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x27650, 6494),
                    Fw("header-copy", 0x7000, 0x28FB0, 256),
                    Bin("diff-extend", "DiffDLM.bin", 0x0, 0x2F200, 143360),
                ]),
        ],
        branchRules: SingleCascadeExtendBranchRules(1, Enumerable.Range(2, 12), Enumerable.Range(14, 16)),
        commonFwVersionRule: LegacyCombinerCommonFwVersionRule.Major("1", "1.x.x", "PostbuildSetup_51930_1.4.0"));

    /// <summary>NT51931 CtrlRAM postbuild profile.</summary>
    public static LegacyCombinerPostbuildProfile Nt51931 { get; } = new(
        "nfc.nt51931.ctrlram-postbuild-v1",
        "NT51931",
        ToolBindingId,
        "nt51931_fw.bin",
        [
            NtBasedCommand(
                "nt51931-single-merge-crc",
                "NT51930BASED_NORMAL_MODE",
                [
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x16800, 4048),
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x177D0, 10240),
                    Bin("mp", "MP_Ctrlram.bin", 0x0, 0x19FD0, 9216),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x1C3D0, 5728),
                    Fw("header-copy", 0x0, 0x1DA30, 256),
                    Fw("fw-config-backup", 0x16000, 0x3B000, 2048),
                ]),
        ],
        [
            NtBasedCommand(
                "nt51931-cascade-merge-crc",
                "NT51930BASED_NORMAL_MODE",
                [
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x16800, 4048),
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x177D0, 10240),
                    Bin("mp", "MP_Ctrlram.bin", 0x0, 0x19FD0, 9216),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x1C3D0, 5728),
                    Fw("header-copy", 0x0, 0x1DA30, 256),
                    Fw("fw-config-backup", 0x16000, 0x3B000, 2048),
                    Bin("diff", "DiffDLM.bin", 0x0, 0x22800, 97280),
                ]),
        ],
        "IC FlashMap postbuild/PostbuildSetup_51931_1.3.0.bat",
        branchRules: SingleCascadeBranchRules(0, Enumerable.Range(1, 19)));
}
