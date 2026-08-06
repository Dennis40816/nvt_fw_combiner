using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal sealed partial class CompositionProfileDefinition
{
    private void ValidateOutputNaming(
        IReadOnlyDictionary<string, CompositionProfileMetadataBinding> metadataBindings)
    {
        foreach (string tokenId in Output.RequiredTokenIds)
        {
            if (!Output.FileNameTemplate.Contains($"{{{tokenId}}}", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Output template does not contain required token '{tokenId}'.");
            }
        }

        if (Output.RuleId is null)
        {
            return;
        }

        if (!Output.TokenRequirements.Select(static requirement => requirement.TokenId)
                .SequenceEqual(Output.RequiredTokenIds, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Typed output token requirements must exactly match the required token ids.");
        }

        bool isNormalFlashCode =
            StringComparer.Ordinal.Equals(
                Output.RuleId,
                CompiledOutputNamingRequirement.NormalFlashCodeV1RuleId) &&
            Output.OutputArtifactType == CompiledOutputArtifactType.FlashCode &&
            StringComparer.Ordinal.Equals(
                Output.FileNameTemplate,
                CompiledOutputNamingRequirement.NormalFlashCodeV1Template);
        bool isTpFirmware =
            StringComparer.Ordinal.Equals(
                Output.RuleId,
                CompiledOutputNamingRequirement.TpFirmwareV1RuleId) &&
            Output.OutputArtifactType == CompiledOutputArtifactType.TpFirmware &&
            StringComparer.Ordinal.Equals(
                Output.FileNameTemplate,
                CompiledOutputNamingRequirement.TpFirmwareV1Template);
        if (!isNormalFlashCode && !isTpFirmware)
        {
            throw new ArgumentException(
                "Typed output naming must select one canonical rule, artifact type, and template.");
        }

        string[] expectedTokenIds = isNormalFlashCode
            ? ["date", "dp-version", "ic", "tp-version"]
            : ["date", "ic", "tp-version"];
        if (!Output.RequiredTokenIds.SequenceEqual(
                expectedTokenIds,
                StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Typed output naming must declare the canonical token set for its selected rule.");
        }

        foreach (CompositionProfileOutputTokenRequirement requirement in Output.TokenRequirements)
        {
            ValidateCanonicalOutputTokenRequirement(
                requirement,
                isNormalFlashCode);
            ValidateOutputTokenSource(requirement, metadataBindings);
        }
    }

    private static void ValidateCanonicalOutputTokenRequirement(
        CompositionProfileOutputTokenRequirement requirement,
        bool isNormalFlashCode)
    {
        (CompiledOutputTokenSourceKind expectedSource,
            CompiledOutputTokenMissingPolicy expectedMissing,
            string? expectedPlaceholder) = requirement.TokenId switch
            {
                "date" => (
                    CompiledOutputTokenSourceKind.RunDateUtc,
                    CompiledOutputTokenMissingPolicy.Block,
                    null),
                "ic" => (
                    CompiledOutputTokenSourceKind.CompiledIc,
                    CompiledOutputTokenMissingPolicy.Block,
                    null),
                "dp-version" when isNormalFlashCode => (
                    CompiledOutputTokenSourceKind.DpcmiVersion,
                    CompiledOutputTokenMissingPolicy.UsePlaceholder,
                    "xxxx"),
                "tp-version" => (
                    CompiledOutputTokenSourceKind.FirmwareConfigTpVersion,
                    CompiledOutputTokenMissingPolicy.UsePlaceholder,
                    "xxxx"),
                _ => throw new ArgumentException(
                    $"Output token '{requirement.TokenId}' is not valid for the selected naming rule."),
            };
        if (requirement.SourceKind != expectedSource ||
            requirement.MissingPolicy != expectedMissing ||
            !StringComparer.Ordinal.Equals(
                requirement.Placeholder,
                expectedPlaceholder))
        {
            throw new ArgumentException(
                $"Output token '{requirement.TokenId}' does not match the canonical source and missing-value policy.");
        }
    }

    private static void ValidateOutputTokenSource(
        CompositionProfileOutputTokenRequirement requirement,
        IReadOnlyDictionary<string, CompositionProfileMetadataBinding> metadataBindings)
    {
        string? expectedStructureId = requirement.SourceKind switch
        {
            CompiledOutputTokenSourceKind.DpcmiVersion => "dpcmi",
            CompiledOutputTokenSourceKind.FirmwareConfigTpVersion =>
                "firmware-config-general-parameters",
            CompiledOutputTokenSourceKind.CompiledIc or
                CompiledOutputTokenSourceKind.RunDateUtc => null,
            CompiledOutputTokenSourceKind.Unspecified =>
                throw new ArgumentOutOfRangeException(
                    nameof(requirement),
                    requirement.SourceKind,
                    "Typed output tokens cannot have an unspecified source kind."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(requirement),
                requirement.SourceKind,
                "Unknown output token source kind."),
        };
        if (expectedStructureId is null)
        {
            if (requirement.MetadataBindingId is not null)
            {
                throw new ArgumentException(
                    "Non-metadata output token sources cannot declare a metadata binding.");
            }

            return;
        }

        if (requirement.MetadataBindingId is null ||
            !metadataBindings.TryGetValue(
                requirement.MetadataBindingId,
                out CompositionProfileMetadataBinding? binding) ||
            !binding.Purposes.Contains(CompositionProfileMetadataPurpose.OutputNaming) ||
            !StringComparer.Ordinal.Equals(binding.StructureId, expectedStructureId))
        {
            throw new ArgumentException(
                $"Output token '{requirement.TokenId}' must reference one output-naming metadata binding for canonical structure '{expectedStructureId}'.");
        }
    }
}
