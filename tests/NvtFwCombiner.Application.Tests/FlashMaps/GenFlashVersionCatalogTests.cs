using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.FlashMaps;

/// <summary>Golden-backed checks for gen_flash contiguous DP main/sub version-byte rules.</summary>
public sealed class GenFlashVersionCatalogTests
{
    /// <summary>Guards DP version rules as contiguous main/sub byte pairs inside the declared DP range.</summary>
    [Fact]
    public void DpVersionRulesAreContiguousAndInsideDeclaredDpRange()
    {
        Assert.NotEmpty(GenFlashVersionCatalog.AllDpVersionRules);

        string[] ruleIds = [.. GenFlashVersionCatalog.AllDpVersionRules.Select(rule => rule.IcId)];
        Assert.Equal(ruleIds.Length, ruleIds.Distinct(StringComparer.Ordinal).Count());

        foreach (GenFlashDpVersionRule rule in GenFlashVersionCatalog.AllDpVersionRules)
        {
            Assert.Equal(rule.OutputDpStart + rule.InputRelativeOffset, rule.OutputMainAbsoluteAddress);
            Assert.Equal(rule.OutputMainAbsoluteAddress + 1, rule.OutputSubAbsoluteAddress);
            Assert.True(rule.OutputMainAbsoluteAddress >= rule.OutputDpStart);
            Assert.True(rule.OutputSubAbsoluteAddress < rule.OutputDpEndExclusive);
            Assert.True(GenFlashVersionCatalog.TryGetDpVersionRule($"NT{rule.IcId}", out GenFlashDpVersionRule resolved));
            Assert.Same(rule, resolved);
        }
    }

    /// <summary>Guards gen_flash DP version rules against drift from executable Standard Merge DP regions.</summary>
    [Fact]
    public void DpVersionRulesMatchStandardMergeDpRegions()
    {
        var profilesByIc = BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles
            .ToDictionary(profile => profile.IcId, StringComparer.Ordinal);

        foreach (GenFlashDpVersionRule rule in GenFlashVersionCatalog.AllDpVersionRules)
        {
            string icId = $"NT{rule.IcId}";
            Assert.True(profilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile), icId);

            ProfileRegion dpRegion = Assert.Single(profile.Regions, region => region.RegionId == "dp-region");
            Assert.Equal(rule.OutputDpStart, dpRegion.Range.Start);
            Assert.Equal(rule.OutputDpEndExclusive, dpRegion.Range.EndExclusive);
            Assert.True(rule.OutputMainAbsoluteAddress >= dpRegion.Range.Start, icId);
            Assert.True(rule.OutputSubAbsoluteAddress < dpRegion.Range.EndExclusive, icId);
        }
    }

    /// <summary>Prevents gen_flash Standard Merge onboarding from omitting the output-name DP version rule.</summary>
    [Fact]
    public void GenFlashStandardMergeProfilesHaveDpVersionRules()
    {
        foreach (CompositionProfileDefinition profile in BuiltInStandardMergeProfiles.ExecutableStandardMergeProfiles
                     .Where(profile => profile.ProfileId.Contains("gen-flash", StringComparison.Ordinal)))
        {
            Assert.True(
                GenFlashVersionCatalog.TryGetDpVersionRule(profile.IcId, out _),
                $"Missing gen_flash DP version rule for Standard Merge profile {profile.ProfileId} ({profile.IcId}).");
        }
    }

    /// <summary>Reads contiguous DP main/sub version bytes from owner-approved gen_flash standard-merge DP inputs.</summary>
    [Theory]
    [InlineData("51920", "0101")]
    [InlineData("51923", "8100")]
    [InlineData("51926", "0102")]
    [InlineData("51927", "5401")]
    [InlineData("51928", "8211")]
    [InlineData("51929", "0200")]
    [InlineData("51931", "8D60")]
    [InlineData("51932", "8201")]
    public void GoldenDpInputsExposeExpectedGenFlashVersion(string ic, string expectedToken)
    {
        byte[] image = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "standard-merge-gen-flash",
            "inputs",
            ic,
            "dp.bin"));

        Assert.True(GenFlashVersionCatalog.TryReadDpVersion(
            $"NT{ic}",
            image,
            out GenFlashDpVersionMetadata metadata));

        Assert.Equal(expectedToken, metadata.VersionToken);
        Assert.Equal($"D{expectedToken}", metadata.DisplayVersion);
    }

    /// <summary>Rejects truncated payloads that expose only the main DP version byte.</summary>
    [Fact]
    public void DpVersionRequiresContiguousMainAndSubBytes()
    {
        byte[] image = new byte[0x68];
        image[0x67] = 0x12;

        Assert.False(GenFlashVersionCatalog.TryReadDpVersion(
            "NT51929",
            image,
            out _));
    }

    /// <summary>ICs without gen_flash DP version evidence stay unclassified instead of guessing offsets.</summary>
    [Theory]
    [InlineData("NT51930")]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void MissingDpVersionEvidenceDoesNotCreateRule(string ic)
    {
        Assert.False(GenFlashVersionCatalog.TryGetDpVersionRule(ic, out _));
    }
}
