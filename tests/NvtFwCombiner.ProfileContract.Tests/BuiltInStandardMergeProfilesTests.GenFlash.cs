using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

public sealed partial class BuiltInStandardMergeProfilesTests
{
    /// <summary>Verifies every gen_flash standard merge profile compiles with fixed profile operations.</summary>
    [Fact]
    public void GenFlashStandardMergeProfilesCompile()
    {
        string[] expectedIcIds =
        [
            "NT51920",
            "NT51923",
            "NT51926",
            "NT51927",
            "NT51928",
            "NT51929",
            "NT51931",
            "NT51932",
        ];

        Assert.Equal(expectedIcIds, BuiltInStandardMergeProfiles.GenFlashStandardMergeProfiles.Select(profile => profile.IcId));
        foreach (CompositionProfileDefinition profile in BuiltInStandardMergeProfiles.GenFlashStandardMergeProfiles)
        {
            ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

            Assert.True(result.IsSuccess);
            Assert.Equal("standard-merge", profile.ModeId);
            Assert.Equal("standard-merge", profile.ExperienceId);
            Assert.Equal(CompositionKind.Merge, profile.CompositionKind);
            Assert.Equal(ImageInitializationKind.Blank, profile.Initialization.Kind);
            Assert.All(result.CompiledComposition!.Plan.OrderedOperations, operation => Assert.Equal(OverlapPolicy.Reject, operation.OverlapPolicy));
        }
    }

    /// <summary>Verifies NT51928 carries the extra LD copy after TP and DP, matching gen_flash order.</summary>
    [Fact]
    public void GenFlashNt51928IncludesLdCopyAfterDp()
    {
        CompositionProfileDefinition profile = BuiltInStandardMergeProfiles.GenFlashStandardMergeProfiles
            .Single(item => item.IcId == "NT51928");

        ProfileCompileResult result = CompositionProfileCompiler.Compile(profile, []);

        Assert.True(result.IsSuccess);
        Assert.Equal(["copy-tp", "copy-dp", "copy-ld"], result.CompiledComposition!.Plan.OrderedOperations.Select(operation => operation.OperationId));
        Assert.Contains(profile.AddressSpaces, addressSpace => addressSpace.AddressSpaceId == "ld-input");
    }

    /// <summary>Verifies gen_flash DP inputs require only copied bytes while retaining golden sizes as warnings.</summary>
    [Fact]
    public void GenFlashStandardMergeProfilesDeclareRequiredRangesAndExpectedGoldenSizes()
    {
        Dictionary<string, Dictionary<string, long>> expectedRequiredLengths = new(StringComparer.Ordinal)
        {
            ["NT51920"] = new(StringComparer.Ordinal) { ["dp-input"] = 0x40000, ["tp-input"] = 0x30000 },
            ["NT51923"] = new(StringComparer.Ordinal) { ["dp-input"] = 0x40000, ["tp-input"] = 0x3C000 },
            ["NT51926"] = new(StringComparer.Ordinal) { ["dp-input"] = 0x40000, ["tp-input"] = 0x3C000 },
            ["NT51927"] = new(StringComparer.Ordinal) { ["dp-input"] = 0x40000, ["tp-input"] = 0x35000 },
            ["NT51928"] = new(StringComparer.Ordinal)
            {
                ["dp-input"] = 0x40000,
                ["ld-input"] = 0x80000,
                ["tp-input"] = 0x35000,
            },
            ["NT51929"] = new(StringComparer.Ordinal) { ["dp-input"] = 0x06000, ["tp-input"] = 0x40000 },
            ["NT51931"] = new(StringComparer.Ordinal) { ["dp-input"] = 0x40000, ["tp-input"] = 0x3C000 },
            ["NT51932"] = new(StringComparer.Ordinal) { ["dp-input"] = 0x06000, ["tp-input"] = 0x40000 },
        };
        Dictionary<string, long> expectedDpGoldenLengths = new(StringComparer.Ordinal)
        {
            ["NT51920"] = 0x40000,
            ["NT51923"] = 0x40000,
            ["NT51926"] = 0x40000,
            ["NT51927"] = 0x200000,
            ["NT51928"] = 0x80000,
            ["NT51929"] = 0x40000,
            ["NT51931"] = 0x80000,
            ["NT51932"] = 0x40000,
        };

        foreach (CompositionProfileDefinition profile in BuiltInStandardMergeProfiles.GenFlashStandardMergeProfiles)
        {
            var actualLengths = profile.AddressSpaces
                .Where(addressSpace => addressSpace.Mutability == AddressSpaceMutability.Immutable)
                .ToDictionary(
                    addressSpace => addressSpace.AddressSpaceId,
                    addressSpace => addressSpace.Length,
                    StringComparer.Ordinal);

            Assert.Equal(expectedRequiredLengths[profile.IcId], actualLengths);
            AddressSpace dpInput = Assert.Single(profile.AddressSpaces, addressSpace => addressSpace.AddressSpaceId == "dp-input");
            Assert.Equal(InputOversizePolicy.ExtractDeclaredRange, dpInput.InputOversizePolicy);
            Assert.Equal([expectedDpGoldenLengths[profile.IcId]], dpInput.ExpectedInputLengths);
            Assert.All(
                profile.AddressSpaces.Where(addressSpace => addressSpace.AddressSpaceId == "tp-input"),
                addressSpace => Assert.InRange(addressSpace.Length, 1, 0x40000));
        }
    }
}
