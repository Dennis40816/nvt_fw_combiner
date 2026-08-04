using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static WorkbenchAbMergeInspectionBatch InspectAbMergeInputSlots(
        string icId,
        IReadOnlyList<WorkbenchFirmwareInspectionInput> inputs,
        Func<string, byte[]?> readFirmwareImage)
    {
        WorkbenchFirmwareInspectionInput[] selected =
        [
            .. inputs.Where(static input => input.AbMergeAddressSpaceId is not null),
        ];
        if (selected.Length == 0)
        {
            return WorkbenchAbMergeInspectionBatch.Empty;
        }

        var authoringRevision = new AuthoringRevision(
            selected.Select(static input => input.AuthoringRevision).Distinct().Single());
        string? topologyToken = selected
            .Select(static input => input.AbMergeTopologyToken)
            .Distinct(StringComparer.Ordinal)
            .Single();
        TopologySelection? topology =
            AbMergeWorkbenchCompositionService.ResolveTopologySelection(topologyToken);
        var resolver = new AbMergeAuthoringResolver(topology);
        var service = new CompiledAuthoringWorkflowService(resolver);
        ResolvedCapability? exactCapability = selected[0].ExactCapability;
        if (selected.Any(input => !ReferenceEquals(input.ExactCapability, exactCapability)))
        {
            throw new InvalidOperationException("AB Merge inspection leases disagree on the exact compilation.");
        }
        CompiledAuthoringInspectionBatch batch = service.InspectBatch(
                icId,
                authoringRevision,
                [
                    .. selected.Select(input => new CompiledAuthoringSelectedInput(
                        input.AbMergeAddressSpaceId!,
                        input.Path,
                        readFirmwareImage(input.Path))),
                ],
                exactCapability);
        Dictionary<string, AuthoringInputSlotStatus> statuses = new(StringComparer.Ordinal);
        Dictionary<string, WorkbenchAbMergeInputFacts> facts = new(StringComparer.Ordinal);
        foreach (WorkbenchFirmwareInspectionInput input in selected)
        {
            string addressSpaceId = input.AbMergeAddressSpaceId!;
            AuthoringInputSlotStatus status = batch.Statuses[addressSpaceId];
            var inputFacts = new WorkbenchAbMergeInputFacts(
                addressSpaceId,
                status.Observation.Versions);

            statuses.Add(input.InspectionId, status);
            facts.Add(input.InspectionId, inputFacts);
        }

        return new WorkbenchAbMergeInspectionBatch(
            batch.Catalog,
            statuses,
            facts,
            batch.Issues);
    }
}
