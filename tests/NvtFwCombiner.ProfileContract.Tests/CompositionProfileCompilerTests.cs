using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests profile compiler validation for the 0.2 core contract.</summary>
public sealed class CompositionProfileCompilerTests
{
    /// <summary>Verifies general merge explicit mappings compile into normal copy operations.</summary>
    [Fact]
    public void GeneralMergeExplicitMappingCompilesToPlanOperation()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Merge,
            "general-merge",
            ImageInitialization.Blank("output-image", 4, 0));
        ExplicitMapping mapping = CreateMapping(ExplicitMappingOperationKind.CopyRange);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, [mapping]);

        Assert.True(result.IsSuccess);
        CompositionOperation operation = Assert.Single(result.Plan!.OrderedOperations);
        Assert.Equal(CompositionOperationKind.CopyRange, operation.Kind);
        Assert.Equal("source", operation.SourceSpaceId);
    }

    /// <summary>Verifies fixed experiences cannot accept request-time explicit mappings.</summary>
    [Fact]
    public void FixedExperienceRejectsExplicitMappings()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Merge,
            "standard-merge",
            ImageInitialization.Blank("output-image", 4, 0));

        ProfileCompileResult result = CompositionProfileCompiler.Compile(
            profile,
            [CreateMapping(ExplicitMappingOperationKind.CopyRange)]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.explicit-mapping.not-allowed");
    }

    /// <summary>Verifies initializer kind must match the approved merge versus replace semantics.</summary>
    [Fact]
    public void ReplaceExperienceRejectsBlankInitialization()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "display-replace",
            ImageInitialization.Blank("output-image", 4, 0));

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.initialization-kind.mismatch");
    }

    /// <summary>Verifies explicit mapping operation kind must match profile composition kind.</summary>
    [Fact]
    public void MergeProfileRejectsReplaceMappingKind()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Merge,
            "general-merge",
            ImageInitialization.Blank("output-image", 4, 0));

        ProfileCompileResult result = CompositionProfileCompiler.Compile(
            profile,
            [CreateMapping(ExplicitMappingOperationKind.ReplaceRange)]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.plan.invalid");
    }

    private static CompositionProfileDefinition CreateProfile(
        CompositionKind compositionKind,
        string experienceId,
        ImageInitialization initialization)
    {
        AddressSpace[] addressSpaces =
        [
            new("source", 4, AddressSpaceMutability.Immutable),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        return new CompositionProfileDefinition(
            "demo-profile",
            "1.0.0",
            compositionKind,
            experienceId,
            initialization,
            addressSpaces,
            []);
    }

    private static ExplicitMapping CreateMapping(ExplicitMappingOperationKind operationKind)
    {
        return new ExplicitMapping(
            "mapping-1",
            10,
            operationKind,
            "source",
            new ByteRange(0, 2),
            "output-image",
            new ByteRange(1, 2),
            OverlapPolicy.Reject,
            1,
            "compile explicit mapping");
    }
}
