using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    internal static CompositionProfileDefinition Normalize(CompositionProfileDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion is not ("2.0" or "2.1" or "2.2" or "2.3" or "2.4" or "2.5" or "2.6" or "2.7" or "2.8" or "2.9" or "2.10" or "2.11" or "2.12" or "2.13" or "2.14" or "2.15"))
        {
            throw Error("schemaVersion", "Expected composition-profile schema version '2.0' through '2.15'.");
        }

        CompositionKind compositionKind = NormalizeCompositionKind(
            document.CompositionKind,
            "compositionKind");
        IcNumberInputMode? icNumberInputMode = NormalizeIcNumberInputMode(
            document.IcNumberInputMode,
            "icNumberInputMode");
        CompiledProfilePromotion promotion = NormalizePromotion(
            RequireObject(document.Promotion, "promotion"),
            "promotion");
        CompositionProfileExperience experience = NormalizeExperience(
            RequireObject(document.Experience, "experience"),
            "experience");
        CompositionProfileCompilationContext compilationContext = NormalizeCompilationContext(document);
        CompositionProfileInputSlot[] inputSlots = NormalizeList(
            document.InputSlots,
            "inputSlots",
            (slot, path) => NormalizeInputSlot(slot, document.SchemaVersion, path));
        CompositionProfileInputSelectionGroup[] inputSelectionGroups =
            document.InputSelectionGroups is null
                ? []
                : NormalizeList(
                    document.InputSelectionGroups,
                    "inputSelectionGroups",
                    NormalizeInputSelectionGroup);
        CompositionProfileSpace[] spaces = NormalizeList(
            document.Spaces,
            "spaces",
            (space, path) => NormalizeSpace(space, document.SchemaVersion, path));
        CompositionProfileView[] views = NormalizeList(
            document.Views,
            "views",
            (view, path) => NormalizeView(view, path, document.SchemaVersion));
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
            (operation, path) => NormalizeOperation(operation, path, document.SchemaVersion));
        CompositionProfileValidation[] validations = NormalizeList(
            document.Validations,
            "validations",
            NormalizeValidation);
        CompositionProfileProcessorStage[] processorStages = NormalizeList(
            document.ProcessorStages,
            "processorStages",
            (stage, path) => NormalizeProcessorStage(stage, document.SchemaVersion, path));
        CompositionProfileOutput output = NormalizeOutput(
            RequireObject(document.Output, "output"),
            document.SchemaVersion,
            "output");
        IReadOnlyList<string> evidenceRefs = RequireList(document.EvidenceRefs, "evidenceRefs");

        return Wrap("$", () => new CompositionProfileDefinition(
            document.ProfileId,
            document.ProfileVersion,
            promotion,
            compositionKind,
            icNumberInputMode,
            experience,
            compilationContext,
            inputSlots,
            spaces,
            views,
            metadataBindings,
            regionAccessRules,
            operations,
            validations,
            processorStages,
            output,
            evidenceRefs,
            inputSelectionGroups));
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
