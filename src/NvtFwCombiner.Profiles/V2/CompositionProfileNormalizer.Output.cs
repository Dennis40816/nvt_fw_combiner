using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    internal static CompiledOutputNamingRequirement NormalizeOutput(
        CompositionProfileOutputDocument document,
        string path = "output",
        IReadOnlyList<CompositionProfileMetadataBinding>? metadataBindings = null)
    {
        string? ruleId = null;
        CompiledOutputArtifactType outputArtifactType =
            CompiledOutputArtifactType.Unspecified;
        CompiledOutputTokenRequirement[]? tokenRequirements = null;
        if (document.RuleId is not null)
        {
            ruleId = document.RuleId;
            outputArtifactType = NormalizeOutputArtifactType(
                document.OutputArtifactType!,
                $"{path}.outputArtifactType");
            tokenRequirements = NormalizeOutputTokenRequirements(
                document.TokenRequirements!,
                $"{path}.tokenRequirements",
                metadataBindings ?? []);
        }

        CompiledOutputInvalidCharacterPolicy invalidCharacterPolicy =
            NormalizeInvalidCharacterPolicy(
                document.InvalidCharacterPolicy,
                $"{path}.invalidCharacterPolicy");

        return Wrap(path, () => ruleId is null
            ? new CompiledOutputNamingRequirement(
                document.FileNameTemplate,
                document.AllowOverride,
                invalidCharacterPolicy,
                document.RequiredTokenIds)
            : new CompiledOutputNamingRequirement(
                document.FileNameTemplate,
                document.AllowOverride,
                invalidCharacterPolicy,
                document.RequiredTokenIds,
                ruleId,
                outputArtifactType,
                tokenRequirements));
    }

    private static CompiledOutputInvalidCharacterPolicy NormalizeInvalidCharacterPolicy(
        string value,
        string path)
    {
        return value switch
        {
            "reject" => CompiledOutputInvalidCharacterPolicy.Reject,
            "replace-underscore" => CompiledOutputInvalidCharacterPolicy.ReplaceUnderscore,
            _ => throw Error(path, "Unknown output invalid-character policy."),
        };
    }

    private static CompiledOutputArtifactType NormalizeOutputArtifactType(
        string value,
        string path)
    {
        return value switch
        {
            "flash-code" => CompiledOutputArtifactType.FlashCode,
            "tp-firmware" => CompiledOutputArtifactType.TpFirmware,
            _ => throw Error(path, "Unknown output artifact type."),
        };
    }

    private static CompiledOutputTokenRequirement[] NormalizeOutputTokenRequirements(
        IReadOnlyList<CompositionProfileOutputTokenRequirementDocument> documents,
        string path,
        IReadOnlyList<CompositionProfileMetadataBinding> metadataBindings)
    {
        return [.. documents.Select((document, index) => NormalizeOutputTokenRequirement(
            document,
            $"{path}[{index}]",
            metadataBindings))];
    }

    private static CompiledOutputTokenRequirement NormalizeOutputTokenRequirement(
        CompositionProfileOutputTokenRequirementDocument document,
        string path,
        IReadOnlyList<CompositionProfileMetadataBinding> metadataBindings)
    {
        CompositionProfileOutputTokenSourceDocument source = document.Source;
        CompiledOutputTokenSourceKind sourceKind = source.Kind switch
        {
            "compiled-ic" => CompiledOutputTokenSourceKind.CompiledIc,
            "run-date-utc" => CompiledOutputTokenSourceKind.RunDateUtc,
            "dpcmi-version" => CompiledOutputTokenSourceKind.DpcmiVersion,
            "firmware-config-tp-version" =>
                CompiledOutputTokenSourceKind.FirmwareConfigTpVersion,
            _ => throw Error($"{path}.source.kind", "Unknown output token source kind."),
        };
        bool isMetadataSource = sourceKind is
            CompiledOutputTokenSourceKind.DpcmiVersion or
            CompiledOutputTokenSourceKind.FirmwareConfigTpVersion;
        string? metadataBindingId = isMetadataSource
            ? CanonicalPolicyValueRules.RequireCanonicalId(
                source.MetadataBindingId!,
                $"{path}.source.metadataBindingId")
            : source.MetadataBindingId;

        CompiledOutputTokenMissingPolicy missingPolicy =
            document.MissingPolicy switch
            {
                "block" => CompiledOutputTokenMissingPolicy.Block,
                "use-placeholder" =>
                    CompiledOutputTokenMissingPolicy.UsePlaceholder,
                _ => throw Error(
                    $"{path}.missingPolicy",
                    "Unknown output token missing policy."),
            };
        string? metadataSpaceId = null;
        if (metadataBindingId is not null)
        {
            CompositionProfileMetadataBinding? binding = metadataBindings.FirstOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.BindingId, metadataBindingId));
            if (binding is null ||
                !binding.Purposes.Contains(CompositionProfileMetadataPurpose.OutputNaming))
            {
                throw Error(
                    $"{path}.source.metadataBindingId",
                    "Expected one metadata binding with output-naming purpose.");
            }

            // A family-local structure id may reference the canonical definition.
            // Exact canonical identity is therefore checked after map resolution by
            // CapabilityPublicationCoherence, not inferred from this local alias.
            metadataSpaceId = binding.SpaceId;
        }

        return new CompiledOutputTokenRequirement(
            CanonicalPolicyValueRules.RequireCanonicalId(
                document.TokenId,
                $"{path}.tokenId"),
            sourceKind,
            metadataBindingId,
            missingPolicy,
            document.Placeholder,
            metadataSpaceId);
    }
}
