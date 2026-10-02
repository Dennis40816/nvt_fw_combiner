using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.ExternalTools;

public sealed partial class LegacyCombinerPostbuildCatalogTests
{
    private static readonly string[] LegacyNormalModes = ["CRC_Enable", "CRC32_Enable", "CRC_Disable"];
    private static readonly string[] CrcMethods = ["CRC8", "CRC32"];
    /// <summary>Verifies every normalized postbuild command line follows the Combiner 1.13.0 argv contract.</summary>
    [Fact]
    public void CommandLineBuilderMatchesHsiCombinerArgumentShapes()
    {
        foreach (LegacyCombinerPostbuildCommandPlan plan in AllPlans())
        {
            foreach (LegacyCombinerPostbuildCommand command in plan.Commands)
            {
                IReadOnlyList<string> arguments = LegacyCombinerPostbuildCommandLineBuilder.CreateArguments(
                    command,
                    @"C:\nfc\output\fw.bin",
                    @"C:\nfc\BIN");

                VerifyArgumentShape(command, arguments);
            }
        }
    }

    /// <summary>Locks the NT51927 three-chip MERGE and CRC command heads used by the postbuild script.</summary>
    [Fact]
    public void CommandLineBuilderKeepsNt51927ThreeChipPostbuildCommandHeads()
    {
        const string firmwarePath = "output/nt51927_fw.bin";
        const string binDirectory = "BIN";
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildCatalog.Nt51927.ResolvePlan(new IcNumberSelection(IcNumberInputMode.NumericSelector, ["3"]));

        IReadOnlyList<string> mergeArguments = LegacyCombinerPostbuildCommandLineBuilder.CreateArguments(
            plan.Commands[0],
            firmwarePath,
            binDirectory);
        IReadOnlyList<string> crcArguments = LegacyCombinerPostbuildCommandLineBuilder.CreateArguments(
            plan.Commands[^1],
            firmwarePath,
            binDirectory);

        Assert.Equal("nt51927-3chip-master-ctrlram", plan.Commands[0].CommandId);
        Assert.Equal([
            "MERGE_MODE",
            firmwarePath,
            firmwarePath,
            "0x0",
            "0x0",
            "217088",
            Path.Combine(binDirectory, "NF_Ctrlram.bin"),
            "0x0",
            "0x16800",
            "16",
            Path.Combine(binDirectory, "NF_Ctrlram.bin"),
            "0xFD0",
            "0x16810",
            "4032",
        ], mergeArguments.Take(14));
        Assert.Equal([
            "NT51927BASED_GEN_CRC_MODE",
            "CRC32",
            firmwarePath,
            firmwarePath,
        ], crcArguments);
    }

    /// <summary>Locks the NT51950 NT-based command head and first block from postbuild evidence.</summary>
    [Fact]
    public void CommandLineBuilderKeepsNt51950CascadePostbuildCommandHead()
    {
        const string firmwarePath = "output/nt51950_fw.bin";
        const string binDirectory = "BIN";
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildCatalog.Nt51950.ResolvePlan(new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        IReadOnlyList<string> arguments = LegacyCombinerPostbuildCommandLineBuilder.CreateArguments(
            plan.Commands[0],
            firmwarePath,
            binDirectory);

        Assert.Equal("nt51950-cascade-merge-crc", plan.Commands[0].CommandId);
        Assert.Equal([
            "NT51950BASED_NORMAL_MODE",
            "CRC8",
            firmwarePath,
            firmwarePath,
            Path.Combine(binDirectory, "Normal_Ctrlram.bin"),
            "0x0",
            "0x25610",
            "23552",
        ], arguments.Take(8));
    }

    private static void VerifyArgumentShape(
        LegacyCombinerPostbuildCommand command,
        IReadOnlyList<string> arguments)
    {
        switch (command.Family)
        {
            case LegacyCombinerCommandFamily.NormalMode:
                Assert.Contains(command.ModeArgument, LegacyNormalModes);
                Assert.True(arguments.Count >= 6);
                Assert.Equal(0, (arguments.Count - 2) % 4);
                break;
            case LegacyCombinerCommandFamily.MergeMode:
                Assert.Equal("MERGE_MODE", command.ModeArgument);
                Assert.True(arguments.Count >= 6);
                Assert.Equal(0, (arguments.Count - 2) % 4);
                break;
            case LegacyCombinerCommandFamily.NtBasedNormalMode:
                Assert.Contains(command.CrcArgument, CrcMethods);
                Assert.True(arguments.Count >= 8);
                Assert.Equal(0, (arguments.Count - 4) % 4);
                break;
            case LegacyCombinerCommandFamily.CrcOnlyMode:
                Assert.Equal("NT51927BASED_GEN_CRC_MODE", command.ModeArgument);
                Assert.Equal("CRC32", command.CrcArgument);
                Assert.Equal(4, arguments.Count);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command.Family, "Unsupported command family.");
        }
    }
}
