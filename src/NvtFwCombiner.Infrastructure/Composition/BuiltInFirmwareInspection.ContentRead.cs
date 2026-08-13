using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.Composition;

internal sealed partial class BuiltInFirmwareInspection
{
    public async ValueTask<FirmwareConfigMetadataReadResult> ReadFirmwareConfigMetadataAsync(
        string icId,
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FirmwareContentRead file =
            await ReadFirmwareFileAsync(path, cancellationToken).ConfigureAwait(false);
        return new FirmwareConfigMetadataReadResult(
            file.Image is null ? null : ReadFirmwareConfigMetadata(_projection, icId, file.Image),
            file.FileStamp,
            file.IsStable);
    }

    public async ValueTask<FirmwareInspectionBatchResult> InspectFirmwareBatchAsync(
        string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs,
        CancellationToken cancellationToken)
    {
        ValidateInspectionInputs(icId, inputs);
        var files = new Dictionary<string, FirmwareContentRead>(StringComparer.Ordinal);
        foreach (string path in inputs
                     .SelectMany(static input => new[] { input.Path, input.TpPath })
                     .Where(static path => !string.IsNullOrWhiteSpace(path))
                     .Select(static path => path!)
                     .Distinct(StringComparer.Ordinal))
        {
            files.Add(
                path,
                await ReadFirmwareFileAsync(path, cancellationToken).ConfigureAwait(false));
        }

        IReadOnlyList<FirmwareInspectionSnapshotResult> inspections =
            InspectFirmwareBatch(this, icId, inputs, path => files[path].Image);
        return new FirmwareInspectionBatchResult(
            inspections.ToDictionary(
                static result => result.InspectionId,
                static result => result.Inspection,
                StringComparer.Ordinal),
            files.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.FileStamp,
                StringComparer.Ordinal),
            files.Where(static pair => !pair.Value.IsStable).Select(static pair => pair.Key));
    }

    public async ValueTask<bool> IsFirmwareContentCurrentAsync(
        string path,
        FileStamp identity,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FirmwareContentRead file =
            await ReadFirmwareFileAsync(path, cancellationToken).ConfigureAwait(false);
        return file.FileStamp == identity;
    }

    private async ValueTask<FirmwareContentRead> ReadFirmwareFileAsync(
            string path,
            CancellationToken cancellationToken)
    {
        try
        {
            SelectedFileContentInspection inspection = await _contentInspector
                .InspectAsync(path, int.MaxValue, cancellationToken)
                .ConfigureAwait(false);
            byte[] image = inspection.AcceptedBytes?.ToArray() ??
                throw new InvalidOperationException(
                    "The firmware content adapter did not retain its accepted bytes.");
            return new FirmwareContentRead(inspection.FileStamp, image, IsStable: true);
        }
        catch (SelectedFileChangedDuringInspectionException)
        {
            return new FirmwareContentRead(null, null, IsStable: false);
        }
        catch (Exception exception) when (exception is
            SelectedFileSizeLimitExceededException or
            IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            return new FirmwareContentRead(null, null, IsStable: true);
        }
    }

    private readonly record struct FirmwareContentRead(
        FileStamp? FileStamp,
        byte[]? Image,
        bool IsStable);
}
