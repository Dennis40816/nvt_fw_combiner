using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static WorkbenchStandardMergeInspectionBatch InspectStandardMergeInputSlots(
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
            return WorkbenchStandardMergeInspectionBatch.Empty;
        }

        var authoringRevision = new AuthoringRevision(
            selected.Select(static input => input.AuthoringRevision).Distinct().Single());
        CompiledAuthoringInspectionBatch batch = s_standardMergeAuthoring.InspectBatch(
            icId,
            authoringRevision,
            [
                .. selected.Select(input => new CompiledAuthoringSelectedInput(
                    input.StandardMergeAddressSpaceId!,
                    input.Path,
                    readFirmwareImage(input.Path))),
            ]);
        return new WorkbenchStandardMergeInspectionBatch(
            batch.Catalog,
            selected.ToDictionary(
                static input => input.InspectionId,
                input => batch.Statuses[input.StandardMergeAddressSpaceId!],
                StringComparer.Ordinal),
            batch.Issues);
    }
}
