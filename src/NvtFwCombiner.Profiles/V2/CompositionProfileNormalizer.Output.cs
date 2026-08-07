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
        ArgumentNullException.ThrowIfNull(document);
        string? ruleId = null;
        CompiledOutputArtifactType outputArtifactType =
            CompiledOutputArtifactType.Unspecified;
        OutputTokenBuilder[]? normalizedTokenRequirements = null;
        if (document.RuleId is not null)
        {
            ruleId = document.RuleId;
            outputArtifactType = NormalizeOutputArtifactType(
                document.OutputArtifactType!,
                $"{path}.outputArtifactType");
            normalizedTokenRequirements = NormalizeOutputTokenRequirements(
                document.TokenRequirements!,
                $"{path}.tokenRequirements");
        }

        CompiledOutputInvalidCharacterPolicy invalidCharacterPolicy =
            NormalizeInvalidCharacterPolicy(
                document.InvalidCharacterPolicy,
                $"{path}.invalidCharacterPolicy");

        CompiledOutputTokenRequirement[]? tokenRequirements = normalizedTokenRequirements?
            .Select((requirement, index) => CompileOutputTokenRequirement(
                requirement,
                $"{path}.tokenRequirements[{index}]",
                metadataBindings ?? []))
            .ToArray();

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

    private static OutputTokenBuilder[] NormalizeOutputTokenRequirements(
        IReadOnlyList<CompositionProfileOutputTokenRequirementDocument> documents,
        string path)
    {
        return NormalizeList(
            documents,
            path,
            NormalizeOutputTokenRequirement);
    }

    private static OutputTokenBuilder NormalizeOutputTokenRequirement(
        CompositionProfileOutputTokenRequirementDocument document,
        string path)
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
        return new OutputTokenBuilder(
            CanonicalPolicyValueRules.RequireCanonicalId(
                document.TokenId,
                $"{path}.tokenId"),
            sourceKind,
            metadataBindingId,
            missingPolicy,
            document.Placeholder);
    }

    private static CompiledOutputTokenRequirement CompileOutputTokenRequirement(
        OutputTokenBuilder requirement,
        string path,
        IReadOnlyList<CompositionProfileMetadataBinding> metadataBindings)
    {
        string? metadataSpaceId = null;
        if (requirement.MetadataBindingId is { } metadataBindingId)
        {
            CompositionProfileMetadataBinding? binding = metadataBindings.FirstOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.BindingId, metadataBindingId));
            string expectedStructureId =
                requirement.SourceKind == CompiledOutputTokenSourceKind.DpcmiVersion
                    ? "dpcmi"
                    : "firmware-config-general-parameters";
            if (binding is null ||
                !binding.Purposes.Contains(CompositionProfileMetadataPurpose.OutputNaming) ||
                !StringComparer.Ordinal.Equals(binding.StructureId, expectedStructureId))
            {
                throw Error(
                    $"{path}.source.metadataBindingId",
                    $"Expected one output-naming metadata binding for canonical structure '{expectedStructureId}'.");
            }

            metadataSpaceId = binding.SpaceId;
        }

        return new CompiledOutputTokenRequirement(
            requirement.TokenId,
            requirement.SourceKind,
            requirement.MetadataBindingId,
            requirement.MissingPolicy,
            requirement.Placeholder,
            metadataSpaceId);
    }

    private sealed record OutputTokenBuilder(
        string TokenId,
        CompiledOutputTokenSourceKind SourceKind,
        string? MetadataBindingId,
        CompiledOutputTokenMissingPolicy MissingPolicy,
        string? Placeholder);
}
