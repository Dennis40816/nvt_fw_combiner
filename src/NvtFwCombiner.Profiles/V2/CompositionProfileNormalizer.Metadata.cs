using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    internal static CompositionProfileMetadataBinding NormalizeMetadataBinding(
        CompositionProfileMetadataBindingDocument document,
        string path = "metadataBindings[0]")
    {
        ArgumentNullException.ThrowIfNull(document);
        IReadOnlyList<string> purposeDocuments = document.Purposes;
        var purposes = new CompositionProfileMetadataPurpose[purposeDocuments.Count];
        for (int index = 0; index < purposeDocuments.Count; index++)
        {
            purposes[index] = NormalizeMetadataPurpose(
                purposeDocuments[index],
                $"{path}.purposes[{index}]");
        }

        bool hasTypedTargets = document.TargetReferences is not null;
        FirmwareMetadataReferenceTarget[] targets;
        IReadOnlyList<string> evidenceRefs;
        if (hasTypedTargets)
        {
            IReadOnlyList<CompositionProfileMetadataTargetReferenceDocument> targetDocuments =
                document.TargetReferences!;
            targets = new FirmwareMetadataReferenceTarget[targetDocuments.Count];
            for (int index = 0; index < targetDocuments.Count; index++)
            {
                targets[index] = NormalizeMetadataTarget(
                    targetDocuments[index],
                    $"{path}.targetReferences[{index}]");
            }

            evidenceRefs = document.EvidenceRefs!;
        }
        else
        {
            IReadOnlyList<string> fieldIds = document.FieldIds!;
            targets =
            [
                .. fieldIds.Select(static fieldId =>
                    new FirmwareMetadataReferenceTarget(
                        FirmwareMetadataReferenceTargetKind.Field,
                        fieldId)),
            ];
            evidenceRefs = document.EvidenceRefs ?? [];
        }

        return Wrap(path, () => new CompositionProfileMetadataBinding(
            document.BindingId,
            document.SpaceId,
            document.StructureId,
            targets,
            purposes,
            evidenceRefs));
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
            "inspection" => CompositionProfileMetadataPurpose.Inspection,
            "formatting" => CompositionProfileMetadataPurpose.Formatting,
            "copy" => CompositionProfileMetadataPurpose.Copy,
            "relocation" => CompositionProfileMetadataPurpose.Relocation,
            "integrity" => CompositionProfileMetadataPurpose.Integrity,
            "processor" => CompositionProfileMetadataPurpose.Processor,
            "memory-projection" => CompositionProfileMetadataPurpose.MemoryProjection,
            "report-classification" => CompositionProfileMetadataPurpose.ReportClassification,
            _ => throw Error(path, "Unknown metadata binding purpose."),
        };
    }

    private static FirmwareMetadataReferenceTarget NormalizeMetadataTarget(
        CompositionProfileMetadataTargetReferenceDocument document,
        string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        FirmwareMetadataReferenceTargetKind kind = document.TargetKind switch
        {
            "span" => FirmwareMetadataReferenceTargetKind.Span,
            "field" => FirmwareMetadataReferenceTargetKind.Field,
            "series" => FirmwareMetadataReferenceTargetKind.Series,
            "group" => FirmwareMetadataReferenceTargetKind.Group,
            _ => throw Error($"{path}.targetKind", "Unknown metadata target kind."),
        };
        return Wrap(path, () =>
            new FirmwareMetadataReferenceTarget(
                kind,
                document.TargetId));
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
