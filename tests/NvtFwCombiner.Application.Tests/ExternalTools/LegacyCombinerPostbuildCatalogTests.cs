using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.ExternalTools;

/// <summary>Executable evidence for owner-provided CtrlRAM postbuild catalog data.</summary>
public sealed partial class LegacyCombinerPostbuildCatalogTests
{
    private const long UniversalCtrlRamSentinelLength = 0x23000;

    /// <summary>Locks NT51926 to the legacy CRC_Enable command family.</summary>
    [Fact]
    public void Nt51926UsesNormalModeCrcEnable()
    {
        LegacyCombinerPostbuildCommand command = LegacyCombinerPostbuildCatalog.Nt51926.SingleCommands[0];

        Assert.Equal(LegacyCombinerCommandFamily.NormalMode, command.Family);
        Assert.Equal("CRC_Enable", command.ModeArgument);
        Assert.Null(command.CrcArgument);
    }

    /// <summary>Verifies postbuild profiles expose display-ready categories without leaking source script prefixes.</summary>
    [Fact]
    public void PostbuildProfilesExposeDisplayReadyCategories()
    {
        Assert.Equal("51926_1.4.1", LegacyCombinerPostbuildCatalog.Nt51926CommonFw141.DisplayCategory);
        Assert.Equal("51950_2.0.0", LegacyCombinerPostbuildCatalog.Nt51951.DisplayCategory);
    }

    /// <summary>Locks every normal-mode CRC word that Combiner may refresh outside explicit copy targets.</summary>
    [Fact]
    public void NormalModePlansDeclareKnownSourceHeaderIntegrityWrites()
    {
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51926,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        IReadOnlyList<ByteRange> ranges = IntegrityRanges(plan, 0x40000);

        Assert.Equal(
            [
                new ByteRange(0x18, 4),
                new ByteRange(0x1C, 4),
                new ByteRange(0x3C, 4),
                new ByteRange(0x4C, 4),
                new ByteRange(0x5C, 4),
                new ByteRange(0xFC, 4),
            ],
            ranges);
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
        IReadOnlyList<ByteRange> nt51932Ranges = IntegrityRanges(nt51932, 0x40000);
        IReadOnlyList<ByteRange> nt51950Ranges = IntegrityRanges(nt51950, 0x40000);

        Assert.Contains(new ByteRange(0x7100, 4), nt51932Ranges);
        Assert.Contains(new ByteRange(0x7118, 4), nt51932Ranges);
        Assert.Contains(new ByteRange(0xA11C, 4), nt51950Ranges);
        Assert.Contains(new ByteRange(0xA130, 4), nt51950Ranges);
    }

    /// <summary>Locks cascade-only DLM CRC authority to the owner-confirmed header capacity of each family.</summary>
    [Fact]
    public void NtBasedCascadePlansDeclareDlmCrcWords()
    {
        LegacyCombinerPostbuildCommandPlan nt51932 = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51932,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade_2to8"]));
        LegacyCombinerPostbuildCommandPlan nt51950 = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51950,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        Assert.Contains(new ByteRange(0x7128, 0x1C), IntegrityRanges(nt51932, 0x40000));
        Assert.Contains(new ByteRange(0xA134, 0x4C), IntegrityRanges(nt51950, 0x40000));
    }

