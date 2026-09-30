using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Original blocked facts remain renderable across the slot and pending memory surfaces.</summary>
public sealed class MemoryPendingDisplayTests
{
    /// <summary>Non-terminal prerequisite errors use readiness text, not terminal BIN-health formatting.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NonTerminalBlockerKeepsErrorCardAndPendingDetail(bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        var slot = new FirmwareSlotViewModel("dp", "DP BIN", "DP input", FirmwareSlotKind.Dp, addressSpaceId: "dp-input") { FilePath = "dp.bin" };
        var status = new AuthoringInputSlotStatus(
            new CapabilityRouteIdentity("NT-SYNTHETIC", ExperienceIds.StandardMerge, "none", "default"),
            new ResolutionToken("pending-test"), new AuthoringRevision(1), new string('a', 64), null,
            new InputSelectionMemberReadiness("dp-input", true, ResolvedChildReadiness.Blocked, false, "Unresolved prerequisite", null),
            "dp-input", null, null, null, "dp.bin");
        slot.ApplyExperienceText(text);
        FirmwareInspectionProjection.ApplyInputSlotInspection(slot, status, text);
        Assert.Same(status, slot.InputIssueStatus);
        Assert.True(Assert.IsType<IssueCardViewModel>(slot.IssueCard).IsError);
        (_, _, IReadOnlyList<MemoryCoverageSegmentViewModel> coverage) = UiCompositionRunner.GetPendingMemoryDisplay(
            text, [slot], MemoryPendingPrerequisite.DpBin);
        Assert.Equal(text.GetDpInputSelectionReadinessDetail(status.SelectionReadiness), Assert.Single(coverage).Detail);
        slot.FilePath = "different.bin";
        Assert.Null(slot.InputIssueStatus);
        Assert.Empty(slot.InputAuthoringIssues);
        Assert.Equal(MemoryInputAvailabilityIssue.None, slot.InputAvailabilityIssue);
    }
}
