namespace NvtFwCombiner.UiSmoke.Tests;

internal sealed class DelegatingFirmwareInspection : IFirmwareInspection
{
    private readonly Func<
        string,
        IReadOnlyList<FirmwareInspectionSnapshotInput>,
        IReadOnlyList<FirmwareInspectionSnapshotResult>>? _batchReader;
    private readonly IFirmwareInspection _inner;
    private readonly Func<CtrlRamInspectionDisplay, CtrlRamInspectionDisplay>? _displayProjector;

    internal DelegatingFirmwareInspection(
        IFirmwareInspection inner,
        Func<
            string,
            IReadOnlyList<FirmwareInspectionSnapshotInput>,
            IReadOnlyList<FirmwareInspectionSnapshotResult>>? batchReader = null,
        Func<CtrlRamInspectionDisplay, CtrlRamInspectionDisplay>? displayProjector = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _batchReader = batchReader;
        _displayProjector = displayProjector;
    }

    public async ValueTask<FirmwareInspectionBatchResult> InspectFirmwareBatchAsync(
        string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs,
        CancellationToken cancellationToken,
        IProgress<AuthoringInspectionProgress>? progress = null)
    {
        FirmwareInspectionBatchResult before = await _inner
            .InspectFirmwareBatchAsync(icId, inputs, cancellationToken, progress);
        if (_batchReader is null)
        {
            return _displayProjector is null ? before : new FirmwareInspectionBatchResult(
                before.InspectionsById.ToDictionary(static pair => pair.Key,
                    pair => pair.Value.CtrlRamDisplay is { } display
                        ? pair.Value with { CtrlRamDisplay = _displayProjector(display) }
                        : pair.Value, StringComparer.Ordinal),
                before.FileStamps, before.UnstableFilePaths);
        }

        IReadOnlyList<FirmwareInspectionSnapshotResult> inspections = _batchReader(icId, inputs);
        FirmwareInspectionBatchResult after = await _inner
            .InspectFirmwareBatchAsync(icId, inputs, cancellationToken);
        return new FirmwareInspectionBatchResult(
            inspections.ToDictionary(
                static result => result.InspectionId,
                static result => result.Inspection,
                StringComparer.Ordinal),
            before.FileStamps.ToDictionary(
                static pair => pair.Key,
                pair => pair.Value == after.FileStamps[pair.Key] ? pair.Value : null,
                StringComparer.Ordinal),
            before.FileStamps
                .Where(pair => pair.Value != after.FileStamps[pair.Key])
                .Select(static pair => pair.Key));
    }

    public CtrlRamInspectionDisplay ProjectCtrlRamInspectionDisplay(
        string icId,
        string numberToken,
        FirmwareConfigMetadataSnapshot? baseFirmware)
    {
        CtrlRamInspectionDisplay display = _inner.ProjectCtrlRamInspectionDisplay(icId, numberToken, baseFirmware);
        return _displayProjector?.Invoke(display) ?? display;
    }

}
