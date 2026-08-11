using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Application.Authoring;

internal sealed partial class StandardMergeAuthoringExperience
{
    internal FirmwareInspectionStatusBatch InspectInputSlots(
        string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs,
        Func<string, byte[]?> readFirmwareImage)
    {
        FirmwareInspectionSnapshotInput[] selected =
        [
            .. inputs.Where(static input => input.StandardMergeAddressSpaceId is not null),
        ];
        if (selected.Length == 0)
        {
            return FirmwareInspectionStatusBatch.Empty;
        }

        var authoringRevision = new AuthoringRevision(
            selected.Select(static input => input.AuthoringRevision).Distinct().Single());
        ResolvedCapability? exactCapability = selected[0].ExactCapability;
        if (selected.Any(input => !ReferenceEquals(input.ExactCapability, exactCapability)))
        {
            throw new InvalidOperationException("Standard Merge inspection leases disagree on the exact compilation.");
        }
        CompiledAuthoringInspectionBatch batch = new CompiledAuthoringWorkflowService(
                new StandardMergeAuthoringResolver(this))
            .InspectBatch(
            icId,
            authoringRevision,
            [
                .. selected.Select(input => new CompiledAuthoringSelectedInput(
                    input.StandardMergeAddressSpaceId!,
                    input.Path,
                    readFirmwareImage(input.Path))),
            ],
            exactCapability);
        return new FirmwareInspectionStatusBatch(
            batch.Catalog,
            selected.ToDictionary(
                static input => input.InspectionId,
                input => batch.Statuses[input.StandardMergeAddressSpaceId!],
                StringComparer.Ordinal),
            batch.Issues);
    }
}
