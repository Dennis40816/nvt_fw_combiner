using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class CompositionProfileCompilerTests
{
    /// <summary>Verifies CtrlRAM replace profiles may declare oversized-input truncation.</summary>
    [Fact]
    public void InputTruncationCompilesForCtrlRamReplaceProfile()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "ctrlram-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            addressSpaces:
            [
                new("source", 4, AddressSpaceMutability.Immutable),
                new("ctrlram-replacement", 4, AddressSpaceMutability.Immutable, inputOversizePolicy: InputOversizePolicy.TruncateWithWarning),
                new("output-image", 4, AddressSpaceMutability.Mutable),
            ],
            operations:
            [
                CompositionOperation.ReplaceRange(
                    "replace-ctrlram",
                    10,
                    "ctrlram-replacement",
                    new ByteRange(0, 3),
                    "output-image",
                    new ByteRange(1, 3),
                    OverlapPolicy.Reject,
                    "replace ctrlram"),
            ],
            regions:
            [
                new ProfileRegion(
                    "ctrlram",
                    "output-image",
                    new ByteRange(1, 3),
                    RegionAtomicity.ExplicitMapping,
                    RegionWritePolicy.GeneralExplicit,
                    classificationTags: ["tp-ctrlram"]),
            ],
            accessRules:
            [
                new RegionAccessRule("ctrlram", RegionAccessKind.ExplicitRange, "allow ctrlram"),
            ]);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
    }

    /// <summary>Verifies CtrlRAM Replace may stage a whole CtrlRAM region for its postbuild processor.</summary>
    [Fact]
    public void CtrlRamReplaceAllowsWholeRegionStagedSourceForProcessorDependency()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "ctrlram-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            addressSpaces:
            [
                new("source", 4, AddressSpaceMutability.Immutable),
                new("ctrlram-replacement", 2, AddressSpaceMutability.Immutable, inputOversizePolicy: InputOversizePolicy.TruncateWithWarning),
                new("output-image", 4, AddressSpaceMutability.Mutable),
            ],
            operations:
            [
                CompositionOperation.RunExternalProcessor(
                    "postbuild-ctrlram",
                    10,
                    "output-image",
                    new ByteRange(0, 4),
                    new ExternalProcessorInvocation(
                        "legacy-combiner",
                        "legacy-combiner-1.13.0",
                        [new ByteRange(0, 4)],
                        [new ByteRange(1, 2)],
                        [
                            new ExternalProcessorStagedSourceBinding(
                                "ctrlram-replacement",
                                new ByteRange(0, 2),
                                new ByteRange(1, 2)),
                        ]),
                    OverlapPolicy.ReplaceExisting,
                    "stage ctrlram for combiner pasteback"),
            ],
            regions:
            [
                new ProfileRegion(
                    "ctrlram",
                    "output-image",
                    new ByteRange(1, 2),
                    RegionAtomicity.Whole,
                    RegionWritePolicy.WholeOnly,
                    processorDependencyIds: ["legacy-combiner"],
                    classificationTags: ["tp-ctrlram"]),
            ],
            accessRules:
            [
                new RegionAccessRule("ctrlram", RegionAccessKind.Whole, "allow whole ctrlram replacement"),
            ]);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess, FormatIssues(result.Issues));
    }

    /// <summary>Verifies CtrlRAM truncation must target an explicitly tagged CtrlRAM region.</summary>
    [Fact]
    public void InputTruncationRejectsCtrlRamReplaceNonCtrlRamRegion()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "ctrlram-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            addressSpaces:
            [
                new("source", 4, AddressSpaceMutability.Immutable),
                new("replacement", 4, AddressSpaceMutability.Immutable, inputOversizePolicy: InputOversizePolicy.TruncateWithWarning),
                new("output-image", 4, AddressSpaceMutability.Mutable),
            ],
            operations:
            [
                CompositionOperation.ReplaceRange(
                    "replace-non-ctrlram",
                    10,
                    "replacement",
                    new ByteRange(0, 3),
                    "output-image",
                    new ByteRange(1, 3),
                    OverlapPolicy.Reject,
                    "replace non-ctrlram"),
            ]);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.input-truncation.ctrlram-region-required");
    }

    /// <summary>Verifies non-CtrlRAM profiles cannot opt in to oversized-input truncation.</summary>
    [Fact]
    public void InputTruncationRejectsNonCtrlRamProfile()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "dp-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            addressSpaces:
            [
                new("source", 4, AddressSpaceMutability.Immutable),
                new("dp-replacement", 4, AddressSpaceMutability.Immutable, inputOversizePolicy: InputOversizePolicy.TruncateWithWarning),
                new("output-image", 4, AddressSpaceMutability.Mutable),
            ]);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.input-truncation.not-allowed");
    }

    /// <summary>Verifies declared-range extraction cannot become a generic oversize-input bypass.</summary>
    [Fact]
    public void InputExtractionRejectsNonStandardMergeDpInput()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "dp-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            addressSpaces:
            [
                new("source", 4, AddressSpaceMutability.Immutable),
                new(
                    "dp-input",
                    4,
                    AddressSpaceMutability.Immutable,
                    inputOversizePolicy: InputOversizePolicy.ExtractDeclaredRange,
                    expectedInputLengths: [4]),
                new("output-image", 4, AddressSpaceMutability.Mutable),
            ]);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.input-extraction.not-allowed");
        Assert.Contains(result.Issues, issue => issue.Code == "profile.expected-input-lengths.not-allowed");
    }

    /// <summary>Verifies request-time address spaces cannot choose truncation policy.</summary>
    [Fact]
    public void InputTruncationRejectsRuntimeAddressSpace()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Merge,
            "general-merge",
            ImageInitialization.Blank("output-image", 4, 0),
            addressSpaces:
            [
                new("output-image", 4, AddressSpaceMutability.Mutable),
            ]);
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.CopyRange,
            sourceBindingId: "runtime-source");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(
            profile,
            [mapping],
            [new AddressSpace(
                "runtime-source",
                4,
                AddressSpaceMutability.Immutable,
                inputOversizePolicy: InputOversizePolicy.TruncateWithWarning)]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.input-truncation.request-not-allowed");
    }
}
