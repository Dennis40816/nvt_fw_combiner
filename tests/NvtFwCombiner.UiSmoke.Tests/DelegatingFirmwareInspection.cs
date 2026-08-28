namespace NvtFwCombiner.UiSmoke.Tests;

internal sealed class DelegatingFirmwareInspection : IFirmwareInspection
{
    private readonly Func<
        string,
        IReadOnlyList<FirmwareInspectionSnapshotInput>,
        IReadOnlyList<FirmwareInspectionSnapshotResult>>? _batchReader;
    private readonly IFirmwareInspection _inner;

    internal DelegatingFirmwareInspection(
        IFirmwareInspection inner,
        Func<
            string,
            IReadOnlyList<FirmwareInspectionSnapshotInput>,
            IReadOnlyList<FirmwareInspectionSnapshotResult>>? batchReader = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _batchReader = batchReader;
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
            return before;
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
        return _inner.ProjectCtrlRamInspectionDisplay(icId, numberToken, baseFirmware);
    }

}
