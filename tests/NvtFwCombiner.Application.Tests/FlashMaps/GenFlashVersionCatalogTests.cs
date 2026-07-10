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

    /// <summary>Guards CMI DP register triples against offset or expected-payload-length drift.</summary>
    [Fact]
    public void CmiDpCodeRulesFitInsideExpectedPayloadLengths()
    {
        Assert.NotEmpty(GenFlashVersionCatalog.AllCmiDpCodeRules);

        string[] ruleIds = [.. GenFlashVersionCatalog.AllCmiDpCodeRules.Select(rule => rule.IcId)];
        Assert.Equal(ruleIds.Length, ruleIds.Distinct(StringComparer.Ordinal).Count());

        foreach (CmiDpCodeRule rule in GenFlashVersionCatalog.AllCmiDpCodeRules)
        {
            Assert.True(rule.Register16Offset >= 0);
            Assert.NotEmpty(rule.ExpectedPayloadLengths);
            Assert.Equal(
                rule.ExpectedPayloadLengths.Count,
                rule.ExpectedPayloadLengths.Distinct().Count());
            Assert.All(rule.ExpectedPayloadLengths, length =>
            {
                Assert.True(length > 0);
                Assert.True(rule.Register16Offset + 2 < length);
            });
            Assert.True(GenFlashVersionCatalog.TryGetCmiDpCodeRule($"NT{rule.IcId}", out CmiDpCodeRule resolved));
            Assert.Same(rule, resolved);
        }
    }

    /// <summary>Reads the owner-confirmed NT51950 CMI registers without conflating Jira and DP version.</summary>
    [Fact]
    public void GoldenNt51950DpInputExposesCmiJiraAndDpVersion()
    {
        byte[] image = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "standard-merge-gen-flash",
            "inputs",
            "51950",
            "dp-256k",
            "dp.bin"));

        Assert.True(GenFlashVersionCatalog.TryReadCmiDpCode(
            "NT51950",
            image,
            out CmiDpCodeMetadata metadata));

        Assert.Equal(0x3B016, metadata.Register16Offset);
        Assert.Equal(0x3B018, metadata.Register18Offset);
        Assert.Equal(0x40, metadata.SystemCodeLowByte);
        Assert.Equal(0xCC, metadata.MajorVersionByte);
        Assert.Equal(0x0, metadata.MinorVersionNibble);
        Assert.Equal(576, metadata.JiraNumber);
        Assert.True(metadata.IsExpectedPayloadLength);
    }

    /// <summary>Reads the owner-confirmed NT51951 512 KB CMI registers independently from NT51950.</summary>
    [Fact]
    public void GoldenNt51951DpInputExposesCmiJiraAndDpVersion()
    {
        byte[] image = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "standard-merge-gen-flash",
            "inputs",
            "51951",
            "dp-512k",
            "dp.bin"));

        Assert.True(GenFlashVersionCatalog.TryReadCmiDpCode(
            "NT51951",
            image,
            out CmiDpCodeMetadata metadata));

        Assert.Equal(0x05016, metadata.Register16Offset);
        Assert.Equal(0x05018, metadata.Register18Offset);
        Assert.Equal(0xB7, metadata.SystemCodeLowByte);
        Assert.Equal(0x05, metadata.MajorVersionByte);
        Assert.Equal(0x0, metadata.MinorVersionNibble);
        Assert.Equal(695, metadata.JiraNumber);
        Assert.True(metadata.IsExpectedPayloadLength);
    }

    /// <summary>Cross-checks NT51926 2IC CMI registers against the owner-approved Jira/D-version filename.</summary>
    [Fact]
    public void GoldenNt51926TwoChipBaseMatchesFilenameJiraAndLegacyDpMajor()
    {
        byte[] image = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "ctrlram-replace",
            "fixtures",
            "20260705",
            "base",
            "nt51926-2ic-csot-toyota-d02t06-jira0597-20260622.bin"));

        AssertCmiMajorMatchesLegacyVersion("NT51926", image, 0x3E014, 597, 0x02, "AUTO_PRJ-597");
    }

    /// <summary>Cross-checks NT51927 2IC CMI registers against the owner-approved Jira/D-version filename.</summary>
    [Fact]
    public void GoldenNt51927TwoChipBaseMatchesFilenameJiraAndLegacyDpMajor()
    {
        byte[] image = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "ctrlram-replace",
            "fixtures",
            "20260705",
            "base",
            "nt51927-2ic-csot1560-d09t0d-jira0251-20260617.bin"));

        AssertCmiMajorMatchesLegacyVersion("NT51927", image, 0x3C01C, 251, 0x09, "AUTO_PRJ-251");
    }

    /// <summary>NT51927 3IC CMI major matches legacy DP version; its approved filename has no Jira token.</summary>
    [Fact]
    public void GoldenNt51927ThreeChipBaseMatchesLegacyDpMajor()
    {
        byte[] image = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "ctrlram-replace",
            "fixtures",
            "20260705",
            "base",
            "nt51927-3ic-tm-tl177xfks03-gm-d08t9b-20260703.bin"));

        AssertCmiMajorMatchesLegacyVersion("NT51927", image, 0x3C01C, 528, 0x08, "AUTO_PRJ-528");
    }

    /// <summary>NT51927's CMI location has evidence for both 256 KB Flash and 2 MiB DP payloads.</summary>
    [Fact]
    public void GoldenNt51927TwoMiBDpInputExposesCmiMajorMatchingLegacyVersion()
    {
        byte[] image = File.ReadAllBytes(RepositoryPaths.FromRepositoryRoot(
            "testdata",
            "golden",
            "standard-merge-gen-flash",
            "inputs",
            "51927",
            "dp.bin"));

        AssertCmiMajorMatchesLegacyVersion("NT51927", image, 0x3C01C, 313, 0x54, "AUTO_PRJ-313");
    }

    /// <summary>Guards the owner-handoff NT51929 CMI triple without committing its private firmware payload.</summary>
    [Fact]
    public void OwnerHandoffNt51929CmiMajorMatchesLegacyDpVersion()
    {
        byte[] image = new byte[0x40000];
        image[0x067] = 0x01;
        image[0x068] = 0x02;
        image[0x401A] = 0x52;
        image[0x401B] = 0x01;
        image[0x401C] = 0x02;

        AssertCmiMajorMatchesLegacyVersion("NT51929", image, 0x401A, 594, 0x01, "AUTO_PRJ-594");
        Assert.True(GenFlashVersionCatalog.TryReadCmiDpCode("NT51929", image, out CmiDpCodeMetadata cmi));
        Assert.Equal(0, cmi.MinorVersionNibble);
    }

    /// <summary>Unobserved DP sizes remain readable for metadata and are surfaced as warnings rather than build blockers.</summary>
    [Fact]
    public void CmiDpCodeAcceptsUnexpectedPayloadLengthWithWarning()
    {
        Assert.True(GenFlashVersionCatalog.TryReadCmiDpCode(
            "NT51950",
            new byte[0x80000],
            out CmiDpCodeMetadata nt51950));
        Assert.True(GenFlashVersionCatalog.TryReadCmiDpCode(
            "NT51951",
            new byte[0x40000],
            out CmiDpCodeMetadata nt51951));
        Assert.True(GenFlashVersionCatalog.TryReadCmiDpCode(
            "NT51927",
            new byte[0x80000],
            out CmiDpCodeMetadata nt51927));

        Assert.All([nt51950, nt51951, nt51927], metadata =>
        {
            Assert.False(metadata.IsExpectedPayloadLength);
            Assert.True(metadata.HasPayloadLengthWarning);
        });
    }

    /// <summary>Jira zero is valid CMI data but must not produce an AUTO_PRJ badge.</summary>
    [Fact]
    public void CmiDpCodeWithJiraZeroHasNoBadge()
    {
        Assert.True(GenFlashVersionCatalog.TryReadCmiDpCode(
            "NT51950",
            new byte[0x40000],
            out CmiDpCodeMetadata metadata));

        Assert.Equal(0, metadata.JiraNumber);
        Assert.False(metadata.HasJiraBadge);
        Assert.Null(metadata.JiraBadge);
        Assert.True(metadata.IsExpectedPayloadLength);
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

    private static void AssertCmiMajorMatchesLegacyVersion(
        string icId,
        byte[] image,
        long expectedRegister16Offset,
        ushort expectedJiraNumber,
        byte expectedMajorVersion,
        string expectedJiraBadge)
    {
        Assert.True(GenFlashVersionCatalog.TryReadCmiDpCode(icId, image, out CmiDpCodeMetadata cmi));
        Assert.True(GenFlashVersionCatalog.TryReadDpVersion(icId, image, out GenFlashDpVersionMetadata legacy));

        Assert.Equal(expectedRegister16Offset, cmi.Register16Offset);
        Assert.Equal(expectedJiraNumber, cmi.JiraNumber);
        Assert.Equal(expectedMajorVersion, cmi.MajorVersionByte);
        Assert.Equal(legacy.MainVersionByte, cmi.MajorVersionByte);
        Assert.True(cmi.HasJiraBadge);
        Assert.Equal(expectedJiraBadge, cmi.JiraBadge);
        Assert.True(cmi.IsExpectedPayloadLength);
    }
}
