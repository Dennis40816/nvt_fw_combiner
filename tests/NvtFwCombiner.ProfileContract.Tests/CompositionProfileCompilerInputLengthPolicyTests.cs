using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class CompositionProfileCompilerTests
{
    /// <summary>Verifies compatibility profiles cannot retain V1-owned input-length policy.</summary>
    [Fact]
    public void ProfileOwnedInputLengthsAreRetired()
    {
        CompositionProfileDefinition profile = CreateProfile(
            CompositionKind.Replace,
            "general-replace",
            ImageInitialization.Reference("output-image", "source", 4),
            addressSpaces:
            [
                new("source", 4, AddressSpaceMutability.Immutable, allowedInputLengths: [4]),
                new("output-image", 4, AddressSpaceMutability.Mutable),
            ]);

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.input-lengths.not-allowed");
    }

    /// <summary>Verifies request-time address spaces cannot choose allowed input length policy.</summary>
    [Fact]
    public void AllowedInputLengthsRejectRuntimeAddressSpace()
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
            [new AddressSpace(
                "runtime-source",
                4,
                AddressSpaceMutability.Immutable,
                allowedInputLengths: [2, 4])]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.input-lengths.request-not-allowed");
    }

    /// <summary>Verifies request-time mappings cannot weaken artifact validation with advisory input lengths.</summary>
    [Fact]
    public void ExpectedInputLengthsRejectRuntimeAddressSpace()
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
            [new AddressSpace(
                "runtime-source",
                4,
                AddressSpaceMutability.Immutable,
                expectedInputLengths: [4])]);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.expected-input-lengths.request-not-allowed");
    }
}
