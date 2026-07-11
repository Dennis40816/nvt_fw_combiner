using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class BuiltInStandardMergeProfilesTests
{
    /// <summary>Verifies the synthetic standard merge profile compiles into two ordered copy operations.</summary>
    [Fact]
    public void SyntheticStandardMergeCompilesToDpThenTpCopyPlan()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.SyntheticStandardMerge;

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess);
        Assert.Equal("synthetic-standard-merge", profile.ProfileId);
        Assert.Equal("NT-SYNTHETIC", profile.IcId);
        Assert.Equal("standard-merge", profile.ModeId);
        Assert.Equal("standard-merge", profile.ExperienceId);
        Assert.Equal(CompositionKind.Merge, profile.CompositionKind);
        Assert.Equal(["copy-dp", "copy-tp"], result.CompiledComposition!.Plan.OrderedOperations.Select(operation => operation.OperationId));
        Assert.All(result.CompiledComposition!.Plan.OrderedOperations, operation => Assert.Equal(OverlapPolicy.Reject, operation.OverlapPolicy));
    }

    /// <summary>Verifies NT51930 Standard Merge uses the flash-map dynamic layout ranges.</summary>
    [Fact]
    public void FlashMapNt51930UsesDynamicDpAndTpRanges()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.FlashMapStandardMergeProfiles
            .Single(item => item.IcId == "NT51930");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        Assert.Equal("nt51930-standard-merge-flashmap", profile.ProfileId);
        Assert.Equal(0x40000, profile.Initialization.Capacity);
        Assert.Equal(
            ["copy-tp", "copy-dp"],
            result.CompiledComposition!.Plan.OrderedOperations.Select(operation => operation.OperationId));
        Assert.Contains(result.CompiledComposition!.Plan.OrderedOperations, operation =>
            operation.OperationId == "copy-tp" &&
            operation.SourceRange == ByteRange.FromStartEndExclusive(0x07000, 0x40000) &&
            operation.TargetRange == ByteRange.FromStartEndExclusive(0x07000, 0x40000));
        Assert.Contains(result.CompiledComposition!.Plan.OrderedOperations, operation =>
            operation.OperationId == "copy-dp" &&
            operation.SourceRange == ByteRange.FromStartEndExclusive(0x00000, 0x06000) &&
            operation.TargetRange == ByteRange.FromStartEndExclusive(0x00000, 0x06000));
    }
}
