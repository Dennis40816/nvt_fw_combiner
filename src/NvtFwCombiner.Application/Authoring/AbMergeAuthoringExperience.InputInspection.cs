using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Authoring;

internal sealed partial class AbMergeAuthoringExperience
{
    public AbMergeInspectionBatch InspectInputSlots(
        string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs,
        Func<string, byte[]?> readFirmwareImage)
    {
        FirmwareInspectionSnapshotInput[] selected =
        [
            .. inputs.Where(static input => input.AbMergeAddressSpaceId is not null),
        ];
        if (selected.Length == 0)
        {
            return AbMergeInspectionBatch.Empty;
        }

        var authoringRevision = new AuthoringRevision(
            selected.Select(static input => input.AuthoringRevision).Distinct().Single());
        string? topologyToken = selected
            .Select(static input => input.AbMergeTopologyToken)
            .Distinct(StringComparer.Ordinal)
            .Single();
        TopologySelection? topology =
            _compiler.ResolveAbMergeTopologySelection(
                icId,
                topologyToken);
        var resolver = new AbMergeAuthoringResolver(topology, _catalog);
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
        Dictionary<string, AbMergeInputFacts> facts = new(StringComparer.Ordinal);
        foreach (FirmwareInspectionSnapshotInput input in selected)
        {
            string addressSpaceId = input.AbMergeAddressSpaceId!;
            AuthoringInputSlotStatus status = batch.Statuses[addressSpaceId];
            var inputFacts = new AbMergeInputFacts(
                addressSpaceId,
                status.Observation.Versions);

            statuses.Add(input.InspectionId, status);
            facts.Add(input.InspectionId, inputFacts);
        }

        return new AbMergeInspectionBatch(
            batch.Catalog,
            statuses,
            facts,
            batch.Issues);
    }
}
