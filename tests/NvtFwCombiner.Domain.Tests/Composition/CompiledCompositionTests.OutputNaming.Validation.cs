using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompiledCompositionTests
{
    /// <summary>Token authority is closed over source, binding, missing policy, and placeholder.</summary>
    [Fact]
    public void TypedOutputTokenRejectsInvalidAuthority()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CompiledOutputTokenRequirement(
                "date",
                (CompiledOutputTokenSourceKind)999,
                metadataBindingId: null,
                CompiledOutputTokenMissingPolicy.Block,
                placeholder: null));
        _ = Assert.Throws<ArgumentException>(() =>
            new CompiledOutputTokenRequirement(
                "date",
                CompiledOutputTokenSourceKind.RunDateUtc,
                "unexpected-metadata-binding",
                CompiledOutputTokenMissingPolicy.Block,
                placeholder: null));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CompiledOutputTokenRequirement(
                "date",
                CompiledOutputTokenSourceKind.RunDateUtc,
                metadataBindingId: null,
                (CompiledOutputTokenMissingPolicy)999,
                placeholder: null));
        _ = Assert.Throws<ArgumentException>(() =>
            new CompiledOutputTokenRequirement(
                "date",
                CompiledOutputTokenSourceKind.RunDateUtc,
                metadataBindingId: null,
                CompiledOutputTokenMissingPolicy.Block,
                "forbidden-placeholder"));
    }

    /// <summary>Typed rule authority must be complete and match one canonical renderer contract.</summary>
    [Fact]
    public void TypedOutputRuleRejectsIncompleteOrNonCanonicalAuthority()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CompiledOutputNamingRequirement(
                "output.bin",
                allowOverride: true,
                CompiledOutputInvalidCharacterPolicy.Reject,
                requiredTokenIds: [],
                ruleId: null,
                (CompiledOutputArtifactType)999,
                tokenRequirements: null));
        _ = Assert.Throws<ArgumentException>(() =>
            new CompiledOutputNamingRequirement(
                CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
                allowOverride: true,
                CompiledOutputInvalidCharacterPolicy.Reject,
                ["date", "dp-version", "ic", "tp-version"],
                CompiledOutputNamingRequirement.NormalFlashCodeV1RuleId,
                CompiledOutputArtifactType.Unspecified,
                tokenRequirements: null));
        _ = Assert.Throws<ArgumentException>(() =>
            new CompiledOutputNamingRequirement(
                "output.bin",
                allowOverride: true,
                CompiledOutputInvalidCharacterPolicy.Reject,
                requiredTokenIds: [],
                ruleId: null,
                CompiledOutputArtifactType.FlashCode,
                tokenRequirements: []));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateTypedNormalOutput(
                CompiledOutputArtifactType.TpFirmware,
                NormalTokenRequirements()));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateTypedNormalOutput(
                CompiledOutputArtifactType.FlashCode,
                [.. NormalTokenRequirements(), null!]));
        _ = Assert.Throws<ArgumentException>(() =>
            CreateTypedNormalOutput(
                CompiledOutputArtifactType.FlashCode,
                NormalTokenRequirements().Where(static requirement =>
                    requirement.TokenId != "dp-version")));

        CompiledOutputTokenRequirement[] wrongSource = NormalTokenRequirements();
        wrongSource[0] = new CompiledOutputTokenRequirement(
            "date",
            CompiledOutputTokenSourceKind.CompiledIc,
            metadataBindingId: null,
            CompiledOutputTokenMissingPolicy.Block,
            placeholder: null);
        _ = Assert.Throws<ArgumentException>(() =>
            CreateTypedNormalOutput(
                CompiledOutputArtifactType.FlashCode,
                wrongSource));
    }

    private static CompiledOutputNamingRequirement CreateTypedNormalOutput(
        CompiledOutputArtifactType artifactType,
        IEnumerable<CompiledOutputTokenRequirement> requirements)
    {
        return new CompiledOutputNamingRequirement(
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            allowOverride: true,
            CompiledOutputInvalidCharacterPolicy.Reject,
            ["date", "dp-version", "ic", "tp-version"],
            CompiledOutputNamingRequirement.NormalFlashCodeV1RuleId,
            artifactType,
            requirements);
    }

    private static CompiledOutputTokenRequirement[] NormalTokenRequirements()
    {
        return
        [
            new CompiledOutputTokenRequirement(
                "date",
                CompiledOutputTokenSourceKind.RunDateUtc,
                metadataBindingId: null,
                CompiledOutputTokenMissingPolicy.Block,
                placeholder: null),
            new CompiledOutputTokenRequirement(
                "dp-version",
                CompiledOutputTokenSourceKind.DpcmiVersion,
                "dp-inspection",
                CompiledOutputTokenMissingPolicy.UsePlaceholder,
                "xxxx",
                "source"),
            new CompiledOutputTokenRequirement(
                "ic",
                CompiledOutputTokenSourceKind.CompiledIc,
                metadataBindingId: null,
                CompiledOutputTokenMissingPolicy.Block,
                placeholder: null),
            new CompiledOutputTokenRequirement(
                "tp-version",
                CompiledOutputTokenSourceKind.FirmwareConfigTpVersion,
                "tp-inspection",
                CompiledOutputTokenMissingPolicy.UsePlaceholder,
                "xxxx",
                "source"),
        ];
    }
}
