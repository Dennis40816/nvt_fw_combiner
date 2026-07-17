using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class CompositionProfileCompilerTests
{
    /// <summary>Verifies CtrlRAM compatibility cannot compile without its staged postbuild processor.</summary>
    [Fact]
    public void CtrlRamReplaceRequiresStagedPostbuildProcessor()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "ctrlram-replace",
            ImageInitialization.Reference("output-image", "source", 4));

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "profile.ctrlram-replace.staged-processor-required");
    }

    /// <summary>Verifies an unstaged processor cannot stand in for CtrlRAM postbuild pasteback.</summary>
    [Fact]
    public void CtrlRamReplaceRejectsUnstagedProcessor()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "ctrlram-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            operations: [CreateExternalProcessorOperation("crc-v1")],
            regions:
            [
                new ProfileRegion(
                    "ctrlram",
                    "output-image",
                    new ByteRange(3, 1),
                    RegionAtomicity.Whole,
                    RegionWritePolicy.WholeOnly,
                    processorDependencyIds: ["crc-v1"],
                    classificationTags: ["tp-ctrlram"]),
            ],
            accessRules:
            [
                new RegionAccessRule("ctrlram", RegionAccessKind.Whole, "allow whole CtrlRAM replacement"),
            ]);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "profile.ctrlram-replace.staged-processor-required");
    }

    /// <summary>Verifies staged postbuild sources cannot target a non-CtrlRAM region.</summary>
    [Fact]
    public void CtrlRamReplaceRejectsStagedNonCtrlRamRegion()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "ctrlram-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            operations:
            [
                CompositionOperation.RunExternalProcessor(
                    "postbuild",
                    10,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        "crc-v1",
                        "tool-v1",
                        [new ByteRange(0, 4)],
                        [new ByteRange(3, 1)],
                        [new ExternalProcessorStagedSourceBinding(
                            "source",
                            new ByteRange(0, 1),
                            new ByteRange(3, 1))]),
                    OverlapPolicy.ReplaceExisting,
                    "stage a non-CtrlRAM source"),
            ],
            regions:
            [
                new ProfileRegion(
                    "payload",
                    "output-image",
                    new ByteRange(3, 1),
                    RegionAtomicity.Whole,
                    RegionWritePolicy.WholeOnly,
                    processorDependencyIds: ["crc-v1"]),
            ],
            accessRules:
            [
                new RegionAccessRule("payload", RegionAccessKind.Whole, "synthetic non-CtrlRAM payload"),
            ]);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "profile.ctrlram-replace.staged-source-region-required");
    }

    /// <summary>Verifies direct fixed ReplaceRange operations cannot coexist with the staged Replace model.</summary>
    [Fact]
    public void FixedReplaceRangeOperationIsRetired()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "ctrlram-replace",
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
        Assert.Contains(result.Issues, issue => issue.Code == "profile.operation.kind-retired");
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
            targetRange: new ByteRange(0, 4));

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, [mapping]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.explicit-mapping.protected-overlap");
    }

    /// <summary>Verifies external processor operations may write only regions owned by the declared processor.</summary>
    [Fact]
    public void ExternalProcessorOperationCompilesForProcessorOwnedRegion()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            operations: [CreateExternalProcessorOperation("crc-v1")],
            regions:
            [
                new ProfileRegion(
                    "payload",
                    "output-image",
                    new ByteRange(0, 3),
                    RegionAtomicity.ExplicitMapping,
                    RegionWritePolicy.GeneralExplicit),
                new ProfileRegion(
                    "payload-crc",
                    "output-image",
                    new ByteRange(3, 1),
                    RegionAtomicity.Whole,
                    RegionWritePolicy.Forbidden,
                    processorDependencyIds: ["crc-v1"]),
            ],
            accessRules:
            [
                new RegionAccessRule("payload", RegionAccessKind.ExplicitRange, "payload"),
                new RegionAccessRule("payload-crc", RegionAccessKind.Hidden, "processor owned"),
            ]);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess);
        CompositionOperation operation = Assert.Single(result.CompiledComposition!.Plan.OrderedOperations);
        Assert.Equal(CompositionOperationKind.RunExternalProcessor, operation.Kind);
    }

    /// <summary>Verifies external processor write ranges fail when the target region is owned by a different processor.</summary>
    [Fact]
    public void ExternalProcessorOperationRejectsUnownedRegion()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            operations: [CreateExternalProcessorOperation("wrong-processor")],
            regions:
            [
                new ProfileRegion(
                    "payload-crc",
                    "output-image",
                    new ByteRange(3, 1),
                    RegionAtomicity.Whole,
                    RegionWritePolicy.Forbidden,
                    processorDependencyIds: ["crc-v1"]),
            ],
            accessRules:
            [
                new RegionAccessRule("payload-crc", RegionAccessKind.Hidden, "processor owned"),
            ]);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.external-processor.region-not-owned");
    }

}
