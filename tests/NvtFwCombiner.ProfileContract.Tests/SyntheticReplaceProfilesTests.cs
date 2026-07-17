using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests synthetic Replace definitions without exposing them from production catalogs.</summary>
public sealed class SyntheticReplaceProfilesTests
{
    /// <inheritdoc/>
    [Fact]
    public void SyntheticGeneralReplaceCompilesExplicitMapping()
    {
        CompositionProfileDefinition profile = SyntheticReplaceProfiles.General;
        var mapping = new ExplicitMapping(
            "replace-general",
            100,
            ExplicitMappingOperationKind.ReplaceRange,
            "replacement-input",
            new ByteRange(0, 2),
            "output-image",
            new ByteRange(1, 2),
            OverlapPolicy.Reject,
            alignment: 1,
            "replace payload",
            targetRegionId: null);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(
            profile,
            [mapping],
            [new AddressSpace("replacement-input", 2, AddressSpaceMutability.Immutable)]);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        Assert.Equal(IcNumberInputMode.SingleSelector, profile.IcNumberInputMode);
        CompositionOperation operation = Assert.Single(result.CompiledComposition!.Plan.OrderedOperations);
        Assert.Equal(CompositionOperationKind.ReplaceRange, operation.Kind);
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}"));
    }
}
