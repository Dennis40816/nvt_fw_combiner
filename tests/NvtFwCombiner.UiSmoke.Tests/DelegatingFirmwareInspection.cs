using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.UiSmoke.Tests;

internal sealed class DelegatingFirmwareInspection : IFirmwareInspection
{
    private readonly Func<
        string,
        IReadOnlyList<FirmwareInspectionSnapshotInput>,
        IReadOnlyList<FirmwareInspectionSnapshotResult>>? _batchReader;
    private readonly IFirmwareInspection _inner;
    private readonly Func<string, string, FirmwareConfigMetadataSnapshot?>? _metadataReader;

    internal DelegatingFirmwareInspection(
        IFirmwareInspection inner,
        Func<string, string, FirmwareConfigMetadataSnapshot?>? metadataReader = null,
        Func<
            string,
            IReadOnlyList<FirmwareInspectionSnapshotInput>,
            IReadOnlyList<FirmwareInspectionSnapshotResult>>? batchReader = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _metadataReader = metadataReader;
        _batchReader = batchReader;
    }

    public async ValueTask<FirmwareConfigMetadataReadResult> ReadFirmwareConfigMetadataAsync(
        string icId,
        string path,
        CancellationToken cancellationToken)
    {
        FirmwareConfigMetadataReadResult before = await _inner
            .ReadFirmwareConfigMetadataAsync(icId, path, cancellationToken);
        if (_metadataReader is null)
        {
            return before;
        }

        FirmwareConfigMetadataSnapshot? metadata = _metadataReader(icId, path);
        FirmwareConfigMetadataReadResult after = await _inner
            .ReadFirmwareConfigMetadataAsync(icId, path, cancellationToken);
        return new FirmwareConfigMetadataReadResult(
            metadata,
            before.FileStamp == after.FileStamp ? before.FileStamp : null,
            before.IsContentStable &&
                after.IsContentStable &&
                before.FileStamp == after.FileStamp);
    }

    public async ValueTask<FirmwareInspectionBatchResult> InspectFirmwareBatchAsync(
        string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs,
        CancellationToken cancellationToken)
    {
        FirmwareInspectionBatchResult before = await _inner
            .InspectFirmwareBatchAsync(icId, inputs, cancellationToken);
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

    public ValueTask<bool> IsFirmwareContentCurrentAsync(
        string path,
        FileStamp identity,
        CancellationToken cancellationToken)
    {
        return _inner.IsFirmwareContentCurrentAsync(path, identity, cancellationToken);
    }

    public CtrlRamInspectionDisplay ProjectCtrlRamInspectionDisplay(
        string icId,
        string numberToken,
        FirmwareConfigMetadataSnapshot? baseFirmware)
    {
        return _inner.ProjectCtrlRamInspectionDisplay(icId, numberToken, baseFirmware);
    }

}
