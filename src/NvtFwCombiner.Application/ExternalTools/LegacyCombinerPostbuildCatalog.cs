using System.Globalization;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Owner-provided CtrlRAM postbuild command profiles normalized from postbuild scripts.</summary>
public static class LegacyCombinerPostbuildCatalog
{
    private const string ToolBindingId = "legacy-combiner-1.13.0";

    /// <summary>Supported postbuild profiles in stable IC order.</summary>
    public static IReadOnlyList<LegacyCombinerPostbuildProfile> All =>
    [
        Nt51917,
        Nt51919,
        Nt51920,
        Nt51923,
        Nt51926,
        Nt51926CommonFw141,
        Nt51927,
        Nt51928,
        Nt51929,
        Nt51930,
        Nt51930CommonFw1x,
        Nt51931,
        Nt51932,
        Nt51950,
        Nt51951,
    ];

    /// <summary>Gets every approved postbuild profile for an IC, including codebase-version variants.</summary>
    public static IReadOnlyList<LegacyCombinerPostbuildProfile> GetProfiles(string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        return [
            .. All.Where(profile => string.Equals(profile.IcId, icId, StringComparison.Ordinal)),
        ];
    }

    /// <summary>Gets the default postbuild profile used for catalog-only display when no base image is available.</summary>
    public static bool TryGetDefaultProfile(
        string icId,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = GetProfiles(icId);
        postbuildProfile = profiles.Count == 0 ? null : profiles[0];
        return profiles.Count > 0;
    }

    /// <summary>Selects the postbuild category for a base image Common FW version.</summary>
    public static bool TrySelectProfileForCommonFwVersion(
        string icId,
        string? commonFwVersion,
        out LegacyCombinerPostbuildProfile? postbuildProfile,
        out string? issue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);

        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = GetProfiles(icId);
        if (profiles.Count == 0)
        {
            postbuildProfile = null;
            issue = $"No legacy Combiner postbuild profile is registered for {icId}.";
            return false;
        }

        if (profiles.Count == 1)
        {
            postbuildProfile = profiles[0];
            issue = null;
            return true;
        }

        if (TryResolveVersionedProfile(icId, commonFwVersion, out postbuildProfile))
        {
            issue = null;
            return true;
        }

