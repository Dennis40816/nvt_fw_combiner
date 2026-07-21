using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.ExternalTools;

public sealed partial class LegacyCombinerPostbuildCatalogTests
{
    /// <summary>Locks NT51917 to the owner-approved NT51927 special postbuild flow.</summary>
    [Fact]
    public void Nt51917AliasesNt51927PostbuildFlow()
    {
        AssertNt51927Alias(
            LegacyCombinerPostbuildCatalog.Nt51917,
            "NT51917",
            "nt51917_fw.bin",
            "nt51917-2chip-right-ctrlram");
    }

    /// <summary>Locks NT51928 non-NB to the owner-approved NT51927 special postbuild flow.</summary>
    [Fact]
    public void Nt51928AliasesNt51927PostbuildFlow()
    {
        AssertNt51927Alias(
            LegacyCombinerPostbuildCatalog.Nt51928,
            "NT51928",
            "nt51928_fw.bin",
            "nt51928-2chip-right-ctrlram");
    }

    /// <summary>Locks the 51927 family to TP_FW postbuild followed by final DP/TP assembly.</summary>
    [Fact]
    public void Nt51927FamilyDeclaresRefreshedTpAssembly()
    {
        LegacyCombinerPostbuildProfile[] tpAssemblyProfiles =
        [
            LegacyCombinerPostbuildCatalog.Nt51917,
            LegacyCombinerPostbuildCatalog.Nt51927,
            LegacyCombinerPostbuildCatalog.Nt51928,
        ];

        Assert.All(tpAssemblyProfiles, profile =>
            Assert.Equal(
                LegacyCombinerPostbuildAssemblyKind.RefreshedTpThenStandardMerge,
                profile.AssemblyKind));
        Assert.All(
            LegacyCombinerPostbuildCatalog.All.Except(tpAssemblyProfiles),
            profile => Assert.Equal(LegacyCombinerPostbuildAssemblyKind.InPlaceFirmwareImage, profile.AssemblyKind));
    }

    /// <summary>Locks NT51929 to the owner-approved NT51932-based postbuild flow.</summary>
    [Fact]
    public void Nt51929AliasesNt51932PostbuildFlow()
    {
        Assert.Equal("NT51929", LegacyCombinerPostbuildCatalog.Nt51929.IcId);
        Assert.Equal("nt51929_fw.bin", LegacyCombinerPostbuildCatalog.Nt51929.FirmwareFileName);

        LegacyCombinerPostbuildCommand nt51929Command = LegacyCombinerPostbuildCatalog.Nt51929.SingleCommands[0];
        LegacyCombinerPostbuildCommand nt51932Command = LegacyCombinerPostbuildCatalog.Nt51932.SingleCommands[0];

        Assert.StartsWith("nt51929-", nt51929Command.CommandId, StringComparison.Ordinal);
        Assert.Equal("NT51932BASED_NORMAL_MODE", nt51929Command.ModeArgument);
        Assert.Equal(nt51932Command.CrcArgument, nt51929Command.CrcArgument);
        AssertEquivalentBlocks(nt51932Command.Blocks, nt51929Command.Blocks);
    }

    /// <summary>Locks NT51919 to the owner-approved NT51929/NT51932-based postbuild flow.</summary>
    [Fact]
    public void Nt51919AliasesNt51929PostbuildFlow()
    {
        Assert.Equal("NT51919", LegacyCombinerPostbuildCatalog.Nt51919.IcId);
        Assert.Equal("nt51919_fw.bin", LegacyCombinerPostbuildCatalog.Nt51919.FirmwareFileName);

        LegacyCombinerPostbuildCommand nt51919Command = LegacyCombinerPostbuildCatalog.Nt51919.SingleCommands[0];
        LegacyCombinerPostbuildCommand nt51929Command = LegacyCombinerPostbuildCatalog.Nt51929.SingleCommands[0];

        Assert.StartsWith("nt51919-", nt51919Command.CommandId, StringComparison.Ordinal);
        Assert.Equal("NT51932BASED_NORMAL_MODE", nt51919Command.ModeArgument);
        Assert.Equal(nt51929Command.CrcArgument, nt51919Command.CrcArgument);
        AssertEquivalentBlocks(nt51929Command.Blocks, nt51919Command.Blocks);
    }

