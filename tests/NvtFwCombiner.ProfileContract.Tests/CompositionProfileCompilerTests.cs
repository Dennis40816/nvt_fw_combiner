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

    /// <summary>Verifies explicit mapping range lengths must satisfy the declared alignment.</summary>
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

    /// <summary>Verifies general replace mappings are denied until a profile grants a target region policy.</summary>
    [Fact]
    public void GeneralReplaceRejectsMappingWithoutTargetPolicy()
    {
        CompositionProfileDefinition profile = CreateGeneralReplaceProfile();
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.ReplaceRange,
            sourceBindingId: "runtime-replacement",
            targetRange: new ByteRange(0, 2),
            targetRegionId: "slot-a");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(
            profile,
            [mapping],
            [new AddressSpace("runtime-replacement", 4, AddressSpaceMutability.Immutable)]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.explicit-mapping.region-policy.missing");
    }

    /// <summary>Verifies approved general replace target policies compile into replace-range operations.</summary>
    [Fact]
    public void GeneralReplaceAcceptsProfileApprovedTargetPolicy()
    {
        CompositionProfileDefinition profile = CreateGeneralReplaceProfile(
            [CreateTargetPolicy("slot-a", [new ByteRange(0, 2)])]);
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.ReplaceRange,
            sourceBindingId: "runtime-replacement",
            targetRange: new ByteRange(0, 2),
            targetRegionId: "slot-a");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(
            profile,
            [mapping],
            [new AddressSpace("runtime-replacement", 4, AddressSpaceMutability.Immutable)]);

        Assert.True(result.IsSuccess);
        CompositionOperation operation = Assert.Single(result.Plan!.OrderedOperations);
        Assert.Equal(CompositionOperationKind.ReplaceRange, operation.Kind);
    }

    /// <summary>Verifies target policies still reject protected bytes inside otherwise allowed ranges.</summary>
    [Fact]
    public void GeneralReplaceRejectsProtectedTargetRange()
    {
        CompositionProfileDefinition profile = CreateGeneralReplaceProfile(
            [CreateTargetPolicy("slot-a", [new ByteRange(0, 4)], [new ByteRange(1, 1)])]);
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.ReplaceRange,
            sourceBindingId: "runtime-replacement",
            targetRange: new ByteRange(0, 2),
            targetRegionId: "slot-a");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(
            profile,
            [mapping],
            [new AddressSpace("runtime-replacement", 4, AddressSpaceMutability.Immutable)]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.explicit-mapping.protected-range");
    }

    private static CompositionProfileDefinition CreateProfile(
        CompositionKind compositionKind,
        string experienceId,
        ImageInitialization initialization,
        IReadOnlyList<AddressSpace>? addressSpaces = null,
        IReadOnlyList<ExplicitMappingTargetPolicy>? explicitMappingTargetPolicies = null)
    {
        AddressSpace[] defaultAddressSpaces =
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
            addressSpaces ?? defaultAddressSpaces,
            [],
            explicitMappingTargetPolicies);
    }

    private static CompositionProfileDefinition CreateGeneralReplaceProfile(
        IReadOnlyList<ExplicitMappingTargetPolicy>? explicitMappingTargetPolicies = null)
    {
        AddressSpace[] addressSpaces =
        [
            new("reference-base", 4, AddressSpaceMutability.Immutable),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        return CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "reference-base", 4),
            addressSpaces,
            explicitMappingTargetPolicies);
    }

    private static ExplicitMappingTargetPolicy CreateTargetPolicy(
        string regionId,
        IReadOnlyList<ByteRange> allowedRanges,
        IReadOnlyList<ByteRange>? protectedRanges = null)
    {
        return new ExplicitMappingTargetPolicy(
            regionId,
            "output-image",
            allowedRanges,
            RegionAccessKind.ExplicitRange,
            RegionWritePolicy.GeneralExplicit,
            RegionAtomicity.ExplicitMapping,
            1,
            protectedRanges);
    }

    private static ExplicitMapping CreateMapping(
        ExplicitMappingOperationKind operationKind,
        string sourceBindingId = "source",
        ByteRange? sourceRange = null,
        ByteRange? targetRange = null,
        int alignment = 1,
        string? targetRegionId = null)
    {
        return new ExplicitMapping(
            "mapping-1",
            10,
            operationKind,
            sourceBindingId,
            sourceRange ?? new ByteRange(0, 2),
            "output-image",
            targetRange ?? new ByteRange(1, 2),
            OverlapPolicy.Reject,
            alignment,
            "compile explicit mapping",
            targetRegionId);
    }
}
