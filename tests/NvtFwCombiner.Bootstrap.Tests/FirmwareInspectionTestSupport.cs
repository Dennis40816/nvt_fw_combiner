namespace NvtFwCombiner.Bootstrap.Tests;

internal static class FirmwareInspectionTestSupport
{
    internal static WorkbenchFirmwareInspection InspectFirmware(
        string icId,
        string path,
        string? tpPath = null,
        WorkbenchCtrlRamInspectionRequest? ctrlRamRequest = null)
    {
        return FirmwareInspectionAdapter.InspectFirmwareBatch(
            icId,
            [new WorkbenchFirmwareInspectionInput("test", path, tpPath, ctrlRamRequest)])
            .Single()
            .Inspection;
    }

    internal static WorkbenchFirmwareInspection InspectFirmware(
        string icId,
        string path,
        string? tpPath,
        WorkbenchCtrlRamInspectionRequest? ctrlRamRequest,
        Func<string, byte[]?> readFirmwareImage)
    {
        return FirmwareInspectionAdapter.InspectFirmware(
            icId,
            path,
            tpPath,
            ctrlRamRequest,
            readFirmwareImage);
    }

    internal static WorkbenchDpVersionMetadata? TryReadDpVersionMetadata(
        string icId,
        string path)
    {
        return InspectFirmware(icId, path).DpVersion;
    }

    internal static WorkbenchCmiDpCodeMetadata? TryReadCmiDpCodeMetadata(
        string icId,
        string path,
        string? tpPath = null)
    {
        return InspectFirmware(icId, path, tpPath).CmiDpCode;
    }

    internal static WorkbenchFirmwareContextSuggestion? TryReadFirmwareContextSuggestion(
        string icId,
        string path)
    {
        return InspectFirmware(icId, path).ContextSuggestion;
    }
}

internal static class OutputNamingTestSupport
{
    internal static WorkbenchOutputFileNameSuggestion CreateFlashCodeOutputFileName(
        string icId,
        IReadOnlyList<WorkbenchOutputNameCandidate> candidates,
        DateOnly? date = null)
    {
        string? cmiTpPath = candidates
            .Where(static candidate => candidate.Kind is WorkbenchOutputNameCandidateKind.Tp or WorkbenchOutputNameCandidateKind.Base)
            .OrderBy(static candidate => candidate.Kind == WorkbenchOutputNameCandidateKind.Tp ? 0 : 1)
            .Select(static candidate => candidate.Path)
            .FirstOrDefault(static path => !string.IsNullOrWhiteSpace(path));
        WorkbenchFirmwareInspectionInput[] inputs =
        [
            .. candidates
                .Select((candidate, index) => (Candidate: candidate, Index: index))
                .Where(static item => !string.IsNullOrWhiteSpace(item.Candidate.Path))
                .Select(item => new WorkbenchFirmwareInspectionInput(
                    item.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    item.Candidate.Path!,
                    item.Candidate.Kind == WorkbenchOutputNameCandidateKind.Dp
                        ? cmiTpPath
                        : null)),
        ];
        IReadOnlyList<WorkbenchFirmwareInspectionResult> inspections =
            FirmwareInspectionAdapter.InspectFirmwareBatch(icId, inputs);
        IReadOnlyDictionary<string, WorkbenchFirmwareInspection> byId = inspections
            .ToDictionary(static item => item.InspectionId, static item => item.Inspection, StringComparer.Ordinal);
        WorkbenchOutputNameInspectionCandidate[] inspectedCandidates =
        [
            .. candidates.Select((candidate, index) => new WorkbenchOutputNameInspectionCandidate(
                candidate.Kind,
                byId.GetValueOrDefault(index.ToString(System.Globalization.CultureInfo.InvariantCulture)))),
        ];
        return CompositionOutputNaming.CreateFlashCodeOutputFileNameFromInspections(
            icId,
            inspectedCandidates,
            date);
    }
}
