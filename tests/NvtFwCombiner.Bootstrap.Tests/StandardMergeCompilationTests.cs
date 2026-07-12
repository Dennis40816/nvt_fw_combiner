using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Tests the shared Standard Merge compatibility compilation boundary.</summary>
public sealed class StandardMergeCompilationTests
{
    /// <summary>A normal IC compiles to its existing atomic artifact identity and output capacity.</summary>
    [Fact]
    public void NormalProfileCompilesThroughSharedResolver()
    {
        bool succeeded = WorkbenchCompositionService.TryCompileStandardMerge(
            "NT51920",
            dpInputLength: null,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(succeeded);
        Assert.Empty(issues);
        Assert.NotNull(composition);
        Assert.Equal("nt51920-standard-merge-gen-flash", composition.ProfileId);
        Assert.Equal(0x40000, composition.Plan.OutputInitialization.Capacity);
        Assert.Equal(["dp-input", "tp-input"], composition.Plan.RequiredInputAddressSpaceIds);
    }

    /// <summary>DP Perspective specialization compiles the selected container length without changing identity.</summary>
    [Theory]
    [InlineData("NT51950", 0x40000)]
    [InlineData("NT51950", 0x80000)]
    [InlineData("NT51950", 0x100000)]
    [InlineData("NT51951", 0x40000)]
    [InlineData("NT51951", 0x80000)]
    [InlineData("NT51951", 0x100000)]
    public void DpPerspectiveProfileUsesSelectedInputLength(string icId, long dpInputLength)
    {
        bool succeeded = WorkbenchCompositionService.TryCompileStandardMerge(
            icId,
            dpInputLength,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(succeeded);
        Assert.Empty(issues);
        Assert.NotNull(composition);
        Assert.Equal(icId, composition.IcId);
        Assert.EndsWith("-standard-merge-dp-perspective", composition.ProfileId, StringComparison.Ordinal);
        Assert.Equal(dpInputLength, composition.Plan.OutputInitialization.Capacity);
        WorkbenchProfileSummary baseline = WorkbenchCompositionService.GetStandardMergeProfileSummaries()
            .Single(summary => string.Equals(summary.IcId, icId, StringComparison.Ordinal));
        Assert.Equal(baseline.RequiredInputAddressSpaceIds, composition.Plan.RequiredInputAddressSpaceIds);
    }

    /// <summary>An unsupported DP Perspective length returns a stable issue without an artifact.</summary>
    [Fact]
    public void UnsupportedDpPerspectiveLengthDoesNotCompile()
    {
        bool succeeded = WorkbenchCompositionService.TryCompileStandardMerge(
            "NT51950",
            0x40001,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.False(succeeded);
        Assert.Null(composition);
        CompositionIssue issue = Assert.Single(issues);
        Assert.Equal(WorkbenchIssueCodes.StandardMergeDpLengthUnsupported, issue.Code);
        Assert.Contains("0x40001", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>An unknown IC never produces an executable artifact.</summary>
    [Fact]
    public void UnknownIcDoesNotCompile()
    {
        bool succeeded = WorkbenchCompositionService.TryCompileStandardMerge(
            "NT00000",
            dpInputLength: null,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.False(succeeded);
        Assert.Null(composition);
        Assert.Empty(issues);
    }

    /// <summary>General Merge defaults retain representative compiled Standard Merge capacities.</summary>
    [Theory]
    [InlineData("NT51920", "0x40000")]
    [InlineData("NT51950", "0x100000")]
    [InlineData("NT51951", "0x100000")]
    public void GeneralMergeDefaultLengthUsesCompiledOutputCapacity(string icId, string expectedLength)
    {
        Assert.Equal(expectedLength, WorkbenchCompositionService.GetGeneralMergeDefaultOutputLength(icId));
    }

    /// <summary>DP warning policy is projected from the compiled immutable input address space.</summary>
    [Fact]
    public void DpInputLengthPolicyUsesCompiledAddressSpaceFacts()
    {
        bool found = WorkbenchCompositionService.TryGetStandardMergeDpInputLengthPolicy(
            "NT51929",
            out WorkbenchStandardMergeDpInputLengthPolicy policy);

        Assert.True(found);
        Assert.Equal(0x6000, policy.RequiredLength);
        Assert.Equal([0x40000], policy.ExpectedInputLengths);
        Assert.False(WorkbenchCompositionService.TryGetStandardMergeDpInputLengthPolicy("NT51950", out _));
        Assert.False(WorkbenchCompositionService.TryGetStandardMergeDpInputLengthPolicy("NT00000", out _));
    }
}
