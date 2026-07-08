using System.Globalization;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

public static partial class LegacyCombinerPostbuildCatalog
{
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

    private static IReadOnlyList<LegacyCombinerPostbuildBranchRule> SingleCascadeExtendBranchRules(
        int singleValue,
        IEnumerable<int> cascadeValues,
        IEnumerable<int> cascadeExtendedValues)
    {
        return [
            .. SingleCascadeBranchRules(singleValue, cascadeValues),
            .. cascadeExtendedValues.Select(value => BranchRule(
                value.ToString(CultureInfo.InvariantCulture),
                LegacyCombinerPostbuildBranch.CascadeExtended)),
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
