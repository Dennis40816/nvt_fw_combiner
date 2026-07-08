using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.ExternalTools;

/// <summary>Executable evidence for owner-provided CtrlRAM postbuild catalog data.</summary>
public sealed class LegacyCombinerPostbuildCatalogTests
{
    private const long UniversalCtrlRamSentinelLength = 0x23000;
    private static readonly string[] LegacyNormalModes = ["CRC_Enable", "CRC32_Enable", "CRC_Disable"];
    private static readonly string[] CrcMethods = ["CRC8", "CRC32"];

    /// <summary>Locks NT51926 to the legacy CRC_Enable command family.</summary>
    [Fact]
    public void Nt51926UsesNormalModeCrcEnable()
    {
        LegacyCombinerPostbuildCommand command = LegacyCombinerPostbuildCatalog.Nt51926.SingleCommands[0];

        Assert.Equal(LegacyCombinerCommandFamily.NormalMode, command.Family);
        Assert.Equal("CRC_Enable", command.ModeArgument);
        Assert.Null(command.CrcArgument);
    }

    /// <summary>Locks normal-mode source header CRC word writes outside explicit copy targets.</summary>
    [Fact]
    public void NormalModePlansDeclareKnownSourceHeaderIntegrityWrites()
    {
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51926,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        IReadOnlyList<ByteRange> ranges = LegacyCombinerPostbuildPlanner.GetKnownIntegrityWriteRanges(plan, 0x40000);

        Assert.Contains(new ByteRange(0x1C, 4), ranges);
        Assert.Contains(new ByteRange(0xFC, 4), ranges);
    }

    /// <summary>Locks NT-based source header CRC word writes observed in real-tool smoke evidence.</summary>
    [Fact]
    public void NtBasedNormalPlansDeclareKnownSourceHeaderIntegrityWrites()
    {
        LegacyCombinerPostbuildCommandPlan nt51932 = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51932,
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
        LegacyCombinerPostbuildCommandPlan nt51950 = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51950,
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
        LegacyCombinerPostbuildCommandPlan nt51930CommonFw1x = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51930CommonFw1x,
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        IReadOnlyList<ByteRange> nt51932Ranges = LegacyCombinerPostbuildPlanner.GetKnownIntegrityWriteRanges(nt51932, 0x40000);
        IReadOnlyList<ByteRange> nt51950Ranges = LegacyCombinerPostbuildPlanner.GetKnownIntegrityWriteRanges(nt51950, 0x40000);
        IReadOnlyList<ByteRange> nt51930CommonFw1xRanges = LegacyCombinerPostbuildPlanner.GetKnownIntegrityWriteRanges(
            nt51930CommonFw1x,
            0x40000);

        Assert.Contains(new ByteRange(0x7100, 4), nt51932Ranges);
        Assert.Contains(new ByteRange(0x7118, 4), nt51932Ranges);
        Assert.Contains(new ByteRange(0x7100, 4), nt51930CommonFw1xRanges);
        Assert.Contains(new ByteRange(0x7118, 4), nt51930CommonFw1xRanges);
        Assert.Contains(new ByteRange(0xA11C, 4), nt51950Ranges);
        Assert.Contains(new ByteRange(0xA130, 4), nt51950Ranges);
    }

