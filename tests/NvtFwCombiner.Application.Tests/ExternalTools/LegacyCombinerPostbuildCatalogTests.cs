using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
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

    /// <summary>Locks normal-mode source header CRC word writes outside explicit copy targets.</summary>
    [Fact]
    public void NormalModePlansDeclareKnownSourceHeaderIntegrityWrites()
    {
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51926,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        IReadOnlyList<ByteRange> ranges = IntegrityRanges(plan, 0x40000);

        Assert.Contains(new ByteRange(0x1C, 4), ranges);
        Assert.Contains(new ByteRange(0x3C, 4), ranges);
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

        IReadOnlyList<ByteRange> nt51932Ranges = IntegrityRanges(nt51932, 0x40000);
        IReadOnlyList<ByteRange> nt51950Ranges = IntegrityRanges(nt51950, 0x40000);
        IReadOnlyList<ByteRange> nt51930CommonFw1xRanges = IntegrityRanges(
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

        IReadOnlyList<ByteRange> twoChipRanges = IntegrityRanges(
            twoChip,
            0x40000);
        IReadOnlyList<ByteRange> threeChipRanges = IntegrityRanges(
            threeChip,
            0x40000);
        IReadOnlyList<ByteRange> cascadeRanges = IntegrityRanges(
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
            section.SectionId == TpHeaderSectionIds.HeaderCopyMaster &&
            section.Range == new ByteRange(0x1E230, 0x190) &&
            section.SourceRange == new ByteRange(0x200, 0x190));
        Assert.Contains(sections, section =>
            section.SectionId == TpHeaderSectionIds.HeaderCopyRight &&
            section.Range == new ByteRange(0x27230, 0x190) &&
            section.SourceRange == new ByteRange(0x200, 0x190));
        Assert.Contains(sections, section =>
            section.SectionId == TpHeaderSectionIds.HeaderCopyLeft &&
            section.Range == new ByteRange(0x30230, 0x190) &&
            section.SourceRange == new ByteRange(0x200, 0x190));
        Assert.Contains(sections, section =>
            section.SectionId == TpHeaderSectionIds.HeaderCopyFinalBackup &&
            section.Range == new ByteRange(0x32DC0, 0x460) &&
            section.SourceRange == new ByteRange(0x0000, 0x460));
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

        Assert.Equal(0x3F000, requiredCapacity);
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
            section.SectionId == TpHeaderSectionIds.HeaderCopy);
        Assert.Contains(sections, section =>
            section.Range == new ByteRange(0x1C, 4) &&
            section.SectionId == TpHeaderSectionIds.FlashHeaderCrc);
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
            section.SectionId == TpHeaderSectionIds.HeaderCopy);
        Assert.Contains(sections, section =>
            section.Range == new ByteRange(0xA11C, 4) &&
            section.SectionId == TpHeaderSectionIds.FlashHeaderCrc);
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

    /// <summary>Locks NT51930 Common FW 1.x cascade support to the approved DiffDLM branch.</summary>
    [Fact]
    public void Nt51930CascadeUsesLessOrEqual13IcDiffDlmLength()
    {
        LegacyCombinerPostbuildCommandPlan plan = LegacyCombinerPostbuildPlanner.CreatePlan(
            LegacyCombinerPostbuildCatalog.Nt51930CommonFw1x,
            new IcNumberSelection(IcNumberInputMode.CascadeSelector, ["cascade"]));

        LegacyCombinerBlockArgument diffBlock = plan.Commands
            .SelectMany(command => command.Blocks)
            .Single(block => block.SourceFileName == "DiffDLM.bin");

        Assert.Equal(new ByteRange(0x2F200, 65024), diffBlock.FirmwareRange);
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


}
