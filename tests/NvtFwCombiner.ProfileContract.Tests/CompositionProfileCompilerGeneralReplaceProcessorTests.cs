using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class CompositionProfileCompilerTests
{
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
                new RegionAccessRule("tp-payload", RegionAccessKind.ExplicitRange),
            ]);
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.ReplaceRange);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, [mapping]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.explicit-mapping.tp-processor-required");
    }

    /// <summary>Verifies overlapping parent/child regions fail closed without caller-selected target authority.</summary>
    [Fact]
    public void GeneralReplaceRejectsAmbiguousParentAndChildTargetRegions()
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
                new RegionAccessRule("payload", RegionAccessKind.ExplicitRange),
                new RegionAccessRule("tp-window", RegionAccessKind.ExplicitRange),
            ]);
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.ReplaceRange);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, [mapping]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.explicit-mapping.target-region-ambiguous");
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
                new RegionAccessRule("tp-payload", RegionAccessKind.ExplicitRange),
                new RegionAccessRule("tp-crc", RegionAccessKind.Hidden),
            ]);
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.ReplaceRange);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, [mapping]);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
        Assert.Collection(
            result.CompiledComposition!.Plan.OrderedOperations,
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
                new RegionAccessRule("tp-payload", RegionAccessKind.ExplicitRange),
                new RegionAccessRule("tp-crc", RegionAccessKind.Hidden),
            ]);
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.ReplaceRange);

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
                new RegionAccessRule("tp-payload", RegionAccessKind.ExplicitRange),
                new RegionAccessRule("tp-crc", RegionAccessKind.Hidden),
            ]);
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.ReplaceRange);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, [mapping]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.explicit-mapping.tp-processor-required");
    }
}
