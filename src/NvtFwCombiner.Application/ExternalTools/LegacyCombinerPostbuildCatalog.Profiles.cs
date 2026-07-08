namespace NvtFwCombiner.Application.ExternalTools;

public static partial class LegacyCombinerPostbuildCatalog
{
    /// <summary>NT51920 CtrlRAM postbuild profile.</summary>
    public static LegacyCombinerPostbuildProfile Nt51920 { get; } = new(
        "nfc.nt51920.ctrlram-postbuild-v1",
        "NT51920",
        ToolBindingId,
        "nt51920_fw.bin",
        [
            NormalCommand(
                "nt51920-single-merge-crc",
                [
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x22780, 10240),
                    Bin("mp", "MP_Ctrlram.bin", 0x0, 0x24F80, 5888),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x2C710, 4120),
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x2A780, 8080),
                    Fw("fw-config-backup", 0x22000, 0x2F000, 1920),
                    Fw("header-copy", 0x0, 0x26680, 256),
                ]),
            NormalCommand("nt51920-single-header-crc", [Fw("header-copy-final", 0x0, 0x26680, 256)]),
        ],
        [
            NormalCommand(
                "nt51920-cascade-merge-crc",
                [
                    Bin("normal-master", "Normal_Ctrlram.bin", 0x0, 0x22780, 10240),
                    Bin("normal-slave", "Normal_Ctrlram_S.bin", 0x0, 0x26780, 10240),
                    Bin("mp-master", "MP_Ctrlram.bin", 0x0, 0x24F80, 5888),
                    Bin("mp-slave", "MP_Ctrlram_S.bin", 0x0, 0x28F80, 5888),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x2C710, 4120),
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x2A780, 8080),
                    Bin("vector", "Vector_Ctrlram.bin", 0x0, 0x2D728, 600),
                    Fw("fw-config-backup", 0x22000, 0x2F000, 1920),
                    Fw("header-copy", 0x0, 0x26680, 256),
                ]),
            NormalCommand("nt51920-cascade-header-crc", [Fw("header-copy-final", 0x0, 0x26680, 256)]),
        ],
        "IC FlashMap postbuild/PostbuildSetup_51920_1.3.1.bat",
        branchRules: SingleCascadeBranchRules(0, [1]));

    /// <summary>NT51923 CtrlRAM postbuild profile.</summary>
    public static LegacyCombinerPostbuildProfile Nt51923 { get; } = new(
        "nfc.nt51923.ctrlram-postbuild-v1",
        "NT51923",
        ToolBindingId,
        "nt51923_fw.bin",
        [
            NormalCommand(
                "nt51923-single-merge-crc",
                [
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x22800, 14336),
                    Bin("mp", "MP_Ctrlram.bin", 0x0, 0x26000, 10240),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x2E800, 5728),
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x2A000, 17584),
                    Fw("fw-config-backup", 0x22000, 0x3B000, 2048),
                    Fw("header-copy", 0x0, 0x30310, 256),
                ]),
            NormalCommand("nt51923-single-header-crc", [Fw("header-copy-final", 0x0, 0x30310, 256)]),
        ],
        [
            NormalCommand(
                "nt51923-cascade-merge-crc",
                [
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x22800, 14336),
                    Bin("diff-0", "DiffDLM.bin", 0x0, 0x28800, 3072),
                    Bin("diff-1", "DiffDLM.bin", 0x1400, 0x29400, 3072),
                    Bin("mp", "MP_Ctrlram.bin", 0x0, 0x26000, 10240),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x2E800, 5728),
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x2A000, 17584),
                    Fw("fw-config-backup", 0x22000, 0x3B000, 2048),
                    Fw("header-copy", 0x0, 0x30310, 256),
                ]),
            NormalCommand("nt51923-cascade-header-crc", [Fw("header-copy-final", 0x0, 0x30310, 256)]),
        ],
        "IC FlashMap postbuild/PostbuildSetup_51923_1.4.1.bat",
        branchRules: SingleCascadeBranchRules(1, [2, 3]));

    /// <summary>NT51926 CtrlRAM postbuild profile.</summary>
    public static LegacyCombinerPostbuildProfile Nt51926 { get; } = new(
        "nfc.nt51926.ctrlram-postbuild-v1",
        "NT51926",
        ToolBindingId,
        "nt51926_fw.bin",
        [
            NormalCommand(
                "nt51926-single-merge-crc",
                [
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x22800, 11264),
                    Bin("mp", "MP_Ctrlram.bin", 0x0, 0x25400, 9216),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x315D0, 5278),
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x2C800, 11728),
                    Fw("fw-config-backup", 0x22000, 0x3B000, 1920),
                    Fw("header-copy", 0x0, 0x32A70, 256),
                ]),
            NormalCommand("nt51926-single-header-crc", [Fw("header-copy-final", 0x0, 0x32A70, 256)]),
        ],
        [
            NormalCommand(
                "nt51926-cascade-merge-crc",
                [
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x22800, 11264),
                    Bin("diff", "DiffDLM.bin", 0x0, 0x27800, 10240),
                    Bin("mp", "MP_Ctrlram.bin", 0x0, 0x25400, 9216),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x315D0, 5278),
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x2C800, 11728),
                    Fw("fw-config-backup", 0x22000, 0x3B000, 1920),
                    Fw("header-copy", 0x0, 0x32A70, 256),
                ]),
            NormalCommand("nt51926-cascade-header-crc", [Fw("header-copy-final", 0x0, 0x32A70, 256)]),
        ],
        "IC FlashMap postbuild/PostbuildSetup_51926_2.0.0.bat",
        branchRules: SingleCascadeBranchRules(1, [2, 3]),
        commonFwVersionRule: LegacyCombinerCommonFwVersionRule.Exact("2.0.0", "PostbuildSetup_51926_2.0.0"));

    /// <summary>NT51926 Common FW 1.4.1 CtrlRAM postbuild profile.</summary>
    public static LegacyCombinerPostbuildProfile Nt51926CommonFw141 { get; } = new(
        "nfc.nt51926.ctrlram-postbuild-fw1.4.1",
        "NT51926",
        ToolBindingId,
        "nt51926_fw.bin",
        [
            NormalCommand(
                "nt51926-fw141-single-merge-crc",
                [
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x22800, 11264),
                    Bin("mp", "MP_Ctrlram.bin", 0x0, 0x25400, 9216),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x315D0, 5728),
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x2C800, 11728),
                    Fw("fw-config-backup", 0x22000, 0x3B000, 2048),
                    Fw("header-copy", 0x0, 0x32F50, 256),
                ]),
            NormalCommand("nt51926-fw141-single-header-crc", [Fw("header-copy-final", 0x0, 0x32F50, 256)]),
        ],
        [
            NormalCommand(
                "nt51926-fw141-cascade-merge-crc",
                [
                    Bin("normal", "Normal_Ctrlram.bin", 0x0, 0x22800, 11264),
                    Bin("diff", "DiffDLM.bin", 0x0, 0x27800, 10240),
                    Bin("mp", "MP_Ctrlram.bin", 0x0, 0x25400, 9216),
                    Bin("vn", "VN_Ctrlram.bin", 0x0, 0x315D0, 5728),
                    Bin("nf", "NF_Ctrlram.bin", 0x0, 0x2C800, 11728),
                    Fw("fw-config-backup", 0x22000, 0x3B000, 2048),
                    Fw("header-copy", 0x0, 0x32F50, 256),
                ]),
            NormalCommand("nt51926-fw141-cascade-header-crc", [Fw("header-copy-final", 0x0, 0x32F50, 256)]),
        ],
        "IC FlashMap postbuild/PostbuildSetup_51926_1.4.1.bat",
        branchRules: SingleCascadeBranchRules(1, [2, 3]),
        commonFwVersionRule: LegacyCombinerCommonFwVersionRule.Exact("1.4.1", "PostbuildSetup_51926_1.4.1"));

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
