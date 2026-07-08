using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class CompositionProfileCompilerTests
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

    /// <summary>Verifies general replace mappings compile only after region policy allows the target range.</summary>
    [Fact]
    public void GeneralReplaceExplicitMappingCompilesInsideApprovedRegion()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4));
        ExplicitMapping mapping = CreateMapping(ExplicitMappingOperationKind.ReplaceRange);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, [mapping]);

        Assert.True(result.IsSuccess);
        CompositionOperation operation = Assert.Single(result.Plan!.OrderedOperations);
        Assert.Equal(CompositionOperationKind.ReplaceRange, operation.Kind);
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

    /// <summary>Verifies request-time source address spaces are included before explicit mappings are planned.</summary>
    [Fact]
    public void GeneralMergeAcceptsRequestSourceAddressSpace()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Merge,
            "general-merge",
            ImageInitialization.Blank("output-image", 4, 0),
            [new AddressSpace("output-image", 4, AddressSpaceMutability.Mutable)]);
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.CopyRange,
            sourceBindingId: "runtime-source");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(
            profile,
            [mapping],
            [new AddressSpace("runtime-source", 4, AddressSpaceMutability.Immutable)]);

        Assert.True(result.IsSuccess);
        CompositionOperation operation = Assert.Single(result.Plan!.OrderedOperations);
        Assert.Equal("runtime-source", operation.SourceSpaceId);
    }

    /// <summary>Verifies explicit mapping source and target lengths must satisfy the declared alignment.</summary>
    [Fact]
    public void ExplicitMappingLengthMustSatisfyAlignment()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Merge,
            "general-merge",
            ImageInitialization.Blank("output-image", 4, 0));
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.CopyRange,
            sourceRange: new ByteRange(0, 3),
            targetRange: new ByteRange(0, 3),
            alignment: 2);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, [mapping]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.explicit-mapping.alignment");
    }
}
