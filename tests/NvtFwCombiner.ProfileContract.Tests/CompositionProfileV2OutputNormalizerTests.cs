using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Tests DTO normalization for v2 output naming policy.</summary>
public sealed class CompositionProfileV2OutputNormalizerTests
{
    /// <summary>Verifies both invalid-character policies preserve template and override intent.</summary>
    [Fact]
    public void OutputMapsEveryInvalidCharacterPolicy()
    {
        CompositionProfileOutput reject = CompositionProfileNormalizer.NormalizeOutput(
            Output("reject"),
            "2.0");
        CompositionProfileOutput replace = CompositionProfileNormalizer.NormalizeOutput(
            Output("replace-underscore", allowOverride: true),
            "2.0");

        Assert.Equal(CompiledOutputInvalidCharacterPolicy.Reject, reject.InvalidCharacterPolicy);
        Assert.Equal(CompiledOutputInvalidCharacterPolicy.ReplaceUnderscore, replace.InvalidCharacterPolicy);
        Assert.False(reject.AllowOverride);
        Assert.True(replace.AllowOverride);
        Assert.Equal("{original-name}_{version}.bin", replace.FileNameTemplate);
        Assert.Equal(["original-name", "version"], replace.RequiredTokenIds);
    }

    /// <summary>Verifies unknown policy tokens fail at their exact discriminator path.</summary>
    [Fact]
    public void OutputRejectsUnknownPolicyWithPath()
    {
        CompositionProfileNormalizationException exception = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeOutput(Output("future"), "2.0"));

