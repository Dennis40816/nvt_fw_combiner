namespace NvtFwCombiner.Infrastructure.Composition;

internal sealed partial class BuiltInFirmwareInspection
{
    public FirmwareConfigMetadataReadResult ReadFirmwareConfigMetadata(
        string icId,
        string path)
    {
        FirmwareFileIdentity before = CaptureFirmwareFileIdentity(path);
        FirmwareConfigMetadataSnapshot? metadata =
            TryReadFirmwareConfigMetadata(_projection, icId, path);
        FirmwareFileIdentity after = CaptureFirmwareFileIdentity(path);
        return new FirmwareConfigMetadataReadResult(
            metadata,
            before,
            before.Equals(after));
    }

    public FirmwareInspectionBatchResult InspectFirmwareBatch(
        string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
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
            CaptureFirmwareFileIdentity,
            StringComparer.Ordinal);
        IReadOnlyList<FirmwareInspectionSnapshotResult> inspections =
            InspectFirmwareBatch(this, icId, inputs);
        Dictionary<string, FirmwareFileIdentity> after = distinctPaths.ToDictionary(
            static path => path,
            CaptureFirmwareFileIdentity,
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
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return identity.Equals(CaptureFirmwareFileIdentity(path));
    }

    internal static FirmwareFileIdentity CaptureFirmwareFileIdentity(string? path)
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
