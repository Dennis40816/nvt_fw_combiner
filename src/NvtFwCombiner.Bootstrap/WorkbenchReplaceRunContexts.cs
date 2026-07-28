using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Bootstrap;

[Flags]
internal enum DpReplacePartSelection
{
    None = 0,
    InitialCode = 1,
    Ldc = 2,
    All = InitialCode | Ldc,
}

internal sealed record BuiltInV2DpReplaceRunContext(
    IcNumberSelection Selection,
    string BasePath,
    long Capacity,
    DpReplacePartSelection SelectedParts,
    IReadOnlyDictionary<string, string> SlotPaths);

internal sealed record GeneralReplaceRunContext(
    IcNumberSelection Selection,
    IReadOnlyDictionary<string, string> ReportSlotPaths,
    string BasePath,
    long Capacity,
    WorkbenchGeneralReplaceMappingInput[] SelectedMappings,
    WorkbenchGeneralReplacePatchInput[] SelectedPatches);
