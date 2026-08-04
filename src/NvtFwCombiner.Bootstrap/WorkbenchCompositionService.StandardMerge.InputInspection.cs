using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static WorkbenchCompiledAuthoringInspectionBatch InspectStandardMergeInputSlots(
        string icId,
        IReadOnlyList<WorkbenchFirmwareInspectionInput> inputs,
        Func<string, byte[]?> readFirmwareImage)
    {
        WorkbenchFirmwareInspectionInput[] selected =
        [
            .. inputs.Where(static input => input.StandardMergeAddressSpaceId is not null),
        ];
        if (selected.Length == 0)
        {
            return WorkbenchCompiledAuthoringInspectionBatch.Empty;
        }

        var authoringRevision = new AuthoringRevision(
            selected.Select(static input => input.AuthoringRevision).Distinct().Single());
        ResolvedCapability? exactCapability = selected[0].ExactCapability;
        if (selected.Any(input => !ReferenceEquals(input.ExactCapability, exactCapability)))
        {
            throw new InvalidOperationException("Standard Merge inspection leases disagree on the exact compilation.");
        }
        CompiledAuthoringInspectionBatch batch = s_standardMergeAuthoring.InspectBatch(
            icId,
            authoringRevision,
            [
                .. selected.Select(input => new CompiledAuthoringSelectedInput(
                    input.StandardMergeAddressSpaceId!,
                    input.Path,
                    readFirmwareImage(input.Path))),
            ],
            exactCapability);
        return new WorkbenchCompiledAuthoringInspectionBatch(
            batch.Catalog,
            selected.ToDictionary(
                static input => input.InspectionId,
                input => batch.Statuses[input.StandardMergeAddressSpaceId!],
                StringComparer.Ordinal),
            batch.Issues);
    }
}
