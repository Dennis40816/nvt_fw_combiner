using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

internal static partial class FirmwareFamilyResolutionNormalizer
{
    private static FirmwareFullImageMetadataView[]? NormalizeFullImageMetadataViews(
        IReadOnlyList<FirmwareFullImageMetadataViewDocument>? documents,
        Dictionary<string, FirmwareImageMap> maps,
        Dictionary<string, IReadOnlyDictionary<string, FirmwareMetadataStructure>> structuresByMap)
    {
        return documents is null ? null : NormalizeItems(documents, "fullImageMetadataViews", (document, path) =>
        {
            if (!maps.TryGetValue(document.MapId, out FirmwareImageMap? map))
            {
                throw Error($"{path}.mapId", $"Unknown full-image metadata map '{document.MapId}'.");
            }

            FirmwareFullImageMetadataBinding[] bindings = NormalizeItems(
                document.MetadataBindings, $"{path}.metadataBindings", (binding, bindingPath) =>
                {
                    if (!structuresByMap[document.MapId].TryGetValue(binding.StructureId, out FirmwareMetadataStructure? structure))
                    {
                        throw Error($"{bindingPath}.structureId", $"Unknown selected metadata structure '{binding.StructureId}'.");
                    }

                    FirmwareMetadataReferenceTarget[] targets = NormalizeItems(
                        binding.TargetReferences, $"{bindingPath}.targetReferences", (target, targetPath) =>
                            new FirmwareMetadataReferenceTarget(target.TargetKind switch
                            {
                                "span" => FirmwareMetadataReferenceTargetKind.Span,
                                "field" => FirmwareMetadataReferenceTargetKind.Field,
                                "series" => FirmwareMetadataReferenceTargetKind.Series,
                                "group" => FirmwareMetadataReferenceTargetKind.Group,
                                _ => throw Error($"{targetPath}.targetKind", "Unknown metadata target kind."),
                            }, target.TargetId));
                    return new FirmwareFullImageMetadataBinding(binding.BindingId, structure, targets, binding.EvidenceRefs);
                });
            return new FirmwareFullImageMetadataView(document.ViewId, map, document.MemberIds, bindings, document.EvidenceRefs);
        });
    }
}
