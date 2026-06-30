using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests built-in synthetic Replace profiles used by CLI and UI wiring.</summary>
public sealed class BuiltInReplaceProfilesTests
{
    /// <summary>Verifies the built-in Replace catalog exposes the approved three-way taxonomy.</summary>
    [Fact]
    public void BuiltInReplaceProfilesExposeThreeWayTaxonomy()
    {
        Assert.Equal(
            ["dp-replace", "ctrlram-replace", "general-replace"],
            BuiltInReplaceProfiles.All.Select(profile => profile.ExperienceId));
    }

    /// <summary>Verifies fixed DP Replace compiles with short-input padding policy.</summary>
    [Fact]
    public void SyntheticDpReplaceCompiles()
    {
        CompositionProfileDefinition profile = BuiltInReplaceProfiles.SyntheticDpReplace;

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        Assert.Equal(IcNumberInputMode.SingleSelector, profile.IcNumberInputMode);
        Assert.Contains(profile.AddressSpaces, space => space.AddressSpaceId == "dp-replacement" && space.InputPaddingByte == 0xFF);
        CompositionOperation operation = Assert.Single(result.Plan!.OrderedOperations);
        Assert.Equal("replace-dp", operation.OperationId);
    }

    /// <summary>Verifies fixed CtrlRAM Replace compiles with oversized-input truncation policy.</summary>
    [Fact]
    public void SyntheticCtrlRamReplaceCompiles()
    {
        CompositionProfileDefinition profile = BuiltInReplaceProfiles.SyntheticCtrlRamReplace;

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

    /// <summary>Verifies General Replace compiles runtime explicit mappings into replace operations.</summary>
    [Fact]
    public void SyntheticGeneralReplaceCompilesExplicitMapping()
    {
        CompositionProfileDefinition profile = BuiltInReplaceProfiles.SyntheticGeneralReplace;
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
        CompositionOperation operation = Assert.Single(result.Plan!.OrderedOperations);
        Assert.Equal(CompositionOperationKind.ReplaceRange, operation.Kind);
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}"));
    }
}
