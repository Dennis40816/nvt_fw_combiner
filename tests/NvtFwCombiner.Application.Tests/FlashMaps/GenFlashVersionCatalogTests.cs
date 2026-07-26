using System.Text.Json;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.FlashMaps;

/// <summary>Golden-backed checks for gen_flash contiguous DP main/sub version-byte rules.</summary>
public sealed class GenFlashVersionCatalogTests
{
    /// <summary>Guards DP version rules as contiguous main/sub byte pairs inside the declared DP range.</summary>
    [Fact]
    public void DpVersionRulesAreContiguousAndInsideDeclaredDpRange()
    {
        string[] ruleIds = ["51917", "51919", "51920", "51923", "51926", "51927", "51928", "51931", "51932"];
        foreach (string ruleId in ruleIds)
        {
            Assert.True(GenFlashVersionCatalog.TryGetDpVersionRule(ruleId, out GenFlashDpVersionRule rule));
            Assert.Equal(rule.OutputDpStart + rule.InputRelativeOffset, rule.OutputMainAbsoluteAddress);
            Assert.Equal(rule.OutputMainAbsoluteAddress + 1, rule.OutputSubAbsoluteAddress);
            Assert.True(rule.OutputMainAbsoluteAddress >= rule.OutputDpStart);
            Assert.True(rule.OutputSubAbsoluteAddress < rule.OutputDpEndExclusive);
            Assert.True(GenFlashVersionCatalog.TryGetDpVersionRule($"NT{rule.IcId}", out GenFlashDpVersionRule resolved));
            Assert.Same(rule, resolved);
        }
    }

