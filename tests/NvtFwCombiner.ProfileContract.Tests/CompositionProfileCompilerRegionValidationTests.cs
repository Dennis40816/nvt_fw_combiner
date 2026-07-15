using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class CompositionProfileCompilerTests
{
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
        CompositionOperation operation = Assert.Single(result.CompiledComposition!.Plan.OrderedOperations);
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
}
