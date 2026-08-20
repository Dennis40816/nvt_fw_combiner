using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Masked and full DiffDLM routes expose localized equivalent disclosure semantics.</summary>
    [Fact]
    public void DiffDlmCoverageFormatsPreservationAndFullArtifactStates()
    {
        var detail = new Application.MemoryLayout.MemoryLayoutPreservationDetail(
            "diff-nf-0",
            blockIndex: 0,
            Application.MemoryLayout.MemoryEndpointIdentity.NotApplicable,
            "postbuild-diffdlm",
            new ByteRange(0xB90, 0x870),
            new ByteRange(0x2DC90, 0x870));
        var masked = new MemoryCoverageSegmentViewModel(
            "0x2D100-0x2E4FF",
            "DiffDLM",
            "Canonical DiffDLM",
            MemoryCoverageFillRole.DiffDlm,
            20,
            preservationDetails: [detail],
            text: ShellTextResources.For(ShellLanguage.English));
        var localized = new MemoryCoverageSegmentViewModel(
            "0x2D100-0x2E4FF",
            "DiffDLM",
            "Canonical DiffDLM",
            MemoryCoverageFillRole.DiffDlm,
            20,
            preservationDetails: [detail],
            text: ShellTextResources.For(ShellLanguage.ChineseTraditional));
        var full = new MemoryCoverageSegmentViewModel(
            "0x27800-0x29FFF",
            "DiffDLM",
            "Canonical DiffDLM",
            MemoryCoverageFillRole.DiffDlm,
            20);
        var firstDetailedRange = new MemoryCoverageSegmentViewModel(
            "0x2D100-0x2E4FF",
            "DiffDLM",
            "First preserved range",
            MemoryCoverageFillRole.DiffDlm,
            20,
            sourceSlotId: "replace-ctrlram-diff",
            preservationDetails: [detail],
            rangeStart: 0x2D100,
            rangeEndExclusive: 0x2E500);
        var secondDetailedRange = new MemoryCoverageSegmentViewModel(
            "0x2E500-0x2E5FF",
            "DiffDLM",
            "Second preserved range",
            MemoryCoverageFillRole.DiffDlm,
            20,
            sourceSlotId: "replace-ctrlram-diff",
            preservationDetails: [detail],
            rangeStart: 0x2E500,
            rangeEndExclusive: 0x2E600);
        var detailedItem = new MemoryCoverageLogicalItemViewModel(
            "slot:replace-ctrlram-diff",
            [firstDetailedRange, secondDetailedRange],
            ShellTextResources.For(ShellLanguage.English));

        Assert.Equal("Kept 1 active Diff NF segments", masked.PreservationSummary);
        Assert.Contains("Block 0", masked.AccessibleDetail, StringComparison.Ordinal);
        Assert.True(masked.HasPreservationDetails);
        Assert.Contains("保留 1 個有效 Diff NF 區段", localized.PreservationSummary, StringComparison.Ordinal);
        Assert.Equal("Entire DiffDLM", full.PreservationSummary);
        Assert.False(full.HasPreservationDetails);
        Assert.Equal(2, detailedItem.Ranges.Count);
        Assert.All(detailedItem.Ranges, static range => Assert.True(range.HasPreservationDetails));
    }
}
