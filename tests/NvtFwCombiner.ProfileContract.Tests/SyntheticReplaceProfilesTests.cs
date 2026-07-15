using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests synthetic Replace definitions without exposing them from production catalogs.</summary>
public sealed class SyntheticReplaceProfilesTests
{
    /// <inheritdoc/>
    [Fact]
    public void SyntheticDpReplaceCompiles()
    {
        CompositionProfileDefinition profile = SyntheticReplaceProfiles.Dp;

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        Assert.Equal(IcNumberInputMode.SingleSelector, profile.IcNumberInputMode);
        Assert.Contains(profile.AddressSpaces, space => space.AddressSpaceId == "dp-replacement" && space.InputPaddingByte == 0xFF);
        Assert.Contains(profile.AddressSpaces, space => space.AddressSpaceId == "ld-replacement" && space.InputPaddingByte == 0xFF);
        Assert.Equal(["replace-dp", "replace-ld"], result.CompiledComposition!.Plan.OrderedOperations.Select(operation => operation.OperationId));
        Assert.Contains(profile.Regions, region => region.RegionId == "ld" && region.ClassificationTags.Contains("ld", StringComparer.Ordinal));
    }

    /// <inheritdoc/>
    [Fact]
    public void SyntheticCtrlRamReplaceCompiles()
    {
        CompositionProfileDefinition profile = SyntheticReplaceProfiles.CtrlRam;

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        Assert.Equal(IcNumberInputMode.CascadeSelector, profile.IcNumberInputMode);
        Assert.Contains(
            profile.AddressSpaces,
            space => space.AddressSpaceId == "ctrlram-replacement" &&
                space.InputOversizePolicy == InputOversizePolicy.TruncateWithWarning);
        Assert.Contains(
            profile.Regions,
            region => region.ClassificationTags.Contains("tp-ctrlram", StringComparer.Ordinal));
    }

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
