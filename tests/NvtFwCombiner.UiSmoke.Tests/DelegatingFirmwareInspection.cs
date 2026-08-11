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

    public FirmwareConfigMetadataReadResult ReadFirmwareConfigMetadata(
        string icId,
        string path)
    {
        if (_metadataReader is null)
        {
            return _inner.ReadFirmwareConfigMetadata(icId, path);
        }

        FirmwareFileIdentity before = Capture(path);
        FirmwareConfigMetadataSnapshot? metadata = _metadataReader(icId, path);
        FirmwareFileIdentity after = Capture(path);
        return new FirmwareConfigMetadataReadResult(metadata, before, before.Equals(after));
    }

    public FirmwareInspectionBatchResult InspectFirmwareBatch(
        string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs)
    {
        if (_batchReader is null)
        {
            return _inner.InspectFirmwareBatch(icId, inputs);
        }

        string[] distinctPaths =
        [
            .. inputs
                .SelectMany(static input => new[] { input.Path, input.TpPath })
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Select(static path => path!)
                .Distinct(StringComparer.Ordinal),
        ];
        Dictionary<string, FirmwareFileIdentity> before = distinctPaths.ToDictionary(
            static path => path,
            Capture,
            StringComparer.Ordinal);
        IReadOnlyList<FirmwareInspectionSnapshotResult> inspections = _batchReader(icId, inputs);
        Dictionary<string, FirmwareFileIdentity> after = distinctPaths.ToDictionary(
            static path => path,
            Capture,
            StringComparer.Ordinal);
        return new FirmwareInspectionBatchResult(
            inspections.ToDictionary(
                static result => result.InspectionId,
                static result => result.Inspection,
                StringComparer.Ordinal),
            after,
            distinctPaths.Where(path => !before[path].Equals(after[path])));
    }

    public bool IsFirmwareFileIdentityCurrent(string path, FirmwareFileIdentity identity)
    {
        return identity.Equals(Capture(path));
    }

    public CtrlRamInspectionDisplay ProjectCtrlRamInspectionDisplay(
        string icId,
        string numberToken,
        FirmwareConfigMetadataSnapshot? baseFirmware)
    {
        return _inner.ProjectCtrlRamInspectionDisplay(icId, numberToken, baseFirmware);
    }

    private static FirmwareFileIdentity Capture(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return default;
        }

        try
        {
            var file = new FileInfo(path);
            return file.Exists
                ? new FirmwareFileIdentity(true, file.Length, file.LastWriteTimeUtc)
                : default;
        }
        catch (Exception exception) when (exception is
            IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            return default;
        }
    }
}
