using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    internal static CompositionProfileDefinition Normalize(CompositionProfileDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!StringComparer.Ordinal.Equals(document.SchemaVersion, "2.0") &&
            !StringComparer.Ordinal.Equals(document.SchemaVersion, "2.1"))
        {
            throw Error("schemaVersion", "Expected composition-profile schema version '2.0' or '2.1'.");
        }

        CompositionKind compositionKind = NormalizeCompositionKind(
            document.CompositionKind,
            "compositionKind");
        IcNumberInputMode? icNumberInputMode = NormalizeIcNumberInputMode(
            document.IcNumberInputMode,
            "icNumberInputMode");
        CompositionProfilePromotion promotion = NormalizePromotion(
            RequireObject(document.Promotion, "promotion"),
            "promotion");
        CompositionProfileExperience experience = NormalizeExperience(
            RequireObject(document.Experience, "experience"),
            "experience");
        CompositionProfileMapBinding mapBinding = NormalizeMapBinding(
            RequireObject(document.MapBinding, "mapBinding"),
            "mapBinding");
        CompositionProfileInputSlot[] inputSlots = NormalizeList(
            document.InputSlots,
            "inputSlots",
            NormalizeInputSlot);
        CompositionProfileSpace[] spaces = NormalizeList(
            document.Spaces,
            "spaces",
            NormalizeSpace);
        CompositionProfileView[] views = NormalizeList(
            document.Views,
            "views",
            NormalizeView);
        CompositionProfileMetadataBinding[] metadataBindings = NormalizeList(
            document.MetadataBindings,
            "metadataBindings",
            NormalizeMetadataBinding);
        CompositionProfileRegionAccess[] regionAccessRules = NormalizeList(
            document.RegionAccessRules,
            "regionAccessRules",
            NormalizeRegionAccessRule);
        CompositionProfileOperation[] operations = NormalizeList(
            document.Operations,
            "operations",
            NormalizeOperation);
        CompositionProfileValidation[] validations = NormalizeList(
            document.Validations,
            "validations",
            NormalizeValidation);
        CompositionProfileProcessorStage[] processorStages = NormalizeList(
            document.ProcessorStages,
            "processorStages",
            NormalizeProcessorStage);
        CompositionProfileOutput output = NormalizeOutput(
            RequireObject(document.Output, "output"),
            "output");
        IReadOnlyList<string> evidenceRefs = RequireList(document.EvidenceRefs, "evidenceRefs");

        return Wrap("$", () => new CompositionProfileDefinition(
            document.ProfileId,
            document.ProfileVersion,
            promotion,
            compositionKind,
            icNumberInputMode,
            experience,
            mapBinding,
            inputSlots,
            spaces,
            views,
            metadataBindings,
            regionAccessRules,
            operations,
            validations,
            processorStages,
            output,
            evidenceRefs));
    }

    private static CompositionKind NormalizeCompositionKind(string value, string path)
    {
        return value switch
        {
            "merge" => CompositionKind.Merge,
            "replace" => CompositionKind.Replace,
            _ => throw Error(path, "Unknown composition kind."),
        };
    }

    private static IcNumberInputMode? NormalizeIcNumberInputMode(string? value, string path)
    {
        return value switch
        {
            null => null,
            "single-selector" => IcNumberInputMode.SingleSelector,
            "cascade-selector" => IcNumberInputMode.CascadeSelector,
            "numeric-selector" => IcNumberInputMode.NumericSelector,
            _ => throw Error(path, "Unknown IC-number input mode."),
        };
    }

    private static TValue[] NormalizeList<TDocument, TValue>(
        IReadOnlyList<TDocument>? documents,
        string path,
        Func<TDocument, string, TValue> normalize)
        where TDocument : class
    {
        IReadOnlyList<TDocument> required = RequireList(documents, path);
        var values = new TValue[required.Count];
        for (int index = 0; index < required.Count; index++)
        {
            TDocument document = required[index] ?? throw Error(
                $"{path}[{index}]",
                "Array value cannot be null.");
            values[index] = normalize(document, $"{path}[{index}]");
        }

        return values;
    }

    private static T RequireObject<T>(T? value, string path)
        where T : class
    {
        return value ?? throw Error(path, "Required object is missing.");
    }
}