    /// <summary>Single-chip plans do not authorize cascade-only DLM CRC words.</summary>
    [Fact]
    public void NtBasedSinglePlansExcludeCascadeOnlyDlmCrcWords()
    {
        LegacyCombinerPostbuildCommandPlan nt51932 = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51932,
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));
        LegacyCombinerPostbuildCommandPlan nt51950 = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51950,
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        Assert.DoesNotContain(new ByteRange(0x7128, 0x1C), IntegrityRanges(nt51932, 0x40000));
        Assert.DoesNotContain(new ByteRange(0xA134, 0x4C), IntegrityRanges(nt51950, 0x40000));
    }

    /// <summary>The NT51919/29/32 family accepts only owner-confirmed 2–8 IC cascade counts.</summary>
    [Fact]
    public void Nt51929FamilyCascadePlansAreLimitedToTwoThroughEightIc()
    {
        LegacyCombinerPostbuildProfile[] profiles =
        [
            LegacyCombinerPostbuildCatalog.Nt51919,
            LegacyCombinerPostbuildCatalog.Nt51929,
            LegacyCombinerPostbuildCatalog.Nt51932,
        ];

        foreach (LegacyCombinerPostbuildProfile profile in profiles)
        {
            Assert.Equal(
                LegacyCombinerPostbuildBranch.Cascade,
                LegacyCombinerPostbuildPlanner.CreatePlan(
                    profile,
                    new IcNumberSelection(IcNumberInputMode.NumericSelector, ["2"])).Branch);
            Assert.Equal(
                LegacyCombinerPostbuildBranch.Cascade,
                LegacyCombinerPostbuildPlanner.CreatePlan(
                    profile,
                    new IcNumberSelection(IcNumberInputMode.NumericSelector, ["8"])).Branch);
            _ = Assert.Throws<ArgumentException>(() =>
                LegacyCombinerPostbuildPlanner.CreatePlan(
                    profile,
                    new IcNumberSelection(IcNumberInputMode.NumericSelector, ["9"])));
            _ = Assert.Throws<ArgumentException>(() =>
                LegacyCombinerPostbuildPlanner.CreatePlan(
                    profile,
                    new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"])));
        }
    }

    /// <summary>Every admitted Dynamic DiffDLM count expands only its N-1 writable DLM prefixes.</summary>
    [Fact]
    public void Nt51929FamilyDynamicDiffDlmPlansResolveEveryCountWithoutSelectingDiffNf()
    {
        LegacyCombinerPostbuildProfile[] profiles =
        [
            LegacyCombinerPostbuildCatalog.Nt51919,
            LegacyCombinerPostbuildCatalog.Nt51929,
            LegacyCombinerPostbuildCatalog.Nt51932,
        ];
        var selection = new IcNumberSelection(
            IcNumberInputMode.CascadeSelector,
            ["cascade_2to8"]);

        foreach (LegacyCombinerPostbuildProfile profile in profiles)
        {
            LegacyCombinerDiffDlmPolicy policy = Assert.IsType<LegacyCombinerDiffDlmPolicy>(
                profile.DiffDlmPolicy);
            for (int icCount = 2; icCount <= 8; icCount++)
            {
                LegacyCombinerPostbuildCommandPlan plan =
                    LegacyCombinerPostbuildPlanner.CreatePlan(
                        profile,
                        selection,
                        reportedChipCount: icCount);
                LegacyCombinerBlockArgument[] diffDlmBlocks =
                [
                    .. LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(plan)
                        .Where(block => block.SourceFileName == "DiffDLM.bin"),
                ];

                Assert.Equal(icCount, plan.TopologyCount);
                Assert.Equal(icCount - 1, diffDlmBlocks.Length);
                for (int record = 0; record < diffDlmBlocks.Length; record++)
                {
                    Assert.Equal(record * 0x1400, diffDlmBlocks[record].SourceOffset);
                    Assert.Equal(
                        new ByteRange(0x2D100 + (record * 0x1400), 0x0B90),
                        diffDlmBlocks[record].FirmwareRange);
                }

                Assert.Contains(
                    LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(plan),
                    block => block.SourceFileName == policy.IndependentNfSourceFileName);
                Assert.Equal(
                    AlignUp(0x2D100 + ((icCount - 1) * 0x1400), 0x1000),
                    policy.GetExpectedFirmwareConfigBackupStart(icCount));
                Assert.True(
                    policy.GetResolvedFirmwareConfigBackupAuthority(icCount).Contains(
                        new ByteRange(
                            policy.GetExpectedFirmwareConfigBackupStart(icCount),
                            policy.FirmwareConfigBackupLength)));
            }
        }
    }

    /// <summary>An explicit numeric selection remains the run topology even when a range-compatible FWConfig count differs.</summary>
    [Fact]
    public void DynamicDiffDlmExplicitCountIsNotOverriddenByReportedRangeCount()
    {
        var selection = new IcNumberSelection(
            IcNumberInputMode.CascadeSelector,
            ["4"]);

        LegacyCombinerPostbuildCommandPlan plan =
            LegacyCombinerPostbuildPlanner.CreatePlan(
                LegacyCombinerPostbuildCatalog.Nt51932,
                selection,
                reportedChipCount: 5);

        Assert.Equal(4, plan.TopologyCount);
        Assert.Equal(
            3,
            LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(plan)
                .Count(block => block.SourceFileName == "DiffDLM.bin"));
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
        IReadOnlyList<ByteRange> twoChipRanges = IntegrityRanges(
            twoChip,
            0x40000);
        IReadOnlyList<ByteRange> threeChipRanges = IntegrityRanges(
            threeChip,
            0x40000);

        Assert.Contains(new ByteRange(0x23C, 4), twoChipRanges);
        Assert.Contains(new ByteRange(0x24C, 4), twoChipRanges);
        Assert.Contains(new ByteRange(0x26C, 4), twoChipRanges);
        Assert.Contains(new ByteRange(0x27C, 4), twoChipRanges);
        Assert.Contains(new ByteRange(0x22C, 4), threeChipRanges);
        Assert.Contains(new ByteRange(0x29C, 4), threeChipRanges);
        Assert.Contains(new ByteRange(0x2AC, 4), threeChipRanges);

        _ = Assert.Throws<ArgumentException>(() => LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51927,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"])));
    }

    /// <summary>Locks the source header provenance used to name copied NT51927 three-chip report fields.</summary>
    [Fact]
    public void Nt51927ThreeChipHeaderCopySectionsKeepSourceRanges()
    {
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51927,
            new IcNumberSelection(IcNumberInputMode.NumericSelector, ["3"]));

        IReadOnlyList<LegacyCombinerPostbuildWriteRange> sections =
            LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForInPlaceRefresh(plan, 0x40000);

        Assert.Contains(sections, section =>
            section.SectionId == PostbuildWriteSectionIds.HeaderCopyMaster &&
            section.Range == new ByteRange(0x1E230, 0x190) &&
            section.SourceRange == new ByteRange(0x200, 0x190));
        Assert.Contains(sections, section =>
            section.SectionId == PostbuildWriteSectionIds.HeaderCopyRight &&
            section.Range == new ByteRange(0x27230, 0x190) &&
            section.SourceRange == new ByteRange(0x200, 0x190));
        Assert.Contains(sections, section =>
            section.SectionId == PostbuildWriteSectionIds.HeaderCopyLeft &&
            section.Range == new ByteRange(0x30230, 0x190) &&
            section.SourceRange == new ByteRange(0x200, 0x190));
        Assert.Contains(sections, section =>
            section.SectionId == PostbuildWriteSectionIds.HeaderCopyFinalBackup &&
            section.Range == new ByteRange(0x32DC0, 0x460) &&
            section.SourceRange == new ByteRange(0x0000, 0x460));
    }

    /// <summary>Required capacity follows the count-resolved active DLM prefix, never the maximum template envelope.</summary>
    [Fact]
    public void PostbuildPlannerCalculatesRequiredCapacityFromSelectedRangesAndCommands()
    {
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51932,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade_2to8"]));

        long requiredCapacity = LegacyCombinerPostbuildPlanner.CalculateRequiredCapacity(
            plan,
            [new ByteRange(0x27650, 6494)]);

        Assert.Equal(2, plan.TopologyCount);
        Assert.Equal(0x2DC90, requiredCapacity);
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

        IReadOnlyList<LegacyCombinerPostbuildWriteRange> sections =
            LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForStagedSources(
            plan,
            0x40000,
            [normalRange, vnRange],
            [normalRange, vnRange]);
        ByteRange[] ranges = [.. sections.Select(section => section.Range)];

        Assert.Contains(vnRange, ranges);
        Assert.Contains(normalRange, ranges);
        Assert.Contains(new ByteRange(0x32F50, 256), ranges);
        Assert.Contains(new ByteRange(0x1C, 4), ranges);
        Assert.Contains(new ByteRange(0x3C, 4), ranges);
        Assert.Contains(new ByteRange(0xFC, 4), ranges);

        Assert.Contains(sections, section =>
            section.Range == new ByteRange(0x32F50, 256) &&
            section.SectionId == PostbuildWriteSectionIds.HeaderCopy);
        Assert.Contains(sections, section =>
            section.Range == new ByteRange(0x1C, 4) &&
            section.SectionId == PostbuildWriteSectionIds.FlashHeaderCrc);
    }

    /// <summary>The canonical integrity identity covers selected commands, staging, assembly, and capacity.</summary>
    [Fact]
    public void PostbuildPlanIntegrityFingerprintBindsExecutionSemantics()
    {
        LegacyCombinerPostbuildCommandPlan baseline =
            CreateIntegrityFingerprintPlan();
        LegacyCombinerPostbuildCommandPlan same =
            CreateIntegrityFingerprintPlan();
        string fingerprint =
            LegacyCombinerPostbuildPlanner.CalculateIntegrityFingerprint(
                baseline,
                0x40000);

        Assert.Equal(
            fingerprint,
            LegacyCombinerPostbuildPlanner.CalculateIntegrityFingerprint(
                same,
                0x40000));
        Assert.Matches("^[0-9a-f]{64}$", fingerprint);
        Assert.NotEqual(
            fingerprint,
            LegacyCombinerPostbuildPlanner.CalculateIntegrityFingerprint(
                CreateIntegrityFingerprintPlan(modeArgument: "CRC_Enable"),
                0x40000));
        Assert.NotEqual(
            fingerprint,
            LegacyCombinerPostbuildPlanner.CalculateIntegrityFingerprint(
                CreateIntegrityFingerprintPlan(stagedArtifactId: "artifact-b"),
                0x40000));
        Assert.NotEqual(
            fingerprint,
            LegacyCombinerPostbuildPlanner.CalculateIntegrityFingerprint(
                CreateIntegrityFingerprintPlan(firmwareStart: 0x24),
                0x40000));
        Assert.NotEqual(
            fingerprint,
            LegacyCombinerPostbuildPlanner.CalculateIntegrityFingerprint(
                CreateIntegrityFingerprintPlan(
                    assemblyKind:
                        LegacyCombinerPostbuildAssemblyKind
                            .RefreshedTpThenStandardMerge),
                0x40000));
        Assert.NotEqual(
            fingerprint,
            LegacyCombinerPostbuildPlanner.CalculateIntegrityFingerprint(
                baseline,
                0x80000));
    }

    /// <summary>Locks General Replace postbuild refresh writes to firmware-owned header/integrity ranges.</summary>
    [Fact]
    public void PostbuildPlannerInPlaceRefreshExcludesStagedFileBlocks()
    {
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51950,
            new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"]));

        IReadOnlyList<LegacyCombinerPostbuildWriteRange> sections =
            LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForInPlaceRefresh(plan, 0x100000);
        ByteRange[] ranges = [.. sections.Select(section => section.Range)];

        Assert.DoesNotContain(new ByteRange(0x25610, 23552), ranges);
        Assert.Contains(new ByteRange(0x2D30C, 512), ranges);
        Assert.Contains(new ByteRange(0xA11C, 4), ranges);
        Assert.Contains(new ByteRange(0xA130, 4), ranges);

        Assert.Contains(sections, section =>
            section.Range == new ByteRange(0x2D30C, 512) &&
            section.SectionId == PostbuildWriteSectionIds.HeaderCopy);
        Assert.Contains(sections, section =>
            section.Range == new ByteRange(0xA11C, 4) &&
            section.SectionId == PostbuildWriteSectionIds.FlashHeaderCrc);
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

    /// <summary>950-family cascade plans expose only the owner-approved active DLM prefix for exact 2 IC.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void Nt51950FamilyCascadePlanPreservesDiffNfAndUsesFixedBackup(string icId)
    {
        var selection = new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]);
        LegacyCombinerPostbuildProfile profile = icId == "NT51950"
            ? LegacyCombinerPostbuildCatalog.Nt51950
            : LegacyCombinerPostbuildCatalog.Nt51951;

        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            profile,
            selection,
            reportedChipCount: 2);
        LegacyCombinerDiffDlmPolicy policy = Assert.IsType<LegacyCombinerDiffDlmPolicy>(
            profile.DiffDlmPolicy);
        LegacyCombinerBlockArgument diff = Assert.Single(
            plan.Commands.SelectMany(command => command.Blocks),
            block => block.SourceFileName == "DiffDLM.bin");

        Assert.Equal(LegacyCombinerPostbuildBranch.Cascade, plan.Branch);
        Assert.Equal(2, plan.Commands.Count);
        Assert.Equal(2, plan.TopologyCount);
        Assert.Equal(0, diff.SourceOffset);
        Assert.Equal(new ByteRange(0x33200, 0x0910), diff.FirmwareRange);
        Assert.Equal(0x1400, policy.GetRequiredSourceLength(2));
        Assert.Equal(0x36000, policy.GetExpectedFirmwareConfigBackupStart(2));
        Assert.Equal(new ByteRange(0x36000, 0x0780), policy.GetResolvedFirmwareConfigBackupAuthority(2));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            LegacyCombinerPostbuildPlanner.CreatePlan(profile, selection, reportedChipCount: 3));
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

    private static IEnumerable<LegacyCombinerPostbuildCommandPlan> AllPlans()
    {
        foreach (LegacyCombinerPostbuildProfile profile in LegacyCombinerPostbuildCatalog.All)
        {
            foreach (LegacyCombinerPostbuildPlanSelector selector in profile.PlanSelectors)
            {
                IcNumberInputMode mode = selector.Kind switch
                {
                    LegacyCombinerPostbuildPlanSelectorKind.SingleChip => IcNumberInputMode.SingleSelector,
                    LegacyCombinerPostbuildPlanSelectorKind.ExactCount => IcNumberInputMode.NumericSelector,
                    LegacyCombinerPostbuildPlanSelectorKind.GenericCascade or
                        LegacyCombinerPostbuildPlanSelectorKind.CountRange => IcNumberInputMode.CascadeSelector,
                    _ => throw new ArgumentOutOfRangeException(),
                };
                yield return LegacyCombinerPostbuildPlanner.CreatePlan(
                    profile,
                    new IcNumberSelection(mode, [selector.Token]));
            }
        }
    }

    private static long AlignUp(long value, int alignment)
    {
        return checked((value + alignment - 1) / alignment * alignment);
    }

    private static IReadOnlyList<ByteRange> IntegrityRanges(
        LegacyCombinerPostbuildCommandPlan plan,
        long capacity)
    {
        return
        [
            .. LegacyCombinerPostbuildPlanner.GetKnownIntegrityWriteRangeSections(plan, capacity)
                .Select(section => section.Range),
        ];
    }

    private static LegacyCombinerPostbuildCommandPlan
        CreateIntegrityFingerprintPlan(
            string modeArgument = "CRC_Disable",
            string stagedArtifactId = "artifact-a",
            long firmwareStart = 0x20,
            LegacyCombinerPostbuildAssemblyKind assemblyKind =
                LegacyCombinerPostbuildAssemblyKind.InPlaceFirmwareImage)
    {
        var command = new LegacyCombinerPostbuildCommand(
            "postbuild",
            LegacyCombinerCommandFamily.NormalMode,
            modeArgument,
            null,
            [
                new LegacyCombinerBlockArgument(
                    "ctrlram",
                    LegacyCombinerBlockSourceKind.StagedArtifact,
                    "ctrlram.bin",
                    0,
                    new ByteRange(firmwareStart, 4),
                    stagedArtifactId),
            ]);
        var profile = new LegacyCombinerPostbuildProfile(
            "processor",
            "NT51999",
            "tool-binding",
            "firmware.bin",
            [command],
            [command],
            "test evidence",
            assemblyKind: assemblyKind,
            firmwareConfigWriteRoute:
                LegacyCombinerFirmwareConfigWriteRoute.Unavailable);
        return LegacyCombinerPostbuildPlanner.CreatePlan(
            profile,
            profile.PlanSelectors.Single(static selector =>
                selector.Kind ==
                    LegacyCombinerPostbuildPlanSelectorKind.SingleChip));
    }


}
