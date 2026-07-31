using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionOutputNameResolverTests
{
    private static CompiledOutputNamingRequirement NormalFlashCodeOutput()
    {
        return new CompiledOutputNamingRequirement(
            CompiledOutputNamingRequirement.NormalFlashCodeV1Template,
            allowOverride: true,
            CompiledOutputInvalidCharacterPolicy.Reject,
            ["date", "dp-version", "ic", "tp-version"],
            CompiledOutputNamingRequirement.NormalFlashCodeV1RuleId,
            CompiledOutputArtifactType.FlashCode,
            [
                OutputToken(
                    "date",
                    CompiledOutputTokenSourceKind.RunDateUtc,
                    metadataBindingId: null,
                    CompiledOutputTokenMissingPolicy.Block,
                    placeholder: null),
                OutputToken(
                    "dp-version",
                    CompiledOutputTokenSourceKind.DpcmiVersion,
                    "dpcmi-naming",
                    CompiledOutputTokenMissingPolicy.UsePlaceholder,
                    "xxxx"),
                OutputToken(
                    "ic",
                    CompiledOutputTokenSourceKind.CompiledIc,
                    metadataBindingId: null,
                    CompiledOutputTokenMissingPolicy.Block,
                    placeholder: null),
                OutputToken(
                    "tp-version",
                    CompiledOutputTokenSourceKind.FirmwareConfigTpVersion,
                    "firmware-config-general-parameters-naming",
                    CompiledOutputTokenMissingPolicy.UsePlaceholder,
                    "xxxx"),
            ]);
    }

    private static CompiledOutputNamingRequirement TpFirmwareOutput()
    {
        return new CompiledOutputNamingRequirement(
            CompiledOutputNamingRequirement.TpFirmwareV1Template,
            allowOverride: true,
            CompiledOutputInvalidCharacterPolicy.Reject,
            ["date", "ic", "tp-version"],
            CompiledOutputNamingRequirement.TpFirmwareV1RuleId,
            CompiledOutputArtifactType.TpFirmware,
            [
                OutputToken(
                    "date",
                    CompiledOutputTokenSourceKind.RunDateUtc,
                    metadataBindingId: null,
                    CompiledOutputTokenMissingPolicy.Block,
                    placeholder: null),
                OutputToken(
                    "ic",
                    CompiledOutputTokenSourceKind.CompiledIc,
                    metadataBindingId: null,
                    CompiledOutputTokenMissingPolicy.Block,
                    placeholder: null),
                OutputToken(
                    "tp-version",
                    CompiledOutputTokenSourceKind.FirmwareConfigTpVersion,
                    "firmware-config-general-parameters-naming",
                    CompiledOutputTokenMissingPolicy.UsePlaceholder,
                    "xxxx"),
            ]);
    }

    private static CompiledOutputTokenRequirement OutputToken(
        string tokenId,
        CompiledOutputTokenSourceKind sourceKind,
        string? metadataBindingId,
        CompiledOutputTokenMissingPolicy missingPolicy,
        string? placeholder)
    {
        return new CompiledOutputTokenRequirement(
            tokenId,
            sourceKind,
            metadataBindingId,
            missingPolicy,
            placeholder,
            metadataBindingId is null ? null : ArtifactBindingId);
    }
}
