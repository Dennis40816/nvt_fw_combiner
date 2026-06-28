using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests built-in synthetic standard merge profile evidence.</summary>
public sealed class BuiltInStandardMergeProfilesTests
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
        Assert.Equal(["copy-dp", "copy-tp"], result.Plan!.OrderedOperations.Select(operation => operation.OperationId));
        Assert.All(result.Plan.OrderedOperations, operation => Assert.Equal(OverlapPolicy.Reject, operation.OverlapPolicy));
    }
}
