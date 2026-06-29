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

    /// <summary>Verifies general replace mappings can infer the target region from an unambiguous range.</summary>
    [Fact]
    public void GeneralReplaceInfersMappingTargetRegionFromRange()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4));
        ExplicitMapping mapping = CreateMapping(ExplicitMappingOperationKind.ReplaceRange, targetRegionId: null);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, [mapping]);

        Assert.True(result.IsSuccess);
        CompositionOperation operation = Assert.Single(result.Plan!.OrderedOperations);
        Assert.Equal(new ByteRange(1, 2), operation.TargetRange);
    }

    /// <summary>Verifies general replace mappings cannot cross a protected region even within the output address space.</summary>
    [Fact]
    public void GeneralReplaceRejectsMappingThatOverlapsProtectedRegion()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4));
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.ReplaceRange,
            targetRange: new ByteRange(0, 2),
            targetRegionId: "payload");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, [mapping]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.explicit-mapping.range-outside-region");
        Assert.Contains(result.Issues, issue => issue.Code == "profile.explicit-mapping.protected-overlap");
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

    /// <summary>Verifies duplicate region ids return structured compile issues.</summary>
    [Fact]
    public void DuplicateRegionIdsFailWithStructuredIssue()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            regions:
            [
                new ProfileRegion(
                    "payload",
                    "output-image",
                    new ByteRange(0, 2),
                    RegionAtomicity.ExplicitMapping,
                    RegionWritePolicy.GeneralExplicit),
                new ProfileRegion(
                    "payload",
                    "output-image",
                    new ByteRange(2, 2),
                    RegionAtomicity.ExplicitMapping,
                    RegionWritePolicy.GeneralExplicit),
            ]);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.region.duplicate");
    }

    /// <summary>Verifies duplicate access rules return structured compile issues.</summary>
    [Fact]
    public void DuplicateAccessRulesFailWithStructuredIssue()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            accessRules:
            [
                new RegionAccessRule("payload", RegionAccessKind.ExplicitRange, "allow payload"),
                new RegionAccessRule("payload", RegionAccessKind.ExplicitRange, "duplicate payload"),
            ]);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.region-access.duplicate");
    }

    /// <summary>Verifies fixed experience profiles cannot add runtime address spaces.</summary>
    [Fact]
    public void FixedExperienceRejectsRuntimeAddressSpaces()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Merge,
            "standard-merge",
            ImageInitialization.Blank("output-image", 4, 0));

        ProfileCompileResult result = CompositionProfileCompiler.Compile(
            profile,
            [],
            [new AddressSpace("runtime-source", 4, AddressSpaceMutability.Immutable)]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.request-address-space.not-allowed");
    }

    /// <summary>Verifies profile operations are validated against region access policy.</summary>
    [Fact]
    public void FixedProfileOperationRejectsHiddenRegion()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "display-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            operations:
            [
                CompositionOperation.ReplaceRange(
                    "replace-header",
                    10,
                    "source",
                    new ByteRange(0, 1),
                    "output-image",
                    new ByteRange(0, 1),
                    OverlapPolicy.Reject,
                    "unsafe header replace"),
            ]);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.operation.region-not-enabled");
    }

    /// <summary>Verifies parent mappings cannot cross processor-owned child regions.</summary>
    [Fact]
    public void ExplicitMappingRejectsProcessorOwnedChildOverlap()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            regions:
            [
                new ProfileRegion(
                    "payload",
                    "output-image",
                    new ByteRange(0, 4),
                    RegionAtomicity.ExplicitMapping,
                    RegionWritePolicy.GeneralExplicit),
                new ProfileRegion(
                    "payload-crc",
                    "output-image",
                    new ByteRange(1, 1),
                    RegionAtomicity.Whole,
                    RegionWritePolicy.GeneralExplicit,
                    processorDependencyIds: ["crc-v1"]),
            ],
            accessRules:
            [
                new RegionAccessRule("payload", RegionAccessKind.ExplicitRange, "allow payload"),
                new RegionAccessRule("payload-crc", RegionAccessKind.ExplicitRange, "processor owned"),
            ]);
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.ReplaceRange,
            sourceRange: new ByteRange(0, 4),
            targetRange: new ByteRange(0, 4),
            targetRegionId: "payload");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, [mapping]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.explicit-mapping.protected-overlap");
    }

    private static CompositionProfileDefinition CreateProfile(
        CompositionKind compositionKind,
        string experienceId,
        ImageInitialization initialization,
        IReadOnlyList<AddressSpace>? addressSpaces = null,
        IReadOnlyList<CompositionOperation>? operations = null,
        IReadOnlyList<ProfileRegion>? regions = null,
        IReadOnlyList<RegionAccessRule>? accessRules = null)
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
            operations ?? [],
            regions ??
            [
                new ProfileRegion(
                    "header",
                    "output-image",
                    new ByteRange(0, 1),
                    RegionAtomicity.Whole,
                    RegionWritePolicy.Forbidden),
                new ProfileRegion(
                    "payload",
                    "output-image",
                    new ByteRange(1, 3),
                    RegionAtomicity.ExplicitMapping,
                    RegionWritePolicy.GeneralExplicit),
            ],
            accessRules ??
            [
                new RegionAccessRule("header", RegionAccessKind.Hidden, "protect header"),
                new RegionAccessRule("payload", RegionAccessKind.ExplicitRange, "allow general mapping"),
            ]);
    }

    private static ExplicitMapping CreateMapping(
        ExplicitMappingOperationKind operationKind,
        string sourceBindingId = "source",
        ByteRange? sourceRange = null,
        ByteRange? targetRange = null,
        int alignment = 1,
        string? targetRegionId = "payload")
    {
        ByteRange resolvedTargetRange = targetRange ?? new ByteRange(1, 2);
        return new ExplicitMapping(
            "mapping-1",
            10,
            operationKind,
            sourceBindingId,
            sourceRange ?? new ByteRange(0, resolvedTargetRange.Length),
            "output-image",
            resolvedTargetRange,
            OverlapPolicy.Reject,
            alignment,
            "compile explicit mapping",
            targetRegionId);
    }
}
