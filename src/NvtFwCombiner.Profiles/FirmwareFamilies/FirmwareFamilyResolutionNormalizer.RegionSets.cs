using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

internal static partial class FirmwareFamilyResolutionNormalizer
{
    private static Dictionary<string, FirmwareRegionSet> NormalizeRegionSets(
        IReadOnlyList<FirmwareRegionSetDocument> documents)
    {
        Dictionary<string, FirmwareRegionSetDocument> documentsById = IndexUnique(
            documents,
            static document => document.RegionSetId,
            "regionSets",
            "regionSetId");
        Dictionary<string, FirmwareRegionSet> normalized = new(StringComparer.Ordinal);
        foreach ((string regionSetId, FirmwareRegionSetDocument document) in documentsById)
        {
            string path = $"regionSets[{regionSetId}]";
            IReadOnlyList<FirmwareRegionDocument> regionDocuments = document.Regions;
            FirmwareRegion[] regions = NormalizeItems(
                regionDocuments,
                $"{path}.regions",
                NormalizeRegion);

            IReadOnlyList<FirmwareRegionTemplateDocument> templateDocuments =
                document.RegionTemplates ?? [];
            IReadOnlyList<FirmwareRegionInstanceDocument> instanceDocuments =
                document.RegionInstances ?? [];

            Dictionary<string, FirmwareRegionTemplate> templates =
                NormalizeRegionTemplates(templateDocuments, path);
            FirmwareRegionInstance[] instances =
                NormalizeRegionInstances(instanceDocuments, templates, path);
            TranslateInvariant(path, () => normalized.Add(
                regionSetId,
                new FirmwareRegionSet(
                    regionSetId,
                    document.AddressSpaceId,
                    regions,
                    document.EvidenceRefs,
                    templates.Values,
                    instances)));
        }

        return normalized;
    }

    private static Dictionary<string, FirmwareRegionTemplate> NormalizeRegionTemplates(
        IReadOnlyList<FirmwareRegionTemplateDocument> documents,
        string regionSetPath)
    {
        Dictionary<string, FirmwareRegionTemplateDocument> documentsById = IndexUnique(
            documents,
            static document => document.TemplateId,
            $"{regionSetPath}.regionTemplates",
            "templateId");
        Dictionary<string, FirmwareRegionTemplate> normalized = new(StringComparer.Ordinal);
        foreach ((string templateId, FirmwareRegionTemplateDocument document) in documentsById)
        {
            string path = $"{regionSetPath}.regionTemplates[{templateId}]";
            IReadOnlyList<FirmwareRegionDocument> regionDocuments = document.Regions;
            FirmwareRelativeRegion[] regions = NormalizeItems(
                regionDocuments,
                $"{path}.regions",
                (region, regionPath) =>
                {
                    FirmwareRegion physical = NormalizeRegion(region, regionPath);
                    return TranslateInvariant(
                    regionPath,
                    () => new FirmwareRelativeRegion(
                        physical.RegionId,
                        physical.ParentRegionId,
                        physical.Owner,
                        physical.Kind,
                        physical.Range,
                        physical.WriteConstraint,
                        physical.Alignment));
                });

            TranslateInvariant(path, () => normalized.Add(
                templateId,
                new FirmwareRegionTemplate(
                    templateId,
                    ReadInt64(document.CapacityBytes, $"{path}.capacityBytes"),
                    regions)));
        }

        return normalized;
    }

    private static FirmwareRegionInstance[] NormalizeRegionInstances(
        IReadOnlyList<FirmwareRegionInstanceDocument> documents,
        Dictionary<string, FirmwareRegionTemplate> templates,
        string regionSetPath)
    {
        Dictionary<string, FirmwareRegionInstanceDocument> documentsById = IndexUnique(
            documents,
            static document => document.InstanceId,
            $"{regionSetPath}.regionInstances",
            "instanceId");
        var instances = new FirmwareRegionInstance[documentsById.Count];
        int index = 0;
        foreach ((string instanceId, FirmwareRegionInstanceDocument document) in documentsById)
        {
            string path = $"{regionSetPath}.regionInstances[{instanceId}]";
            if (!templates.TryGetValue(document.TemplateId, out FirmwareRegionTemplate? template))
            {
                throw Error(
                    $"{path}.templateId",
                    $"Unknown region template '{document.TemplateId}'.");
            }

            IReadOnlyList<FirmwareRegionIdBindingDocument> bindingDocuments = document.ResolvedRegionIds;
            Dictionary<string, FirmwareRegionIdBindingDocument> bindings = IndexUnique(
                bindingDocuments,
                static binding => binding.TemplateRegionId,
                $"{path}.resolvedRegionIds",
                "templateRegionId");
            var resolvedIds = bindings.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.ResolvedRegionId,
                StringComparer.Ordinal);

            instances[index++] = TranslateInvariant(path, () => new FirmwareRegionInstance(
                instanceId,
                template,
                ReadInt64(document.BaseOffset, $"{path}.baseOffset"),
                document.ParentRegionId,
                resolvedIds));
        }

        return instances;
    }

    private static FirmwareRegion NormalizeRegion(FirmwareRegionDocument document, string path)
    {
        return TranslateInvariant(path, () => new FirmwareRegion(
                document.RegionId,
                document.ParentRegionId,
                NormalizeOwner(document.Owner, $"{path}.owner"),
                NormalizeRegionKind(document.Kind, $"{path}.kind"),
                NormalizeRange(document.Range, $"{path}.range"),
                NormalizeWriteConstraint(document.WriteConstraint, $"{path}.writeConstraint"),
                ReadInt32(document.Alignment, $"{path}.alignment")));
    }
}
