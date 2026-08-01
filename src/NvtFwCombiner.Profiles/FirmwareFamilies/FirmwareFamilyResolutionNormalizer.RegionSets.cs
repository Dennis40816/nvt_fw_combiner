using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.FirmwareFamilies;

public static partial class FirmwareFamilyResolutionNormalizer
{
    private static Dictionary<string, FirmwareRegionSet> NormalizeRegionSets(
        IReadOnlyList<FirmwareRegionSetDocument> documents,
        string schemaVersion)
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
            IReadOnlyList<FirmwareRegionDocument> regionDocuments =
                RequireList(document.Regions, $"{path}.regions");
            var regions = new FirmwareRegion[regionDocuments.Count];
            for (int index = 0; index < regionDocuments.Count; index++)
            {
                regions[index] = NormalizeRegion(regionDocuments[index], $"{path}.regions[{index}]");
            }

            bool hasTemplateDeclarations = document.RegionTemplates is not null;
            bool hasInstanceDeclarations = document.RegionInstances is not null;
            if (hasTemplateDeclarations != hasInstanceDeclarations)
            {
                throw Error(
                    path,
                    "Region templates and region instances must be declared together.");
            }

            IReadOnlyList<FirmwareRegionTemplateDocument> templateDocuments =
                document.RegionTemplates is null
                    ? []
                    : RequireList(document.RegionTemplates, $"{path}.regionTemplates");
            IReadOnlyList<FirmwareRegionInstanceDocument> instanceDocuments =
                document.RegionInstances is null
                    ? []
                    : RequireList(document.RegionInstances, $"{path}.regionInstances");
            if (schemaVersion != "1.2" &&
                (templateDocuments.Count != 0 || instanceDocuments.Count != 0))
            {
                throw Error(
                    path,
                    "Instance-relative region definitions require firmware-family schema version '1.2'.");
            }

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
            IReadOnlyList<FirmwareRegionDocument> regionDocuments =
                RequireList(document.Regions, $"{path}.regions");
            var regions = new FirmwareRelativeRegion[regionDocuments.Count];
            for (int index = 0; index < regionDocuments.Count; index++)
            {
                FirmwareRegion physical = NormalizeRegion(
                    regionDocuments[index],
                    $"{path}.regions[{index}]");
                regions[index] = TranslateInvariant(
                    $"{path}.regions[{index}]",
                    () => new FirmwareRelativeRegion(
                        physical.RegionId,
                        physical.ParentRegionId,
                        physical.Owner,
                        physical.Kind,
                        physical.Range,
                        physical.WriteConstraint,
                        physical.Alignment));
            }

            TranslateInvariant(path, () => normalized.Add(
                templateId,
                new FirmwareRegionTemplate(
                    templateId,
                    ReadInt64(
                        document.CapacityBytes,
                        1,
                        long.MaxValue,
                        $"{path}.capacityBytes"),
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

            IReadOnlyList<FirmwareRegionIdBindingDocument> bindingDocuments =
                RequireList(document.ResolvedRegionIds, $"{path}.resolvedRegionIds");
            Dictionary<string, FirmwareRegionIdBindingDocument> bindings = IndexUnique(
                bindingDocuments,
                static binding => binding.TemplateRegionId,
                $"{path}.resolvedRegionIds",
                "templateRegionId");
            var resolvedIds = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string templateRegionId, FirmwareRegionIdBindingDocument binding) in bindings)
            {
                if (!resolvedIds.TryAdd(templateRegionId, binding.ResolvedRegionId))
                {
                    throw Error(
                        $"{path}.resolvedRegionIds",
                        $"Duplicate template region id '{templateRegionId}'.");
                }
            }

            instances[index++] = TranslateInvariant(path, () => new FirmwareRegionInstance(
                instanceId,
                template,
                ReadInt64(document.BaseOffset, 0, long.MaxValue, $"{path}.baseOffset"),
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
                ReadInt32(document.Alignment, 1, int.MaxValue, $"{path}.alignment")));
    }
}