    /// <summary>Locks NT51951 to the owner-approved NT51950-based postbuild flow.</summary>
    [Fact]
    public void Nt51951AliasesNt51950PostbuildFlow()
    {
        Assert.Equal("NT51951", LegacyCombinerPostbuildCatalog.Nt51951.IcId);
        Assert.Equal("nt51951_fw.bin", LegacyCombinerPostbuildCatalog.Nt51951.FirmwareFileName);

        LegacyCombinerPostbuildCommand nt51951Command = LegacyCombinerPostbuildCatalog.Nt51951.CascadeCommands[0];
        LegacyCombinerPostbuildCommand nt51950Command = LegacyCombinerPostbuildCatalog.Nt51950.CascadeCommands[0];

        Assert.StartsWith("nt51951-", nt51951Command.CommandId, StringComparison.Ordinal);
        Assert.Equal("NT51950BASED_NORMAL_MODE", nt51951Command.ModeArgument);
        Assert.Equal(nt51950Command.CrcArgument, nt51951Command.CrcArgument);
        AssertEquivalentBlocks(nt51950Command.Blocks, nt51951Command.Blocks);
    }

    /// <summary>Locks NT51923 cascade DiffDLM source offsets from the postbuild script.</summary>
    [Fact]
    public void Nt51923CascadeDiffDlmUsesSplitSourceOffsets()
    {
        var selection = new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]);

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51923,
            selection);

        IReadOnlyList<LegacyCombinerBlockArgument> diffBlocks = [
            .. plan.Commands
                .SelectMany(command => command.Blocks)
                .Where(block => block.SourceFileName == "DiffDLM.bin")
                .OrderBy(block => block.SourceOffset),
        ];

        Assert.Equal(2, diffBlocks.Count);
        Assert.Equal(0x0, diffBlocks[0].SourceOffset);
        Assert.Equal(new ByteRange(0x28800, 3072), diffBlocks[0].FirmwareRange);
        Assert.Equal(0x1400, diffBlocks[1].SourceOffset);
        Assert.Equal(new ByteRange(0x29400, 3072), diffBlocks[1].FirmwareRange);
    }

    /// <summary>Verifies NT51927 resolves the explicit single, two-chip, and three-chip postbuild branches.</summary>
    [Fact]
    public void Nt51927ResolvesExplicitNumericIcCountBranches()
    {
        LegacyCombinerPostbuildCommandPlan single = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51927,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["1"]));
        LegacyCombinerPostbuildCommandPlan twoChip = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51927,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["2"]));
        LegacyCombinerPostbuildCommandPlan threeChip = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51927,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["3"]));

        Assert.Equal(LegacyCombinerPostbuildBranch.SingleChip, single.Branch);
        Assert.Equal(LegacyCombinerPostbuildBranch.TwoChip, twoChip.Branch);
        Assert.Equal(LegacyCombinerPostbuildBranch.ThreeChip, threeChip.Branch);
        Assert.Equal(7, single.Commands.Count);
        Assert.Equal(10, twoChip.Commands.Count);
        Assert.Equal(13, threeChip.Commands.Count);
        Assert.Equal("nt51927-2chip-right-ctrlram", twoChip.Commands[4].CommandId);
        Assert.Equal("nt51927-3chip-left-ctrlram", threeChip.Commands[6].CommandId);
    }

    /// <summary>Verifies unsupported NT51927 IC counts fail instead of falling through to a different Combiner section.</summary>
    [Fact]
    public void Nt51927RejectsUnsupportedNumericIcCount()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            LegacyCombinerPostbuildPlanner.CreatePlan(
                LegacyCombinerPostbuildCatalog.Nt51927,
                new IcNumberSelection(IcNumberInputMode.NumericSelector, ["4"])));

        Assert.Contains("is not supported", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>NT51931 follows the production count rule: one is single and counts above one are cascade.</summary>
    [Fact]
    public void Nt51931UsesGenericMultiChipCountRule()
    {
        LegacyCombinerPostbuildCommandPlan single = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51931,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["1"]));
        LegacyCombinerPostbuildCommandPlan cascade = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51931,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["2"]));

        Assert.Equal(LegacyCombinerPostbuildBranch.SingleChip, single.Branch);
        Assert.Equal(LegacyCombinerPostbuildBranch.Cascade, cascade.Branch);
        _ = Assert.Throws<ArgumentException>(() => LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51931,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["0"])));
        Assert.Equal("legacy-combiner-1.13.0", cascade.Profile.ToolBindingId);
        Assert.All(cascade.Commands, command =>
        {
            Assert.Equal("NT51931BASED_NORMAL_MODE", command.ModeArgument);
            Assert.Equal("CRC8", command.CrcArgument);
        });
        Assert.Contains(cascade.Commands.SelectMany(command => command.Blocks), block => block.SourceFileName == "DiffDLM.bin");
    }

    /// <summary>Locks the NT51927 two-chip and three-chip NF split offsets from postbuild.</summary>
    [Fact]
    public void Nt51927PostbuildKeepsDifferentRightNfOffsetsByIcCount()
    {
        LegacyCombinerPostbuildCommandPlan twoChip = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51927,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["2"]));
        LegacyCombinerPostbuildCommandPlan threeChip = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51927,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["3"]));

        LegacyCombinerBlockArgument twoChipRightNf = twoChip.Commands[4].Blocks.Single(block =>
            block.BlockId == "nf-right-body");
        LegacyCombinerBlockArgument threeChipRightNf = threeChip.Commands[4].Blocks.Single(block =>
            block.BlockId == "nf-right-body");

        Assert.Equal(0xFD0, twoChipRightNf.SourceOffset);
        Assert.Equal(new ByteRange(0x1F810, 4032), twoChipRightNf.FirmwareRange);
        Assert.Equal(0x1F90, threeChipRightNf.SourceOffset);
        Assert.Equal(new ByteRange(0x1F810, 4032), threeChipRightNf.FirmwareRange);
    }

    private static void AssertEquivalentBlocks(
        IReadOnlyList<LegacyCombinerBlockArgument> expected,
        IReadOnlyList<LegacyCombinerBlockArgument> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index].SourceKind, actual[index].SourceKind);
            Assert.Equal(expected[index].SourceFileName, actual[index].SourceFileName);
            Assert.Equal(expected[index].SourceOffset, actual[index].SourceOffset);
            Assert.Equal(expected[index].FirmwareRange, actual[index].FirmwareRange);
        }
    }

    private static void AssertNt51927Alias(
        LegacyCombinerPostbuildProfile profile,
        string icId,
        string firmwareFileName,
        string expectedTwoChipCommandId)
    {
        Assert.Equal(icId, profile.IcId);
        Assert.Equal(firmwareFileName, profile.FirmwareFileName);

        LegacyCombinerPostbuildCommandPlan twoChip = LegacyCombinerPostbuildPlanner.CreatePlan(
            profile,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["2"]));

        Assert.Equal(LegacyCombinerPostbuildBranch.TwoChip, twoChip.Branch);
        Assert.Equal(10, twoChip.Commands.Count);
        Assert.Equal(expectedTwoChipCommandId, twoChip.Commands[4].CommandId);
        Assert.Equal("MERGE_MODE", twoChip.Commands[0].ModeArgument);
        Assert.Contains(twoChip.Commands, command => command.ModeArgument == "NT51927BASED_GEN_CRC_MODE");
        AssertEquivalentBlocks(
            LegacyCombinerPostbuildCatalog.Nt51927.TwoChipCommands![4].Blocks,
            twoChip.Commands[4].Blocks);
    }
}