    /// <summary>Locks NT51927-family CRC-only header integrity writes observed in owner golden self-tests.</summary>
    [Fact]
    public void Nt51927CrcOnlyPlansDeclareKnownHeaderIntegrityWrites()
    {
        LegacyCombinerPostbuildCommandPlan twoChip = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51927,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["2"]));
        LegacyCombinerPostbuildCommandPlan threeChip = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51927,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["3"]));
        LegacyCombinerPostbuildCommandPlan cascade = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51927,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        IReadOnlyList<ByteRange> twoChipRanges = LegacyCombinerPostbuildPlanner.GetKnownIntegrityWriteRanges(
            twoChip,
            0x40000);
        IReadOnlyList<ByteRange> threeChipRanges = LegacyCombinerPostbuildPlanner.GetKnownIntegrityWriteRanges(
            threeChip,
            0x40000);
        IReadOnlyList<ByteRange> cascadeRanges = LegacyCombinerPostbuildPlanner.GetKnownIntegrityWriteRanges(
            cascade,
            0x40000);

        Assert.Contains(new ByteRange(0x23C, 4), twoChipRanges);
        Assert.Contains(new ByteRange(0x24C, 4), twoChipRanges);
        Assert.Contains(new ByteRange(0x26C, 4), twoChipRanges);
        Assert.Contains(new ByteRange(0x27C, 4), twoChipRanges);
        Assert.Contains(new ByteRange(0x22C, 4), threeChipRanges);
        Assert.Contains(new ByteRange(0x29C, 4), threeChipRanges);
        Assert.Contains(new ByteRange(0x2AC, 4), threeChipRanges);
        Assert.Contains(new ByteRange(0x22C, 4), cascadeRanges);
        Assert.Contains(new ByteRange(0x29C, 4), cascadeRanges);
        Assert.Contains(new ByteRange(0x2AC, 4), cascadeRanges);
    }

    /// <summary>Locks required capacity calculation to selected ranges and command source/target coverage.</summary>
    [Fact]
    public void PostbuildPlannerCalculatesRequiredCapacityFromSelectedRangesAndCommands()
    {
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51930CommonFw1x,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["14"]));

        long requiredCapacity = LegacyCombinerPostbuildPlanner.CalculateRequiredCapacity(
            plan,
            [new ByteRange(0x27650, 6494)]);

        Assert.Equal(0x52200, requiredCapacity);
    }

    /// <summary>Locks CtrlRAM allowed writes to staged slots plus declared postbuild/header writes.</summary>
    [Fact]
    public void PostbuildPlannerAllowsStagedSourceWrites()
    {
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51926CommonFw141,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));
        var normalRange = new ByteRange(0x22800, 11264);
        var vnRange = new ByteRange(0x315D0, 5728);

        IReadOnlyList<ByteRange> ranges = LegacyCombinerPostbuildPlanner.GetAllowedWriteRangesForStagedSources(
            plan,
            0x40000,
            [normalRange, vnRange],
            [normalRange, vnRange]);

        Assert.Contains(vnRange, ranges);
        Assert.Contains(normalRange, ranges);
        Assert.Contains(new ByteRange(0x32F50, 256), ranges);
        Assert.Contains(new ByteRange(0x1C, 4), ranges);
        Assert.Contains(new ByteRange(0xFC, 4), ranges);

        IReadOnlyList<LegacyCombinerPostbuildWriteRange> sections =
            LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForStagedSources(
                plan,
                0x40000,
                [normalRange, vnRange],
                [normalRange, vnRange]);

        Assert.Contains(sections, section =>
            section.Range == new ByteRange(0x32F50, 256) &&
            section.SectionId == "tp-header-copy");
        Assert.Contains(sections, section =>
            section.Range == new ByteRange(0x1C, 4) &&
            section.SectionId == "tp-flash-header-crc");
    }

    /// <summary>Locks General Replace postbuild refresh writes to firmware-owned header/integrity ranges.</summary>
    [Fact]
    public void PostbuildPlannerInPlaceRefreshExcludesStagedFileBlocks()
    {
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51950,
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        IReadOnlyList<ByteRange> ranges = LegacyCombinerPostbuildPlanner.GetAllowedWriteRangesForInPlaceRefresh(
            plan,
            0x100000);

        Assert.DoesNotContain(new ByteRange(0x25610, 23552), ranges);
        Assert.Contains(new ByteRange(0x2D30C, 512), ranges);
        Assert.Contains(new ByteRange(0xA11C, 4), ranges);
        Assert.Contains(new ByteRange(0xA130, 4), ranges);

        IReadOnlyList<LegacyCombinerPostbuildWriteRange> sections =
            LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForInPlaceRefresh(
                plan,
                0x100000);
        Assert.Contains(sections, section =>
            section.Range == new ByteRange(0x2D30C, 512) &&
            section.SectionId == "tp-header-copy");
        Assert.Contains(sections, section =>
            section.Range == new ByteRange(0xA11C, 4) &&
            section.SectionId == "tp-flash-header-crc");
    }

    /// <summary>Verifies the documented one-file sentinel covers every current staged CtrlRAM input block.</summary>
    [Fact]
    public void UniversalCtrlRamSentinelLengthCoversEveryStagedPostbuildBlock()
    {
        foreach (LegacyCombinerPostbuildCommandPlan plan in AllPlans())
        {
            foreach (LegacyCombinerBlockArgument block in LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(plan))
            {
                long requiredInputLength = checked(block.SourceOffset + block.FirmwareRange.Length);
                Assert.True(
                    requiredInputLength <= UniversalCtrlRamSentinelLength,
                    $"{plan.Profile.IcId} {plan.Profile.ProcessorId} {plan.Branch} {block.BlockId} requires 0x{requiredInputLength:X}, exceeding sentinel length 0x{UniversalCtrlRamSentinelLength:X}.");
            }
        }
    }

    /// <summary>Verifies cascade selection exposes NT51950 DiffDLM postbuild blocks.</summary>
    [Fact]
    public void Nt51950CascadePlanIncludesDiffDlm()
    {
        var selection = new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]);

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51950,
            selection);

        Assert.Equal(LegacyCombinerPostbuildBranch.Cascade, plan.Branch);
        Assert.Equal(2, plan.Commands.Count);
        Assert.Contains(
            plan.Commands.SelectMany(command => command.Blocks),
            block => block.SourceFileName == "DiffDLM.bin" &&
                     block.FirmwareRange == new ByteRange(0x33200, 5120));
    }

    /// <summary>Verifies single selection does not schedule cascade-only DiffDLM blocks.</summary>
    [Fact]
    public void Nt51950SinglePlanOmitsDiffDlm()
    {
        var selection = new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]);

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51950,
            selection);

        Assert.Equal(LegacyCombinerPostbuildBranch.SingleChip, plan.Branch);
        Assert.DoesNotContain(
            plan.Commands.SelectMany(command => command.Blocks),
            block => block.SourceFileName == "DiffDLM.bin");
    }

    /// <summary>Locks NT51930 cascade support to the current owner-approved less-or-equal 13 IC DiffDLM branch.</summary>
    [Fact]
    public void Nt51930CascadeUsesLessOrEqual13IcDiffDlmLength()
    {
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51930,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        LegacyCombinerBlockArgument diffBlock = plan.Commands
            .SelectMany(command => command.Blocks)
            .Single(block => block.SourceFileName == "DiffDLM.bin");

        Assert.Equal(new ByteRange(0x2F200, 65024), diffBlock.FirmwareRange);
    }

    /// <summary>Locks NT51926 Common FW 1.4.1 to its owner-provided header-copy target.</summary>
    [Fact]
    public void Nt51926CommonFw141SelectsLegacyHeaderCopyTarget()
    {
        Assert.True(LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
            "NT51926",
            "1.4.1",
            out LegacyCombinerPostbuildProfile? profile,
            out string? issue), issue);

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            profile!,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        Assert.Equal("nfc.nt51926.ctrlram-postbuild-fw1.4.1", profile!.ProcessorId);
        Assert.Contains("PostbuildSetup_51926_1.4.1.bat", profile.Evidence, StringComparison.Ordinal);
        Assert.Contains(
            plan.Commands.SelectMany(command => command.Blocks),
            block => block.BlockId == "header-copy" &&
                     block.FirmwareRange == new ByteRange(0x32F50, 256));
        Assert.Contains(
            plan.Commands.SelectMany(command => command.Blocks),
            block => block.SourceFileName == "VN_Ctrlram.bin" &&
                     block.FirmwareRange == new ByteRange(0x315D0, 5728));
    }

    /// <summary>Locks NT51930 Common FW 1.x to the 1.4.0-era command shape.</summary>
    [Fact]
    public void Nt51930CommonFw1xSelectsSingleLegacyHeaderCommand()
    {
        Assert.True(LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
            "NT51930",
            "1.3.0",
            out LegacyCombinerPostbuildProfile? profile,
            out string? issue), issue);

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            profile!,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        Assert.Equal("nfc.nt51930.ctrlram-postbuild-fw1.x", profile!.ProcessorId);
        Assert.Contains("PostbuildSetup_51930_1.4.0.bat", profile.Evidence, StringComparison.Ordinal);
        LegacyCombinerPostbuildCommand command = Assert.Single(plan.Commands);
        Assert.Contains(
            command.Blocks,
            block => block.SourceFileName == "MP_Ctrlram.bin" &&
                     block.FirmwareRange == new ByteRange(0x24250, 13312));
        Assert.Contains(
            command.Blocks,
            block => block.BlockId == "header-copy" &&
                     block.FirmwareRange == new ByteRange(0x28FB0, 256));
    }

    /// <summary>Locks NT51930 Common FW 1.x large cascade counts to the archived extend branch.</summary>
    [Fact]
    public void Nt51930CommonFw1xSelectsExtendedCascadeDiffLength()
    {
        LegacyCombinerPostbuildCommandPlan normalCascade = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51930CommonFw1x,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["13"]));
        LegacyCombinerPostbuildCommandPlan extendedCascade = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51930CommonFw1x,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["14"]));

        LegacyCombinerBlockArgument normalDiff = normalCascade.Commands
            .SelectMany(command => command.Blocks)
            .Single(block => block.SourceFileName == "DiffDLM.bin");
        LegacyCombinerBlockArgument extendedDiff = extendedCascade.Commands
            .SelectMany(command => command.Blocks)
            .Single(block => block.SourceFileName == "DiffDLM.bin");

        Assert.Equal(LegacyCombinerPostbuildBranch.Cascade, normalCascade.Branch);
        Assert.Equal(new ByteRange(0x2F200, 65024), normalDiff.FirmwareRange);
        Assert.Equal(LegacyCombinerPostbuildBranch.CascadeExtended, extendedCascade.Branch);
        Assert.Equal(new ByteRange(0x2F200, 143360), extendedDiff.FirmwareRange);
    }

    /// <summary>Locks ambiguous versioned ICs to fail closed for unsupported Common FW versions.</summary>
    [Fact]
    public void VersionedPostbuildSelectionRejectsUnknownCommonFw()
    {
        Assert.False(LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
            "NT51926",
            "1.4.0",
            out LegacyCombinerPostbuildProfile? profile,
            out string? issue));

        Assert.Null(profile);
        Assert.Contains("no approved postbuild category", issue, StringComparison.Ordinal);
        Assert.Contains("Common FW 1.4.1 => PostbuildSetup_51926_1.4.1", issue, StringComparison.Ordinal);
        Assert.Contains("Common FW 2.0.0 => PostbuildSetup_51926_2.0.0", issue, StringComparison.Ordinal);

        Assert.False(LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
            "NT51930",
            "10.0.0",
            out profile,
            out issue));

        Assert.Null(profile);
        Assert.Contains("no approved postbuild category", issue, StringComparison.Ordinal);
        Assert.Contains("Common FW 1.x.x => PostbuildSetup_51930_1.4.0", issue, StringComparison.Ordinal);
        Assert.Contains("Common FW 2.0.0 => PostbuildSetup_51930_2.0.0", issue, StringComparison.Ordinal);
    }

    /// <summary>Locks duplicate IC postbuild rows to explicit Common FW category selection.</summary>
    [Fact]
    public void DuplicatePostbuildIcIdsMustHaveVersionSelectionPolicy()
    {
        (string IcId, string CommonFwVersion, LegacyCombinerPostbuildProfile Profile)[] versionedCases =
        [
            ("NT51926", "1.4.1", LegacyCombinerPostbuildCatalog.Nt51926CommonFw141),
            ("NT51926", "2.0.0", LegacyCombinerPostbuildCatalog.Nt51926),
            ("NT51930", "1.0.0", LegacyCombinerPostbuildCatalog.Nt51930CommonFw1x),
            ("NT51930", "1.3.0", LegacyCombinerPostbuildCatalog.Nt51930CommonFw1x),
            ("NT51930", "2.0.0", LegacyCombinerPostbuildCatalog.Nt51930),
        ];
        string[] duplicateIcIds = [
            .. LegacyCombinerPostbuildCatalog.All
                .GroupBy(profile => profile.IcId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(["NT51926", "NT51930"], duplicateIcIds);
        foreach (string icId in duplicateIcIds)
        {
            Assert.All(
                LegacyCombinerPostbuildCatalog.GetProfiles(icId),
                profile => Assert.NotNull(profile.CommonFwVersionRule));

            Assert.False(LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
                icId,
                commonFwVersion: null,
                out LegacyCombinerPostbuildProfile? profile,
                out string? issue));
            Assert.Null(profile);
            Assert.Contains("multiple postbuild categories", issue, StringComparison.Ordinal);

            string[] duplicateProfileIds = [
                .. LegacyCombinerPostbuildCatalog.GetProfiles(icId)
                    .Select(candidate => candidate.ProcessorId)
                    .Order(StringComparer.Ordinal),
            ];
            string[] reachableProfileIds = [
                .. versionedCases
                    .Where(testCase => testCase.IcId == icId)
                    .Select(testCase => testCase.Profile.ProcessorId)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
            ];
            Assert.Equal(duplicateProfileIds, reachableProfileIds);
        }

        foreach ((string icId, string commonFwVersion, LegacyCombinerPostbuildProfile expectedProfile) in versionedCases)
        {
            AssertSelectsProfile(icId, commonFwVersion, expectedProfile);
        }
    }

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

    /// <summary>Locks NT51931's postbuild-specific IC number mapping: 0 is single, 1 starts cascade.</summary>
    [Fact]
    public void Nt51931ResolvesPostbuildSpecificZeroAndOneMapping()
    {
        LegacyCombinerPostbuildCommandPlan single = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51931,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["NT51931", "0"]));
        LegacyCombinerPostbuildCommandPlan cascade = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51931,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["NT51931", "1"]));

        Assert.Equal(LegacyCombinerPostbuildBranch.SingleChip, single.Branch);
        Assert.Equal(LegacyCombinerPostbuildBranch.Cascade, cascade.Branch);
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
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51927,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["3"]));

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
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51950,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

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

    private static IEnumerable<LegacyCombinerPostbuildCommandPlan> AllPlans()
    {
        foreach (LegacyCombinerPostbuildProfile profile in LegacyCombinerPostbuildCatalog.All)
        {
            yield return LegacyCombinerPostbuildPlanner.CreatePlan(
                profile,
                new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
            yield return LegacyCombinerPostbuildPlanner.CreatePlan(
                profile,
                new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));
        }

        yield return LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51927,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["2"]));
        yield return LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51927,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["3"]));
        yield return LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51930CommonFw1x,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["14"]));
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

    private static void AssertSelectsProfile(
        string icId,
        string commonFwVersion,
        LegacyCombinerPostbuildProfile expectedProfile)
    {
        Assert.True(LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
            icId,
            commonFwVersion,
            out LegacyCombinerPostbuildProfile? profile,
            out string? issue), issue);

        Assert.Same(expectedProfile, profile);
        Assert.Null(issue);
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
