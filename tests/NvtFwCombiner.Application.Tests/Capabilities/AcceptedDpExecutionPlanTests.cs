using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Capabilities;

/// <summary>Contracts for the immutable DP execution selection retained by one compilation.</summary>
public sealed class AcceptedDpExecutionPlanTests
{
    /// <summary>The execution plan snapshots the compiler-compatible IC-number selection.</summary>
    [Fact]
    public void IcNumberSelectionIsDefensivelySnapshotted()
    {
        var parts = new List<string> { IcNumberSelectionTokens.SingleChip };
        var selection = new IcNumberSelection(
            IcNumberInputMode.SingleSelector,
            parts);

        var plan = new AcceptedDpExecutionPlan(selection);
        parts[0] = "changed";

        Assert.Equal(
            IcNumberSelectionTokens.SingleChip,
            Assert.Single(plan.IcNumberSelection.Parts));
    }
}
