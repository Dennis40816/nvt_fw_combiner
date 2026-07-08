namespace NvtFwCombiner.Application.ExternalTools;

public static partial class LegacyCombinerPostbuildCatalog
{
    /// <summary>NT51927 CtrlRAM postbuild profile.</summary>
    public static LegacyCombinerPostbuildProfile Nt51927 { get; } = new(
        "nfc.nt51927.ctrlram-postbuild-v1",
        "NT51927",
        ToolBindingId,
        "nt51927_fw.bin",
        [
            MergeCommand(
                "nt51927-single-master-ctrlram",
                [
                    BaseCopy(),
                    Bin("nf-master", "NF_Ctrlram.bin", 0x0, 0x16800, 4048),
                    Bin("normal-master", "Normal_Ctrlram.bin", 0x0, 0x177D0, 12288),
                    Bin("mp-master", "MP_Ctrlram.bin", 0x0, 0x1A7D0, 9216),
                    Bin("vn-master", "VN_Ctrlram.bin", 0x0, 0x1CBD0, 5728),
                ]),
            MergeCommand("nt51927-single-fw-config-backup", [BaseCopy(), Fw("fw-config-backup", 0x16000, 0x34000, 2048)]),
            MergeCommand("nt51927-single-header-master", [BaseCopy(), Fw("header-master", 0x200, 0x1E230, 400)]),
            MergeCommand("nt51927-single-final-header-backup", [BaseCopy(), Fw("final-header-backup", 0x0, 0x32DC0, 1120)]),
            CrcOnlyCommand("nt51927-single-crc-1"),
            MergeCommand("nt51927-single-header-refresh-master", [BaseCopy(), Fw("header-refresh-master", 0x200, 0x1E230, 400)]),
            CrcOnlyCommand("nt51927-single-crc-2"),
        ],
        Nt51927ThreeChipCommands(),
        "IC FlashMap postbuild/PostbuildSetup_51927_1.4.1.bat",
        twoChipCommands: Nt51927TwoChipCommands(),
        threeChipCommands: Nt51927ThreeChipCommands(),
        branchRules: NumericOneTwoThreeBranchRules(),
        assemblyKind: LegacyCombinerPostbuildAssemblyKind.RefreshedTpThenStandardMerge);

    /// <summary>NT51917 CtrlRAM postbuild profile using the owner-approved NT51927 reference flow.</summary>
    public static LegacyCombinerPostbuildProfile Nt51917 { get; } = new(
        "nfc.nt51917.ctrlram-postbuild-v1",
        "NT51917",
        ToolBindingId,
        "nt51917_fw.bin",
        AliasCommands("nt51927", "nt51917", Nt51927.SingleCommands),
        AliasCommands("nt51927", "nt51917", Nt51927.CascadeCommands),
        "IC FlashMap postbuild/PostbuildSetup_51927_1.4.1.bat; owner confirmation: NT51917 follows NT51927",
        twoChipCommands: AliasCommands("nt51927", "nt51917", Nt51927TwoChipCommands()),
        threeChipCommands: AliasCommands("nt51927", "nt51917", Nt51927ThreeChipCommands()),
        branchRules: NumericOneTwoThreeBranchRules(),
        assemblyKind: LegacyCombinerPostbuildAssemblyKind.RefreshedTpThenStandardMerge);

    /// <summary>NT51928 CtrlRAM postbuild profile using the owner-approved NT51927 reference flow.</summary>
    public static LegacyCombinerPostbuildProfile Nt51928 { get; } = new(
        "nfc.nt51928.ctrlram-postbuild-v1",
        "NT51928",
        ToolBindingId,
        "nt51928_fw.bin",
        AliasCommands("nt51927", "nt51928", Nt51927.SingleCommands),
        AliasCommands("nt51927", "nt51928", Nt51927.CascadeCommands),
        "IC FlashMap postbuild/PostbuildSetup_51927_1.4.1.bat; owner confirmation: NT51928 follows NT51927; NT51928 NB is not covered",
        twoChipCommands: AliasCommands("nt51927", "nt51928", Nt51927TwoChipCommands()),
        threeChipCommands: AliasCommands("nt51927", "nt51928", Nt51927ThreeChipCommands()),
        branchRules: NumericOneTwoThreeBranchRules(),
        assemblyKind: LegacyCombinerPostbuildAssemblyKind.RefreshedTpThenStandardMerge);
}
