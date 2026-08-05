using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Checks the explicit postbuild write-section contract used by plans and reports.</summary>
public sealed class PostbuildWriteSectionTests
{
    /// <summary>Section ids remain unique, stable report keys.</summary>
    [Fact]
    public void SectionIdsAreUnique()
    {
        Assert.Equal(
            PostbuildWriteSectionSemantics.KnownSectionIds.Count,
            PostbuildWriteSectionSemantics.KnownSectionIds
                .Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Report labels and overlap precedence are shared without IC-specific geometry.</summary>
    [Fact]
    public void PresentationAndOverlapSemanticsAreStable()
    {
        Assert.Equal(
            "TP flash header / CRC fields",
            PostbuildWriteSectionSemantics.GetDisplayName(
                PostbuildWriteSectionIds.FlashHeaderCrc));
        Assert.Equal(
            "Header copy / final backup",
            PostbuildWriteSectionSemantics.GetDisplayName(
                PostbuildWriteSectionIds.HeaderCopyFinalBackup));
        Assert.Equal(
            "Postbuild write range",
            PostbuildWriteSectionSemantics.GetDisplayName("unknown-section"));
        Assert.True(
            PostbuildWriteSectionSemantics.GetOverlapPriority(
                PostbuildWriteSectionIds.FlashHeaderCrc) >
            PostbuildWriteSectionSemantics.GetOverlapPriority(
                PostbuildWriteSectionIds.HeaderCopy));
        Assert.True(
            PostbuildWriteSectionSemantics.GetOverlapPriority(
                PostbuildWriteSectionIds.HeaderCopy) >
            PostbuildWriteSectionSemantics.GetOverlapPriority(
                PostbuildWriteSectionIds.CtrlRamReplacement));
    }

    /// <summary>Every trusted block carries its section explicitly instead of being classified from its name.</summary>
    [Fact]
    public void EveryTrustedPostbuildBlockDeclaresKnownSection()
    {
        foreach (LegacyCombinerPostbuildCommandPlan plan in AllPostbuildPlans())
        {
            Assert.All(
                plan.Commands.SelectMany(static command => command.Blocks),
                block => Assert.Contains(
                    block.SectionId,
                    PostbuildWriteSectionSemantics.KnownSectionIds));
        }
    }

    /// <summary>Every normalized allowed range retains one declared report section.</summary>
    [Fact]
    public void PlannerWriteRangesUseDeclaredSections()
    {
        foreach (LegacyCombinerPostbuildCommandPlan plan in AllPostbuildPlans())
        {
            ByteRange[] stagedRanges =
            [
                .. LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(plan)
                    .Select(static block => block.FirmwareRange),
            ];
            long capacity = LegacyCombinerPostbuildPlanner.CalculateRequiredCapacity(
                plan,
                stagedRanges);
            ExternalProcessorWriteRangeSection[] sections =
            [
                .. LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForStagedSources(
                    plan,
                    capacity,
                    stagedRanges,
                    stagedRanges),
                .. LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForInPlaceRefresh(
                    plan,
                    capacity),
            ];

            Assert.NotEmpty(sections);
            Assert.All(sections, section => Assert.Contains(
                section.SectionId,
                PostbuildWriteSectionSemantics.KnownSectionIds));
        }
    }

    private static IEnumerable<LegacyCombinerPostbuildCommandPlan> AllPostbuildPlans()
    {
        foreach ((LegacyCombinerPostbuildProfile profile, IcNumberSelection selection) in
                 PostbuildSelectionTestCases.AllProfileBranchSelections())
        {
            yield return LegacyCombinerPostbuildPlanner.CreatePlan(profile, selection);
        }
    }
}
