using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests profile compiler validation for the 0.2 core contract.</summary>
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

    /// <summary>Verifies initializer kind must match the approved merge versus replace semantics.</summary>
    [Fact]
    public void ReplaceExperienceRejectsBlankInitialization()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "dp-replace",
            ImageInitialization.Blank("output-image", 4, 0));

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.initialization-kind.mismatch");
    }

    /// <summary>Verifies typed profiles preserve IC number input modes for UI/request binding.</summary>
    [Theory]
    [InlineData(IcNumberInputMode.SingleSelector)]
    [InlineData(IcNumberInputMode.CascadeSelector)]
    [InlineData(IcNumberInputMode.NumericSelector)]
    public void ProfileDefinitionCarriesIcNumberInputMode(IcNumberInputMode mode)
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "dp-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            icNumberInputMode: mode);

        Assert.Equal(mode, profile.IcNumberInputMode);
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

    /// <summary>Verifies General Replace cannot map TP-classified ranges without a post-mapping processor refresh.</summary>
    [Fact]
    public void GeneralReplaceRejectsTpMappingWithoutExternalProcessor()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            regions:
            [
                new ProfileRegion(
                    "tp-payload",
                    "output-image",
                    new ByteRange(1, 2),
                    RegionAtomicity.ExplicitMapping,
                    RegionWritePolicy.GeneralExplicit,
                    classificationTags: ["tp"]),
            ],
            accessRules:
            [
                new RegionAccessRule("tp-payload", RegionAccessKind.ExplicitRange, "allow TP payload"),
            ]);
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.ReplaceRange,
            targetRegionId: "tp-payload");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, [mapping]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.explicit-mapping.tp-processor-required");
    }

    /// <summary>Verifies General Replace detects TP child ranges even when the explicit mapping targets a parent region.</summary>
    [Fact]
    public void GeneralReplaceRejectsParentMappingThatTouchesTpChildRegionWithoutExternalProcessor()
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
                    "tp-window",
                    "output-image",
                    new ByteRange(1, 2),
                    RegionAtomicity.ExplicitMapping,
                    RegionWritePolicy.GeneralExplicit,
                    classificationTags: ["tp"]),
            ],
            accessRules:
            [
                new RegionAccessRule("payload", RegionAccessKind.ExplicitRange, "allow payload"),
                new RegionAccessRule("tp-window", RegionAccessKind.ExplicitRange, "allow TP window"),
            ]);
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.ReplaceRange,
            targetRegionId: "payload");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, [mapping]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.explicit-mapping.tp-processor-required");
    }

    /// <summary>Verifies General Replace TP mappings compile only when an external processor follows the mapping.</summary>
    [Fact]
    public void GeneralReplaceTpMappingCompilesWithPostMappingExternalProcessor()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            operations: [CreateExternalProcessorOperation("crc-v1", sequence: 20)],
            regions:
            [
                new ProfileRegion(
                    "tp-payload",
                    "output-image",
                    new ByteRange(1, 2),
                    RegionAtomicity.ExplicitMapping,
                    RegionWritePolicy.GeneralExplicit,
                    classificationTags: ["tp"]),
                new ProfileRegion(
                    "tp-crc",
                    "output-image",
                    new ByteRange(3, 1),
                    RegionAtomicity.Whole,
                    RegionWritePolicy.Forbidden,
                    processorDependencyIds: ["crc-v1"],
                    classificationTags: ["tp"]),
            ],
            accessRules:
            [
                new RegionAccessRule("tp-payload", RegionAccessKind.ExplicitRange, "allow TP payload"),
                new RegionAccessRule("tp-crc", RegionAccessKind.Hidden, "processor owned"),
            ]);
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.ReplaceRange,
            targetRegionId: "tp-payload");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, [mapping]);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        Assert.Collection(
            result.Plan!.OrderedOperations,
            operation => Assert.Equal(CompositionOperationKind.ReplaceRange, operation.Kind),
            operation => Assert.Equal(CompositionOperationKind.RunExternalProcessor, operation.Kind));
    }

    /// <summary>Verifies a post-mapping processor does not unlock direct writes to processor-owned TP regions.</summary>
    [Fact]
    public void GeneralReplaceRejectsProcessorOwnedTpMappingEvenWithExternalProcessor()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            operations: [CreateExternalProcessorOperation("crc-v1", sequence: 20)],
            regions:
            [
                new ProfileRegion(
                    "tp-payload",
                    "output-image",
                    new ByteRange(1, 2),
                    RegionAtomicity.ExplicitMapping,
                    RegionWritePolicy.GeneralExplicit,
                    processorDependencyIds: ["crc-v1"],
                    classificationTags: ["tp"]),
                new ProfileRegion(
                    "tp-crc",
                    "output-image",
                    new ByteRange(3, 1),
                    RegionAtomicity.Whole,
                    RegionWritePolicy.Forbidden,
                    processorDependencyIds: ["crc-v1"],
                    classificationTags: ["tp"]),
            ],
            accessRules:
            [
                new RegionAccessRule("tp-payload", RegionAccessKind.ExplicitRange, "processor owned"),
                new RegionAccessRule("tp-crc", RegionAccessKind.Hidden, "processor owned"),
            ]);
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.ReplaceRange,
            targetRegionId: "tp-payload");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, [mapping]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.explicit-mapping.processor-dependency");
    }

    /// <summary>Verifies a pre-mapping processor does not satisfy General Replace TP CRC/header refresh.</summary>
    [Fact]
    public void GeneralReplaceRejectsTpMappingWhenExternalProcessorRunsBeforeMapping()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            operations: [CreateExternalProcessorOperation("crc-v1", sequence: 5)],
            regions:
            [
                new ProfileRegion(
                    "tp-payload",
                    "output-image",
                    new ByteRange(1, 2),
                    RegionAtomicity.ExplicitMapping,
                    RegionWritePolicy.GeneralExplicit,
                    classificationTags: ["tp"]),
                new ProfileRegion(
                    "tp-crc",
                    "output-image",
                    new ByteRange(3, 1),
                    RegionAtomicity.Whole,
                    RegionWritePolicy.Forbidden,
                    processorDependencyIds: ["crc-v1"],
                    classificationTags: ["tp"]),
            ],
            accessRules:
            [
                new RegionAccessRule("tp-payload", RegionAccessKind.ExplicitRange, "allow TP payload"),
                new RegionAccessRule("tp-crc", RegionAccessKind.Hidden, "processor owned"),
            ]);
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.ReplaceRange,
            targetRegionId: "tp-payload");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, [mapping]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.explicit-mapping.tp-processor-required");
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

    private static CompositionProfileDefinition CreateProfile(
        CompositionKind compositionKind,
        string experienceId,
        ImageInitialization initialization,
        IReadOnlyList<AddressSpace>? addressSpaces = null,
        IReadOnlyList<CompositionOperation>? operations = null,
        IReadOnlyList<ProfileRegion>? regions = null,
        IReadOnlyList<RegionAccessRule>? accessRules = null,
        IcNumberInputMode? icNumberInputMode = null)
    {
        AddressSpace[] defaultAddressSpaces =
        [
            new("source", 4, AddressSpaceMutability.Immutable),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        return new CompositionProfileDefinition(
            "demo-profile",
            "1.0.0",
            "NT-SYNTHETIC",
            "standard-merge",
            compositionKind,
            experienceId,
            "demo-output.bin",
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
            ],
            icNumberInputMode);
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

    private static CompositionOperation CreateExternalProcessorOperation(string processorId, int sequence = 10)
    {
        return CompositionOperation.RunExternalProcessor(
            "run-crc",
            sequence,
            "output-image",
            new ByteRange(0, 4),
            new ExternalProcessorInvocation(
                processorId,
                "tool-v1",
                [new ByteRange(0, 4)],
                [new ByteRange(3, 1)]),
            OverlapPolicy.Reject,
            "run synthetic crc processor");
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}"));
    }
}
