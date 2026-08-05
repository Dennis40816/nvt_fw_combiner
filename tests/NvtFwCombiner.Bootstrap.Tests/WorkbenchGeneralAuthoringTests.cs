using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Canonical General authoring boundary tests.</summary>
public sealed class WorkbenchGeneralAuthoringTests
{
    /// <summary>Non-range mapping invariants remain structured authoring issues.</summary>
    [Fact]
    public void GeneralMergeInvalidAlignmentReturnsIssue()
    {
        AuthoringMappingState state = CanonicalAuthoringAdapter.CreateGeneralMergeAuthoringState(
            "mapping-1",
            "input.bin",
            "0",
            "0",
            "1",
            alignment: 0);

        bool created = CanonicalAuthoringAdapter.TryCreateGeneralMergeAuthoringDraft(
            [state],
            out GeneralMappingDraftState? draft,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.False(created);
        Assert.Null(draft);
        Assert.Contains(issues, static issue => issue.Code == WorkbenchIssueCodes.GeneralMergeRangeInvalid);
    }

    /// <summary>Workflow-specific draft seams reject a typed operation from the other workflow.</summary>
    [Fact]
    public void GeneralMergeRejectsReplaceOperation()
    {
        var state = AuthoringMappingState.Create(
            "mapping-1",
            ExplicitMappingOperationKind.ReplaceRange,
            GeneralMappingSource.File("input.bin"),
            "0",
            "0",
            "1",
            CompositionAddressSpaceIds.OutputImage,
            OverlapPolicy.Reject,
            alignment: 1,
            "Replace explicit mapping.",
            fileRangePreset: GeneralMappingFileRangePreset.FromFileStart);

        bool created = CanonicalAuthoringAdapter.TryCreateGeneralMergeAuthoringDraft(
            [state],
            out GeneralMappingDraftState? draft,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.False(created);
        Assert.Null(draft);
        Assert.Contains(issues, static issue => issue.Message.Contains("CopyRange", StringComparison.Ordinal));
    }
}