        issue = string.IsNullOrWhiteSpace(commonFwVersion)
            ? $"{icId} has multiple postbuild categories; base FWConfig Common FW version is required."
            : $"{icId} Common FW {commonFwVersion} has no approved postbuild category. Supported categories: {DescribeSupportedCategories(icId)}.";
        return false;
    }

    private static bool TryResolveVersionedProfile(
        string icId,
        string? commonFwVersion,
        out LegacyCombinerPostbuildProfile? postbuildProfile)
    {
        postbuildProfile = null;
        if (string.IsNullOrWhiteSpace(commonFwVersion))
        {
            return false;
        }

        postbuildProfile = (icId, commonFwVersion.Trim()) switch
        {
            ("NT51926", "1.4.1") => Nt51926CommonFw141,
            ("NT51926", "2.0.0") => Nt51926,
            ("NT51930", string version) when version.StartsWith("1.", StringComparison.Ordinal) => Nt51930CommonFw1x,
            ("NT51930", "2.0.0") => Nt51930,
            _ => null,
        };
        return postbuildProfile is not null;
    }

    private static string DescribeSupportedCategories(string icId)
    {
        return icId switch
        {
            "NT51926" => "Common FW 1.4.1 => PostbuildSetup_51926_1.4.1; Common FW 2.0.0 => PostbuildSetup_51926_2.0.0",
            "NT51930" => "Common FW 1.x.x => PostbuildSetup_51930_1.4.0; Common FW 2.0.0 => PostbuildSetup_51930_2.0.0",
            _ => "one unversioned postbuild category",
        };
    }

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
        branchRules: SingleCascadeBranchRules(1, [2, 3]));

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
        branchRules: SingleCascadeBranchRules(1, [2, 3]));

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
        branchRules: SingleCascadeBranchRules(1, Enumerable.Range(2, 28)));

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
        branchRules: SingleCascadeBranchRules(1, Enumerable.Range(2, 28)));

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

    private static IReadOnlyList<LegacyCombinerPostbuildBranchRule> SingleCascadeBranchRules(
        int singleValue,
        IEnumerable<int> cascadeValues)
    {
        return [
            BranchRule("single", LegacyCombinerPostbuildBranch.SingleChip),
            BranchRule("cascade", LegacyCombinerPostbuildBranch.Cascade),
            BranchRule(singleValue.ToString(CultureInfo.InvariantCulture), LegacyCombinerPostbuildBranch.SingleChip),
            .. cascadeValues.Select(value => BranchRule(
                value.ToString(CultureInfo.InvariantCulture),
                LegacyCombinerPostbuildBranch.Cascade)),
        ];
    }

    private static IReadOnlyList<LegacyCombinerPostbuildBranchRule> NumericOneTwoThreeBranchRules()
    {
        return [
            BranchRule("single", LegacyCombinerPostbuildBranch.SingleChip),
            BranchRule("cascade", LegacyCombinerPostbuildBranch.Cascade),
            BranchRule("1", LegacyCombinerPostbuildBranch.SingleChip),
            BranchRule("2", LegacyCombinerPostbuildBranch.TwoChip),
            BranchRule("3", LegacyCombinerPostbuildBranch.ThreeChip),
        ];
    }

    private static LegacyCombinerPostbuildBranchRule BranchRule(
        string token,
        LegacyCombinerPostbuildBranch branch)
    {
        return new LegacyCombinerPostbuildBranchRule(token, branch);
    }

    private static IReadOnlyList<LegacyCombinerPostbuildCommand> Nt51927TwoChipCommands()
    {
        return
        [
            MergeCommand(
                "nt51927-2chip-master-ctrlram",
                [
                    BaseCopy(),
                    Bin("nf-master", "NF_Ctrlram.bin", 0x0, 0x16800, 4048),
                    Bin("normal-master", "Normal_Ctrlram.bin", 0x0, 0x177D0, 12288),
                    Bin("mp-master", "MP_Ctrlram.bin", 0x0, 0x1A7D0, 9216),
                    Bin("vn-master", "VN_Ctrlram.bin", 0x0, 0x1CBD0, 5728),
                ]),
            MergeCommand("nt51927-2chip-fw-config-backup", [BaseCopy(), Fw("fw-config-backup", 0x16000, 0x34000, 2048)]),
            MergeCommand("nt51927-2chip-header-master", [BaseCopy(), Fw("header-master", 0x200, 0x1E230, 400)]),
            MergeCommand("nt51927-2chip-copy-right-window", [BaseCopy(), Fw("copy-right-window", 0x16000, 0x1F000, 36864)]),
            MergeCommand(
                "nt51927-2chip-right-ctrlram",
                [
                    BaseCopy(),
                    Bin("nf-right-prefix", "NF_Ctrlram.bin", 0x0, 0x1F800, 16),
                    Bin("nf-right-body", "NF_Ctrlram.bin", 0xFD0, 0x1F810, 4032),
                    Bin("normal-right", "Normal_Ctrlram_R.bin", 0x0, 0x207D0, 12288),
                    Bin("mp-right", "MP_Ctrlram_R.bin", 0x0, 0x237D0, 9216),
                    Bin("vn-right", "VN_Ctrlram.bin", 0x0, 0x25BD0, 5728),
                ]),
            MergeCommand("nt51927-2chip-final-header-backup", [BaseCopy(), Fw("final-header-backup", 0x0, 0x32DC0, 1120)]),
            CrcOnlyCommand("nt51927-2chip-crc-1"),
            MergeCommand("nt51927-2chip-header-refresh-master", [BaseCopy(), Fw("header-refresh-master", 0x200, 0x1E230, 400)]),
            MergeCommand("nt51927-2chip-header-refresh-right", [BaseCopy(), Fw("header-refresh-right", 0x200, 0x27230, 400)]),
            CrcOnlyCommand("nt51927-2chip-crc-2"),
        ];
    }

    private static IReadOnlyList<LegacyCombinerPostbuildCommand> Nt51927ThreeChipCommands()
    {
        return
        [
            MergeCommand(
                "nt51927-3chip-master-ctrlram",
                [
                    BaseCopy(),
                    Bin("nf-master-prefix", "NF_Ctrlram.bin", 0x0, 0x16800, 16),
                    Bin("nf-master-body", "NF_Ctrlram.bin", 0xFD0, 0x16810, 4032),
                    Bin("normal-master", "Normal_Ctrlram.bin", 0x0, 0x177D0, 12288),
                    Bin("mp-master", "MP_Ctrlram.bin", 0x0, 0x1A7D0, 9216),
                    Bin("vn-master", "VN_Ctrlram.bin", 0x0, 0x1CBD0, 5728),
                ]),
            MergeCommand("nt51927-3chip-fw-config-backup", [BaseCopy(), Fw("fw-config-backup", 0x16000, 0x34000, 2048)]),
            MergeCommand("nt51927-3chip-header-master", [BaseCopy(), Fw("header-master", 0x200, 0x1E230, 400)]),
            MergeCommand("nt51927-3chip-copy-right-window", [BaseCopy(), Fw("copy-right-window", 0x16000, 0x1F000, 36864)]),
            MergeCommand(
                "nt51927-3chip-right-ctrlram",
                [
                    BaseCopy(),
                    Bin("nf-right-prefix", "NF_Ctrlram.bin", 0x0, 0x1F800, 16),
                    Bin("nf-right-body", "NF_Ctrlram.bin", 0x1F90, 0x1F810, 4032),
                    Bin("normal-right", "Normal_Ctrlram_R.bin", 0x0, 0x207D0, 12288),
                    Bin("mp-right", "MP_Ctrlram_R.bin", 0x0, 0x237D0, 9216),
                    Bin("vn-right", "VN_Ctrlram.bin", 0x0, 0x25BD0, 5728),
                ]),
            MergeCommand("nt51927-3chip-copy-left-window", [BaseCopy(), Fw("copy-left-window", 0x16000, 0x28000, 36864)]),
            MergeCommand(
                "nt51927-3chip-left-ctrlram",
                [
                    BaseCopy(),
                    Bin("nf-left", "NF_Ctrlram.bin", 0x0, 0x28800, 4048),
                    Bin("normal-left", "Normal_Ctrlram_L.bin", 0x0, 0x297D0, 12288),
                    Bin("mp-left", "MP_Ctrlram_L.bin", 0x0, 0x2C7D0, 9216),
                    Bin("vn-left", "VN_Ctrlram.bin", 0x0, 0x2EBD0, 5728),
                ]),
            MergeCommand("nt51927-3chip-final-header-backup", [BaseCopy(), Fw("final-header-backup", 0x0, 0x32DC0, 1120)]),
            CrcOnlyCommand("nt51927-3chip-crc-1"),
            MergeCommand("nt51927-3chip-header-refresh-master", [BaseCopy(), Fw("header-refresh-master", 0x200, 0x1E230, 400)]),
            MergeCommand("nt51927-3chip-header-refresh-right", [BaseCopy(), Fw("header-refresh-right", 0x200, 0x27230, 400)]),
            MergeCommand("nt51927-3chip-header-refresh-left", [BaseCopy(), Fw("header-refresh-left", 0x200, 0x30230, 400)]),
            CrcOnlyCommand("nt51927-3chip-crc-2"),
        ];
    }

    private static LegacyCombinerPostbuildCommand MergeCommand(
        string commandId,
        IReadOnlyList<LegacyCombinerBlockArgument> blocks)
    {
        return new LegacyCombinerPostbuildCommand(
            commandId,
            LegacyCombinerCommandFamily.MergeMode,
            "MERGE_MODE",
            null,
            blocks);
    }

    private static LegacyCombinerPostbuildCommand CrcOnlyCommand(string commandId)
    {
        return new LegacyCombinerPostbuildCommand(
            commandId,
            LegacyCombinerCommandFamily.CrcOnlyMode,
            "NT51927BASED_GEN_CRC_MODE",
            "CRC32",
            []);
    }

    private static LegacyCombinerPostbuildCommand NormalCommand(
        string commandId,
        IReadOnlyList<LegacyCombinerBlockArgument> blocks)
    {
        return new LegacyCombinerPostbuildCommand(
            commandId,
            LegacyCombinerCommandFamily.NormalMode,
            "CRC_Enable",
            null,
            blocks);
    }

    private static LegacyCombinerPostbuildCommand NtBasedCommand(
        string commandId,
        string modeArgument,
        IReadOnlyList<LegacyCombinerBlockArgument> blocks)
    {
        return new LegacyCombinerPostbuildCommand(
            commandId,
            LegacyCombinerCommandFamily.NtBasedNormalMode,
            modeArgument,
            "CRC8",
            blocks);
    }

    private static LegacyCombinerBlockArgument Bin(
        string blockId,
        string fileName,
        long sourceOffset,
        long destinationOffset,
        long length)
    {
        return new LegacyCombinerBlockArgument(
            blockId,
            LegacyCombinerBlockSourceKind.StagedFile,
            fileName,
            sourceOffset,
            new ByteRange(destinationOffset, length));
    }

    private static LegacyCombinerBlockArgument Fw(
        string blockId,
        long sourceOffset,
        long destinationOffset,
        long length)
    {
        return new LegacyCombinerBlockArgument(
            blockId,
            LegacyCombinerBlockSourceKind.FirmwareImage,
            "firmware",
            sourceOffset,
            new ByteRange(destinationOffset, length));
    }

    private static LegacyCombinerBlockArgument BaseCopy()
    {
        return Fw("base", 0x0, 0x0, 217088);
    }

    private static IReadOnlyList<LegacyCombinerPostbuildCommand> AliasCommands(
        string sourceCommandPrefix,
        string aliasCommandPrefix,
        IReadOnlyList<LegacyCombinerPostbuildCommand> commands)
    {
        return [
            .. commands.Select(command => new LegacyCombinerPostbuildCommand(
                command.CommandId.Replace(sourceCommandPrefix, aliasCommandPrefix, StringComparison.Ordinal),
                command.Family,
                command.ModeArgument,
                command.CrcArgument,
                command.Blocks)),
        ];
    }
}
