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

}
