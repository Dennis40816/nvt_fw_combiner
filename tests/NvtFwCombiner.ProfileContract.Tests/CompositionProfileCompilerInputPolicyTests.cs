using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class CompositionProfileCompilerTests
{
    /// <summary>Verifies the compatibility compiler rejects profile-owned input padding.</summary>
    [Fact]
    public void InputPaddingRejectsProfileWithoutProcessorDependency()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            addressSpaces:
            [
                new("source", 4, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF),
                new("output-image", 4, AddressSpaceMutability.Mutable),
            ]);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.input-padding.processor-conflict");
    }

    /// <summary>Verifies profiles with processor-owned integrity keep exact input lengths.</summary>
    [Fact]
    public void InputPaddingRejectsProfileWithProcessorDependency()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            addressSpaces:
            [
                new("source", 4, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF),
                new("output-image", 4, AddressSpaceMutability.Mutable),
            ],
            operations: [CreateExternalProcessorOperation("crc-v1")],
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
        Assert.Contains(result.Issues, issue => issue.Code == "profile.input-padding.processor-conflict");
    }

    /// <summary>Verifies request-time inputs cannot add padding to processor-dependent profiles.</summary>
    [Fact]
    public void InputPaddingRejectsRuntimeAddressSpaceWithProcessorDependency()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            addressSpaces:
            [
                new("source", 4, AddressSpaceMutability.Immutable),
                new("output-image", 4, AddressSpaceMutability.Mutable),
            ],
            operations: [CreateExternalProcessorOperation("crc-v1")],
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

        ProfileCompileResult result = CompositionProfileCompiler.Compile(
            profile,
            [],
            [new AddressSpace("runtime-source", 4, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF)]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.input-padding.request-not-allowed");
    }

    /// <summary>Verifies request-time inputs cannot choose padding even without processor dependencies.</summary>
    [Fact]
    public void InputPaddingRejectsRuntimeAddressSpaceWithoutProcessorDependency()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4));
        ExplicitMapping mapping = CreateMapping(
            ExplicitMappingOperationKind.ReplaceRange,
            sourceBindingId: "runtime-source");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(
            profile,
            [mapping],
            [new AddressSpace("runtime-source", 4, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF)]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.input-padding.request-not-allowed");
    }

    /// <summary>Verifies CtrlRAM replace profiles do not use short-input padding.</summary>
    [Fact]
    public void InputPaddingRejectsCtrlRamReplaceProfile()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "ctrlram-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            addressSpaces:
            [
                new("source", 4, AddressSpaceMutability.Immutable),
                new("ctrlram-replacement", 4, AddressSpaceMutability.Immutable, inputPaddingByte: 0xFF),
                new("output-image", 4, AddressSpaceMutability.Mutable),
            ],
            operations: []);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.input-padding.processor-conflict");
    }

}
