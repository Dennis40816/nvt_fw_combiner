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
        Assert.Equal(baseline.ProfileId, composition.ProfileId);
        Assert.Equal(baseline.CompositionKind, composition.CompositionKind);
        Assert.Equal(baseline.RequiredInputAddressSpaceIds, composition.Plan.RequiredInputAddressSpaceIds);
        Assert.Equal(baseline.DefaultOutputFileName, composition.DefaultOutputFileName);
        Assert.Equal(baseline.IcNumberPolicy, composition.IcNumberPolicy);
        Assert.Equal(
            new ByteRange(0x0A000, 0x2D000),
            composition.V2Details!.Provenance.ResolvedMap.ImageMap.Regions
                .Single(static region => region.RegionId == "tp-overlay").Range);
        Assert.Equal(
            new ByteRange(0x37000, 0x1000),
            composition.V2Details.Provenance.ResolvedMap.ImageMap.Regions
                .Single(static region => region.RegionId == "customer-info").Range);
    }

    /// <summary>DP Perspective profiles wait for a selected DP length instead of selecting a maximum container.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void DpPerspectiveProfileWithoutInputLengthRemainsPending(string icId)
    {
        bool succeeded = WorkbenchCompositionService.TryCompileStandardMerge(
            icId,
            dpInputLength: null,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.False(succeeded);
        Assert.Null(composition);
        Assert.Empty(issues);
    }

    /// <summary>An unsupported DP Perspective length returns a stable issue without an artifact.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void UnsupportedDpPerspectiveLengthDoesNotCompile(string icId)
    {
        bool succeeded = WorkbenchCompositionService.TryCompileStandardMerge(
            icId,
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

    /// <summary>Every selectable General Merge IC has a compiled V2 capacity source.</summary>
    [Fact]
    public void EverySelectableGeneralMergeIcHasACompiledV2CapacitySource()
    {
        foreach (string icId in WorkbenchCompositionService.GetSupportedIcIds())
        {
            Assert.True(WorkbenchCompositionService.IsStandardMergeSupported(icId));
            Assert.StartsWith(
                "0x",
                WorkbenchCompositionService.GetGeneralMergeDefaultOutputLength(icId),
                StringComparison.Ordinal);
        }
    }

    /// <summary>An unknown IC cannot obtain General Merge capacity from a compatibility catalog.</summary>
    [Fact]
    public void UnknownGeneralMergeIcHasNoCompatibilityCapacityFallback()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => WorkbenchCompositionService.GetGeneralMergeDefaultOutputLength("NT00000"));

        Assert.Contains("No compiled V2 Standard Merge profile", exception.Message, StringComparison.Ordinal);
    }

}
