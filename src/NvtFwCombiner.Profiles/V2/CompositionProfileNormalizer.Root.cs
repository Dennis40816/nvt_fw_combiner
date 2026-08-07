using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    internal static CompositionProfileDefinition Normalize(CompositionProfileDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        CompositionKind compositionKind = NormalizeCompositionKind(
            document.CompositionKind,
            "compositionKind");
        IcNumberInputMode? icNumberInputMode = NormalizeIcNumberInputMode(
            document.IcNumberInputMode,
            "icNumberInputMode");
        CompiledProfilePromotion promotion = NormalizePromotion(
            document.Promotion,
            "promotion");
        (
            string experienceId,
            LayoutPolicy layoutPolicy,
            InputPolicy inputPolicy) = NormalizeExperience(
                document.Experience,
                "experience");
        NormalizedCompilationContext compilationContext = NormalizeCompilationContext(document);
        var header = new CompositionProfileHeader(
            experienceId,
            layoutPolicy,
            inputPolicy,
            compilationContext.Kind,
            compilationContext.MapBinding,
            compilationContext.FamilyId,
            compilationContext.FamilyVersion,
            compilationContext.FamilyContentHash,
            Array.AsReadOnly([.. compilationContext.LogicalOutputMemberIds]),
            compilationContext.AllowsConditionalProcessor);
        CompositionInputSlotDefinition[] inputSlots = NormalizeList(
            document.InputSlots,
            "inputSlots",
            NormalizeInputSlot);
        InputSelectionGroupDefinition[] inputSelectionGroups =
            document.InputSelectionGroups is null
                ? []
                : NormalizeList(
                    document.InputSelectionGroups,
                    "inputSelectionGroups",
                    NormalizeInputSelectionGroup);
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
        CompositionOperationDefinition[] operations = NormalizeList(
            document.Operations,
            "operations",
            NormalizeOperation);
        ValidationRequirementDefinition[] validations = NormalizeList(
            document.Validations,
            "validations",
            NormalizeValidation);
        CompositionProfileProcessorStage[] processorStages = NormalizeList(
            document.ProcessorStages,
            "processorStages",
            NormalizeProcessorStage);
        CompiledOutputNamingRequirement output = NormalizeOutput(
            document.Output,
            "output",
            metadataBindings);

        return Wrap("$", () => new CompositionProfileDefinition(
            document.ProfileId,
            document.ProfileVersion,
            promotion,
            compositionKind,
            icNumberInputMode,
            header,
            inputSlots,
            spaces,
            views,
            metadataBindings,
            regionAccessRules,
            operations,
            validations,
            processorStages,
            output,
            document.EvidenceRefs,
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
        IReadOnlyList<TDocument> documents,
        string path,
        Func<TDocument, string, TValue> normalize)
    {
        var values = new TValue[documents.Count];
        for (int index = 0; index < documents.Count; index++)
        {
            values[index] = normalize(documents[index], $"{path}[{index}]");
        }

        return values;
    }
}
