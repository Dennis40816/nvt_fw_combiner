using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Bootstrap;

internal sealed record DpPerspectiveDpReplaceRunContext(
    IcNumberSelection Selection,
    string BasePath,
    long Capacity,
    IReadOnlyDictionary<string, string> SlotPaths);

internal sealed record GeneralReplaceRunContext(
    IcNumberSelection Selection,
    IReadOnlyDictionary<string, string> ReportSlotPaths,
    string BasePath,
    long Capacity,
    WorkbenchGeneralReplaceMappingInput[] SelectedMappings,
    WorkbenchGeneralReplacePatchInput[] SelectedPatches);
