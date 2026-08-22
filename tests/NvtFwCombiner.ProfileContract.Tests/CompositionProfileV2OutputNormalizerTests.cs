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
        CompiledOutputNamingRequirement reject = CompositionProfileNormalizer.NormalizeOutput(
            Output("reject"));
        CompiledOutputNamingRequirement replace = CompositionProfileNormalizer.NormalizeOutput(
            Output("replace-underscore", allowOverride: true));

        Assert.Equal(CompiledOutputInvalidCharacterPolicy.Reject, reject.InvalidCharacterPolicy);
        Assert.Equal(CompiledOutputInvalidCharacterPolicy.ReplaceUnderscore, replace.InvalidCharacterPolicy);
        Assert.False(reject.AllowOverride);
        Assert.True(replace.AllowOverride);
        Assert.Equal("{original-name}_{version}.bin", replace.FileNameTemplate);
        Assert.Equal(
            ["original-name", "version"],
            replace.TokenRequirements.Select(static requirement => requirement.TokenId));
    }

    /// <summary>Verifies unknown policy tokens fail at their exact discriminator path.</summary>
    [Fact]
    public void OutputRejectsUnknownPolicyWithPath()
    {
        CompositionProfileNormalizationException exception = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeOutput(Output("future")));

        Assert.Equal("output.invalidCharacterPolicy", exception.Path);
    }

    /// <summary>Verifies constructor-owned template and token invariants stay at the output aggregate path.</summary>
    [Fact]
    public void OutputRejectsInvalidAggregateValuesAtOutputPath()
    {
        CompositionProfileNormalizationException template = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeOutput(
                Output("reject") with { FileNameTemplate = "" }));
        CompositionProfileNormalizationException token = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.NormalizeOutput(
                Output("reject") with { RequiredTokenIds = ["Version"] }));

        Assert.Equal("output", template.Path);
        Assert.Equal("output", token.Path);
        _ = Assert.IsType<ArgumentException>(template.InnerException, exactMatch: false);
        _ = Assert.IsType<ArgumentException>(token.InnerException, exactMatch: false);
    }

    /// <summary>Schema 2.15 normalizes one typed output artifact and exact token-source policy.</summary>
    [Fact]
    public void OutputMapsV215CanonicalNamingRule()
    {
        CompiledOutputNamingRequirement output = CompositionProfileNormalizer.NormalizeOutput(
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
            "output",
            OutputBindings());

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
            string? Placeholder) Values(CompiledOutputTokenRequirement token)
        {
            return (
                token.TokenId,
                token.SourceKind,
                token.MetadataBindingId,
                token.MissingPolicy,
                token.Placeholder);
        }
    }

    /// <summary>Typed metadata tokens require an explicit output-naming binding purpose.</summary>
    [Fact]
    public void OutputRejectsMetadataBindingWithoutOutputNamingPurpose()
    {
        CompositionProfileOutputDocument document = new(
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            AllowOverride: false,
            InvalidCharacterPolicy: "reject",
            RequiredTokenIds: ["date", "dp-version", "ic", "tp-version"],
            RuleId: CompiledOutputNamingRequirement.NormalFlashCodeV1RuleId,
            OutputArtifactType: "flash-code",
            TokenRequirements:
            [
                Token("date", "run-date-utc", "block"),
                Token("dp-version", "dpcmi-version", "use-placeholder", "dp-inspection", "xxxx"),
                Token("ic", "compiled-ic", "block"),
                Token("tp-version", "firmware-config-tp-version", "use-placeholder", "tp-inspection", "xxxx"),
            ]);
        CompositionProfileMetadataBinding[] bindings = OutputBindings();
        bindings[0] = CompositionProfileV2DefinitionTestData.CreateMetadataBinding(
            "dp-inspection",
            "tp-source",
            "dpcmi",
            ["version"],
            [CompositionProfileMetadataPurpose.Validation]);

        CompositionProfileNormalizationException exception =
            Assert.Throws<CompositionProfileNormalizationException>(() =>
                CompositionProfileNormalizer.NormalizeOutput(
                    document,
                    "output",
                    bindings));

        Assert.Equal(
            "output.tokenRequirements[1].source.metadataBindingId",
            exception.Path);
    }

    /// <summary>
    /// A family-local structure id remains valid until resolved-map coherence
    /// proves that its immutable definition is the canonical DPCMI contract.
    /// </summary>
    [Fact]
    public void OutputRetainsFamilyLocalMetadataBindingAlias()
    {
        CompositionProfileMetadataBinding[] bindings = OutputBindings();
        bindings[0] = CompositionProfileV2DefinitionTestData.CreateMetadataBinding(
            "dp-inspection",
            "dp-source",
            "nt51950-dpcmi-standard-merge",
            ["version"],
            [CompositionProfileMetadataPurpose.OutputNaming]);

        CompiledOutputNamingRequirement output = CompositionProfileNormalizer.NormalizeOutput(
            new CompositionProfileOutputDocument(
                CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
                AllowOverride: true,
                InvalidCharacterPolicy: "reject",
                RequiredTokenIds: ["date", "dp-version", "ic", "tp-version"],
                RuleId: CompiledOutputNamingRequirement.NormalFlashCodeV1RuleId,
                OutputArtifactType: "flash-code",
                TokenRequirements:
                [
                    Token("date", "run-date-utc", "block"),
                    Token("dp-version", "dpcmi-version", "use-placeholder", "dp-inspection", "xxxx"),
                    Token("ic", "compiled-ic", "block"),
                    Token("tp-version", "firmware-config-tp-version", "use-placeholder", "tp-inspection", "xxxx"),
                ]),
            "output",
            bindings);

        Assert.Equal(
            "dp-source",
            output.TokenRequirements.Single(static token => token.TokenId == "dp-version").MetadataSpaceId);
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

    private static CompositionProfileMetadataBinding[] OutputBindings()
    {
        return
        [
            CompositionProfileV2DefinitionTestData.CreateMetadataBinding(
                "dp-inspection",
                "tp-source",
                "dpcmi",
                ["version"],
                [CompositionProfileMetadataPurpose.OutputNaming]),
            CompositionProfileV2DefinitionTestData.CreateMetadataBinding(
                "tp-inspection",
                "tp-source",
                "firmware-config-general-parameters",
                ["tp-version"],
                [CompositionProfileMetadataPurpose.OutputNaming]),
        ];
    }
}