        Assert.Equal("output.invalidCharacterPolicy", exception.Path);
    }

    /// <summary>Verifies missing token arrays retain their exact source path.</summary>
    [Fact]
    public void OutputRejectsMissingTokenArrayWithPath()
    {
        CompositionProfileNormalizationException exception = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeOutput(
                Output("reject") with { RequiredTokenIds = null! },
                "2.0"));

        Assert.Equal("output.requiredTokenIds", exception.Path);
    }

    /// <summary>Verifies constructor-owned template and token invariants stay at the output aggregate path.</summary>
    [Fact]
    public void OutputRejectsInvalidAggregateValuesAtOutputPath()
    {
        CompositionProfileNormalizationException template = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeOutput(
                Output("reject") with { FileNameTemplate = "" },
                "2.0"));
        CompositionProfileNormalizationException token = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeOutput(
                Output("reject") with { RequiredTokenIds = ["Version"] },
                "2.0"));

        Assert.Equal("output", template.Path);
        Assert.Equal("output", token.Path);
        _ = Assert.IsType<ArgumentException>(template.InnerException, exactMatch: false);
        _ = Assert.IsType<ArgumentException>(token.InnerException, exactMatch: false);
    }

    /// <summary>Schema 2.15 normalizes one typed output artifact and exact token-source policy.</summary>
    [Fact]
    public void OutputMapsV215CanonicalNamingRule()
    {
        CompositionProfileOutput output = CompositionProfileNormalizer.NormalizeOutput(
            new CompositionProfileOutputDocument(
                "{ic}_FlashCode_D{dp-version}T{tp-version}_{date}.bin",
                AllowOverride: true,
                InvalidCharacterPolicy: "reject",
                RequiredTokenIds: ["date", "dp-version", "ic", "tp-version"],
                RuleId: "normal-flashcode-v1",
                OutputArtifactType: "flash-code",
                TokenRequirements:
                [
                    Token("date", "run-date-utc", "block"),
                    Token(
                        "dp-version",
                        "dpcmi-version",
                        "use-placeholder",
                        "dp-inspection",
                        "xxxx"),
                    Token("ic", "compiled-ic", "block"),
                    Token(
                        "tp-version",
                        "firmware-config-tp-version",
                        "use-placeholder",
                        "tp-inspection",
                        "xxxx"),
                ]),
            "2.15");

        Assert.Equal("normal-flashcode-v1", output.RuleId);
        Assert.Equal(
            CompiledOutputArtifactType.FlashCode,
            output.OutputArtifactType);
        Assert.Collection(
            output.TokenRequirements,
            token => Assert.Equal(
                ("date", CompiledOutputTokenSourceKind.RunDateUtc, null,
                    CompiledOutputTokenMissingPolicy.Block, null),
                Values(token)),
            token => Assert.Equal(
                ("dp-version", CompiledOutputTokenSourceKind.DpcmiVersion,
                    "dp-inspection", CompiledOutputTokenMissingPolicy.UsePlaceholder,
                    "xxxx"),
                Values(token)),
            token => Assert.Equal(
                ("ic", CompiledOutputTokenSourceKind.CompiledIc, null,
                    CompiledOutputTokenMissingPolicy.Block, null),
                Values(token)),
            token => Assert.Equal(
                ("tp-version",
                    CompiledOutputTokenSourceKind.FirmwareConfigTpVersion,
                    "tp-inspection", CompiledOutputTokenMissingPolicy.UsePlaceholder,
                    "xxxx"),
                Values(token)));

        static (string TokenId, CompiledOutputTokenSourceKind SourceKind,
            string? MetadataBindingId, CompiledOutputTokenMissingPolicy MissingPolicy,
            string? Placeholder) Values(CompositionProfileOutputTokenRequirement token)
        {
            return (
                token.TokenId,
                token.SourceKind,
                token.MetadataBindingId,
                token.MissingPolicy,
                token.Placeholder);
        }
    }

    /// <summary>Typed naming authority is versioned and cannot leak into an older profile schema.</summary>
    [Fact]
    public void OutputRejectsV215RuleUnderLegacySchema()
    {
        CompositionProfileOutputDocument document = Output("reject") with
        {
            RuleId = "normal-flashcode-v1",
            OutputArtifactType = "flash-code",
            TokenRequirements = [Token("ic", "compiled-ic", "block")],
        };

        CompositionProfileNormalizationException exception =
            Assert.Throws<CompositionProfileNormalizationException>(() =>
                CompositionProfileNormalizer.NormalizeOutput(document, "2.14"));

        Assert.Equal("output", exception.Path);
    }

    /// <summary>Schema 2.15 cannot normalize to a deferred typed renderer.</summary>
    [Fact]
    public void OutputRejectsReplacementPolicyForV215()
    {
        CompositionProfileOutputDocument document = new(
            "{ic}_TPFW_T{tp-version}_{date}.bin",
            AllowOverride: true,
            InvalidCharacterPolicy: "replace-underscore",
            RequiredTokenIds: ["date", "ic", "tp-version"],
            RuleId: "tp-firmware-v1",
            OutputArtifactType: "tp-firmware",
            TokenRequirements:
            [
                Token("date", "run-date-utc", "block"),
                Token("ic", "compiled-ic", "block"),
                Token(
                    "tp-version",
                    "firmware-config-tp-version",
                    "use-placeholder",
                    "tp-inspection",
                    "xxxx"),
            ]);

        CompositionProfileNormalizationException exception =
            Assert.Throws<CompositionProfileNormalizationException>(() =>
                CompositionProfileNormalizer.NormalizeOutput(document, "2.15"));

        Assert.Equal("output.invalidCharacterPolicy", exception.Path);
    }

    private static CompositionProfileOutputDocument Output(string policy, bool allowOverride = false)
    {
        return new CompositionProfileOutputDocument(
            "{original-name}_{version}.bin",
            allowOverride,
            policy,
            ["version", "original-name"]);
    }

    private static CompositionProfileOutputTokenRequirementDocument Token(
        string tokenId,
        string sourceKind,
        string missingPolicy,
        string? metadataBindingId = null,
        string? placeholder = null)
    {
        return new CompositionProfileOutputTokenRequirementDocument(
            tokenId,
            new CompositionProfileOutputTokenSourceDocument(
                sourceKind,
                metadataBindingId),
            missingPolicy,
            placeholder);
    }
}
