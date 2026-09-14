using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

internal static partial class FirmwareFamilyResolutionNormalizer
{
    private static FirmwareAbFormatPolicy? NormalizeAbFormatPolicy(
        FirmwareAbFormatPolicyDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        const string Path = "abFormatPolicy";
        IReadOnlyList<FirmwareAbFormatDefinitionDocument> formatDocuments = document.Formats;
        FirmwareAbFormatDefinition[] formats = NormalizeItems(
            formatDocuments,
            $"{Path}.formats",
            NormalizeAbFormat);
        FirmwareAbPrimaryBindings primaryBindings = TranslateInvariant(
            $"{Path}.primaryBindings",
            () => new FirmwareAbPrimaryBindings(
                document.PrimaryBindings.TpAStructureId,
                document.PrimaryBindings.TpBStructureId,
                document.PrimaryBindings.FieldId,
                document.PrimaryBindings.RelationId));
        IReadOnlyList<FirmwareAbFormatVariantDocument> variantDocuments = document.Variants;
        FirmwareAbFormatVariant[] variants = NormalizeItems(
            variantDocuments,
            $"{Path}.variants",
            (variant, path) => TranslateInvariant(path, () => new FirmwareAbFormatVariant(
                variant.MemberId,
                variant.FormatId,
                variant.MapId)));

        return TranslateInvariant(Path, () => new FirmwareAbFormatPolicy(
            document.ScopeId,
            document.CommonFormatId,
            document.CommonDisplayName,
            formats,
            primaryBindings,
            variants));
    }

    private static FirmwareAbFormatDefinition NormalizeAbFormat(
        FirmwareAbFormatDefinitionDocument document,
        string path)
    {
        IReadOnlyList<int> recognitionDocuments = document.DefaultRecognitionValues;
        byte[] recognitionValues = NormalizeItems(
            recognitionDocuments,
            $"{path}.defaultRecognitionValues",
            (value, valuePath) => TranslateInvariant(
                valuePath,
                () => checked((byte)value)));
        return TranslateInvariant(path, () => new FirmwareAbFormatDefinition(
            document.UniqueId,
            document.DisplayName,
            recognitionValues));
    }
}