    /// <summary>Reads contiguous DP main/sub version bytes from owner-approved gen_flash standard-merge DP inputs.</summary>
    [Theory]
    [InlineData("51920", "0101")]
    [InlineData("51923", "8100")]
    [InlineData("51926", "0102")]
    [InlineData("51927", "5401")]
    [InlineData("51928", "8211")]
    [InlineData("51931", "8D60")]
    [InlineData("51932", "8201")]
    public void GoldenDpInputsExposeExpectedGenFlashVersion(string ic, string expectedToken)
    {
        byte[] image = File.ReadAllBytes(
            CanonicalGoldenTestData.ArtifactPath("standard-merge", ic, "dp-input"));

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
            "NT51919",
            image,
            out _));
    }

    /// <summary>Guards CMI DP register triples against offset or expected-payload-length drift.</summary>
    [Fact]
    public void CmiDpCodeRulesFitInsideExpectedPayloadLengths()
    {
        string[] ruleIds = ["51919", "51923", "51926", "51927", "51930", "51932", "51950", "51951"];
        foreach (string ruleId in ruleIds)
        {
            Assert.True(GenFlashVersionCatalog.TryGetCmiDpCodeRule(ruleId, out CmiDpCodeRule rule));
            Assert.True(rule.Register16Offset >= 0);
            Assert.NotEmpty(rule.ExpectedPayloadLengths);
            Assert.Equal(
                rule.ExpectedPayloadLengths.Count,
                rule.ExpectedPayloadLengths.Distinct().Count());
            Assert.All(rule.ExpectedPayloadLengths, length =>
            {
                Assert.True(length > 0);
                Assert.True(rule.Register16Offset + 2 < length);
                if (rule.CascadeRegister16Offset is { } cascadeRegister16Offset)
                {
                    Assert.True(cascadeRegister16Offset + 2 < length);
                }
            });
            Assert.True(GenFlashVersionCatalog.TryGetCmiDpCodeRule($"NT{rule.IcId}", out CmiDpCodeRule resolved));
            Assert.Same(rule, resolved);
        }
    }

    /// <summary>Reads the owner-confirmed NT51950 CMI registers without conflating Jira and DP version.</summary>
    [Fact]
    public void GoldenNt51950DpInputExposesCmiJiraAndDpVersion()
    {
        byte[] image = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(
            "standard-merge",
            "51950",
            "dp-input",
            "dp-256k"));

        Assert.True(GenFlashVersionCatalog.TryReadCmiDpCode(
            "NT51950",
            image,
            firmwareConfigChipNumber: 1,
            out CmiDpCodeMetadata metadata));

        Assert.Equal(0x3B016, metadata.Register16Offset);
        Assert.Equal(0x3B018, metadata.Register18Offset);
        Assert.Equal(0x40, metadata.SystemCodeLowByte);
        Assert.Equal(0xCC, metadata.MajorVersionByte);
        Assert.Equal(0x0, metadata.MinorVersionNibble);
        Assert.Equal(576, metadata.JiraNumber);
        Assert.True(metadata.IsExpectedPayloadLength);
    }

    /// <summary>Cross-checks NT51923 CMI bytes retained in its standard-merge golden output.</summary>
    [Fact]
    public void GoldenNt51923OutputMatchesLegacyDpMajor()
    {
        byte[] image = File.ReadAllBytes(
            CanonicalGoldenTestData.ArtifactPath("standard-merge", "51923", "expected-output"));

        AssertCmiMajorMatchesLegacyVersion("NT51923", image, 0x3E014, 216, 0x81, "AUTO_PRJ-216");
    }

    /// <summary>Cross-checks NT51932 CMI bytes retained in its standard-merge golden output.</summary>
    [Fact]
    public void GoldenNt51932OutputMatchesLegacyDpMajor()
    {
        byte[] image = File.ReadAllBytes(
            CanonicalGoldenTestData.ArtifactPath("standard-merge", "51932", "expected-output"));

        AssertCmiMajorMatchesLegacyVersion("NT51932", image, 0x401A, 495, 0x82, "AUTO_PRJ-495");
    }

    /// <summary>Requires TP FWConfig ChipNumber to select NT51950's single or cascade CMI location.</summary>
    [Fact]
    public void Nt51950CmiLocationUsesFirmwareConfigChipNumber()
    {
        byte[] image = new byte[0x80000];
        image[0x3B016] = 0x40;
        image[0x3B017] = 0xCC;
        image[0x3B018] = 0x02;
        image[0x05016] = 0x56;
        image[0x05017] = 0xA5;
        image[0x05018] = 0x03;

        Assert.False(GenFlashVersionCatalog.TryReadCmiDpCode("NT51950", image, out _));
        Assert.True(GenFlashVersionCatalog.TryReadCmiDpCode(
            "NT51950",
            image,
            firmwareConfigChipNumber: 1,
            out CmiDpCodeMetadata single));
        Assert.True(GenFlashVersionCatalog.TryReadCmiDpCode(
            "NT51950",
            image,
            firmwareConfigChipNumber: 2,
            out CmiDpCodeMetadata cascade));

        Assert.Equal(0x3B016, single.Register16Offset);
        Assert.Equal(576, single.JiraNumber);
        Assert.Equal(0x05016, cascade.Register16Offset);
        Assert.Equal(854, cascade.JiraNumber);
        Assert.Equal(0xA5, cascade.MajorVersionByte);
    }

    /// <summary>Reads the owner-confirmed NT51951 512 KB CMI registers independently from NT51950.</summary>
    [Fact]
    public void GoldenNt51951DpInputExposesCmiJiraAndDpVersion()
    {
        byte[] image = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(
            "standard-merge",
            "51951",
            "dp-input",
            "dp-512k"));

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

    /// <summary>Cross-checks NT51926 2IC CMI registers against the owner-approved Jira/D-version FlashCode.</summary>
    [Fact]
    public void GoldenNt51926TwoChipFlashCodeMatchesFilenameJiraAndLegacyDpMajor()
    {
        byte[] image = ReadCanonicalGolden(
            "nt51926-fw141-cascade2-auto-prj-597-20260717",
            "expected-output");

        AssertCmiMajorMatchesLegacyVersion("NT51926", image, 0x3E014, 597, 0x02, "AUTO_PRJ-597");
    }

    /// <summary>Cross-checks NT51927 2IC CMI registers against the owner-approved Jira/D-version filename.</summary>
    [Fact]
    public void GoldenNt51927TwoChipBaseMatchesFilenameJiraAndLegacyDpMajor()
    {
        byte[] image = ReadCanonicalInputEvidence(
            "nt51927-2chip-self-20260705",
            "reference-base");

        AssertCmiMajorMatchesLegacyVersion("NT51927", image, 0x3C01C, 251, 0x09, "AUTO_PRJ-251");
    }

    /// <summary>NT51927 3IC CMI major matches legacy DP version; its approved filename has no Jira token.</summary>
    [Fact]
    public void GoldenNt51927ThreeChipBaseMatchesLegacyDpMajor()
    {
        byte[] image = ReadCanonicalInputEvidence(
            "nt51927-3chip-self-20260705",
            "reference-base");

        AssertCmiMajorMatchesLegacyVersion("NT51927", image, 0x3C01C, 528, 0x08, "AUTO_PRJ-528");
    }

    /// <summary>NT51927's CMI location has evidence for both 256 KB Flash and 2 MiB DP payloads.</summary>
    [Fact]
    public void GoldenNt51927TwoMiBDpInputExposesCmiMajorMatchingLegacyVersion()
    {
        byte[] image = File.ReadAllBytes(
            CanonicalGoldenTestData.ArtifactPath("standard-merge", "51927", "dp-input"));

        AssertCmiMajorMatchesLegacyVersion("NT51927", image, 0x3C01C, 313, 0x54, "AUTO_PRJ-313");
    }

    /// <summary>NT51929 has no legacy offset rule after its DPCMI authority migrates to the canonical profile.</summary>
    [Fact]
    public void Nt51929HasNoLegacyDpVersionOrCmiRule()
    {
        byte[] image = new byte[0x40000];
        image[0x401A] = 0x52;
        image[0x401B] = 0x01;
        image[0x401C] = 0x02;

        Assert.False(GenFlashVersionCatalog.TryGetDpVersionRule("NT51929", out _));
        Assert.False(GenFlashVersionCatalog.TryGetCmiDpCodeRule("NT51929", out _));
        Assert.False(GenFlashVersionCatalog.TryReadDpVersion("NT51929", image, out _));
        Assert.False(GenFlashVersionCatalog.TryReadCmiDpCode("NT51929", image, out _));
    }

    /// <summary>Reads the owner-confirmed NT51930 FlashCode CMI register triple without guessing a legacy DP offset.</summary>
    [Fact]
    public void GoldenNt51930OutputExposesOwnerConfirmedCmiJiraAndDpVersion()
    {
        byte[] image = ReadCanonicalGolden(
            "nt51930-fw130-cascade3-auto-prj-302-inx-20260718",
            "expected-output");

        Assert.True(GenFlashVersionCatalog.TryReadCmiDpCode(
            "NT51930",
            image,
            out CmiDpCodeMetadata metadata));

        Assert.Equal(0x18, metadata.Register16Offset);
        Assert.Equal(0x1A, metadata.Register18Offset);
        Assert.Equal(0x2E, metadata.SystemCodeLowByte);
        Assert.Equal(0x05, metadata.MajorVersionByte);
        Assert.Equal(0x0, metadata.MinorVersionNibble);
        Assert.Equal(302, metadata.JiraNumber);
        Assert.True(metadata.IsExpectedPayloadLength);
        Assert.Contains("[0x18, 0x1B), 2026-07-22", metadata.EvidenceSource, StringComparison.Ordinal);
    }

    /// <summary>Unobserved DP sizes remain readable for metadata and are surfaced as warnings rather than build blockers.</summary>
    [Fact]
    public void CmiDpCodeAcceptsUnexpectedPayloadLengthWithWarning()
    {
        Assert.True(GenFlashVersionCatalog.TryReadCmiDpCode(
            "NT51950",
            new byte[0x80000],
            firmwareConfigChipNumber: 1,
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
            firmwareConfigChipNumber: 1,
            out CmiDpCodeMetadata metadata));

        Assert.Equal(0, metadata.JiraNumber);
        Assert.False(metadata.HasJiraBadge);
        Assert.Null(metadata.JiraBadge);
        Assert.True(metadata.IsExpectedPayloadLength);
    }

    /// <summary>ICs without a legacy contiguous DP rule retain only their separately declared CMI evidence.</summary>
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

    private static byte[] ReadCanonicalGolden(string caseId, string artifactId)
    {
        return ReadCanonicalArtifact(
            CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", caseId),
            artifactId);
    }

    private static byte[] ReadCanonicalInputEvidence(string caseId, string artifactId)
    {
        return ReadCanonicalArtifact(
            CanonicalGoldenTestData.LoadDirectEvidenceCase("ctrlram-replace", caseId),
            artifactId);
    }

    private static byte[] ReadCanonicalArtifact(JsonElement goldenCase, string artifactId)
    {
        JsonElement artifact = goldenCase.GetProperty("artifacts")
            .EnumerateArray()
            .Single(item => StringComparer.Ordinal.Equals(
                item.GetProperty("artifactId").GetString(),
                artifactId));
        return File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(artifact));
    }
}
