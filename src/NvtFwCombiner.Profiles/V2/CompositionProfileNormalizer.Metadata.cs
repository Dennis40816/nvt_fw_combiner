using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    internal static CompositionProfileMetadataBinding NormalizeMetadataBinding(
        CompositionProfileMetadataBindingDocument document,
        string path = "metadataBindings[0]")
    {
        ArgumentNullException.ThrowIfNull(document);
        IReadOnlyList<string> purposeDocuments = RequireList(document.Purposes, $"{path}.purposes");
        var purposes = new CompositionProfileMetadataPurpose[purposeDocuments.Count];
        for (int index = 0; index < purposeDocuments.Count; index++)
        {
            purposes[index] = NormalizeMetadataPurpose(
                purposeDocuments[index],
                $"{path}.purposes[{index}]");
        }

        return Wrap(path, () => new CompositionProfileMetadataBinding(
            document.BindingId,
            document.SpaceId,
            document.StructureId,
            RequireList(document.FieldIds, $"{path}.fieldIds"),
            purposes));
    }

    internal static CompositionProfileRegionAccess NormalizeRegionAccessRule(
        CompositionProfileRegionAccessRuleDocument document,
        string path = "regionAccessRules[0]")
    {
        ArgumentNullException.ThrowIfNull(document);
        return Wrap(path, () => new CompositionProfileRegionAccess(
            document.RegionId,
            NormalizeRegionAccess(document.Access, $"{path}.access"),
            document.Reason,
            document.AllowedSubregionIds));
    }

    private static CompositionProfileMetadataPurpose NormalizeMetadataPurpose(string value, string path)
    {
        return value switch
        {
            "map-resolution" => CompositionProfileMetadataPurpose.MapResolution,
            "validation" => CompositionProfileMetadataPurpose.Validation,
            "output-naming" => CompositionProfileMetadataPurpose.OutputNaming,
            "display" => CompositionProfileMetadataPurpose.Display,
            "version" => CompositionProfileMetadataPurpose.Version,
            _ => throw Error(path, "Unknown metadata binding purpose."),
        };
    }

    private static RegionAccessKind NormalizeRegionAccess(string value, string path)
    {
        return value switch
        {
            "hidden" => RegionAccessKind.Hidden,
            "read-only" => RegionAccessKind.ReadOnly,
            "whole" => RegionAccessKind.Whole,
            "parts" => RegionAccessKind.Parts,
            "explicit-range" => RegionAccessKind.ExplicitRange,
            _ => throw Error(path, "Unknown region access kind."),
        };
    }
}
