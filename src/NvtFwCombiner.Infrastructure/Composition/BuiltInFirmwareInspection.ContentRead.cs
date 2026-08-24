using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Infrastructure.Composition;

internal sealed partial class BuiltInFirmwareInspection
{
    public async ValueTask<FirmwareInspectionBatchResult> InspectFirmwareBatchAsync(
        string icId,
        IReadOnlyList<FirmwareInspectionSnapshotInput> inputs,
        CancellationToken cancellationToken,
        IProgress<AuthoringInspectionProgress>? progress = null)
    {
        ValidateInspectionInputs(icId, inputs);
        var files = new Dictionary<string, FirmwareContentRead>(StringComparer.Ordinal);
        string[] logicalPaths =
        [
            .. inputs.SelectMany(static input => new[] { input.Path, input.TpPath })
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Select(static path => path!)
                .Distinct(StringComparer.Ordinal),
        ];
        var identityPolicy = new FileSystemCompositionArtifactIdentityPolicy();
        var pathsByCanonicalIdentity = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var canonicalIdentityOrder = new List<string>();
        var unavailablePaths = new List<string>();
        foreach (string logicalPath in logicalPaths)
        {
            string canonicalIdentity;
            try
            {
                canonicalIdentity = identityPolicy.Resolve(logicalPath).CanonicalIdentity;
            }
            catch (Exception exception) when (exception is
                IOException or
                UnauthorizedAccessException or
                ArgumentException or
                NotSupportedException)
            {
                unavailablePaths.Add(logicalPath);
                continue;
            }

            if (!pathsByCanonicalIdentity.TryGetValue(
                    canonicalIdentity,
                    out List<string>? aliases))
            {
                aliases = [];
                pathsByCanonicalIdentity.Add(canonicalIdentity, aliases);
                canonicalIdentityOrder.Add(canonicalIdentity);
            }

            aliases.Add(logicalPath);
        }

        int totalReads = canonicalIdentityOrder.Count + unavailablePaths.Count;
        progress?.Report(new(0, totalReads));
        int completedReads = 0;
        foreach (string unavailablePath in unavailablePaths)
        {
            files.Add(unavailablePath, new FirmwareContentRead(null, null, IsStable: true));
            progress?.Report(new(++completedReads, totalReads));
        }

        foreach (string canonicalIdentity in canonicalIdentityOrder)
        {
            List<string> aliases = pathsByCanonicalIdentity[canonicalIdentity];
            FirmwareContentRead content = await ReadFirmwareFileAsync(
                aliases[0],
                aliases.Min(path => ResolveMaximumContentReadBytes(path, inputs)),
                cancellationToken).ConfigureAwait(false);
            foreach (string logicalPath in aliases)
            {
                files.Add(logicalPath, content);
            }

            progress?.Report(new(++completedReads, totalReads));
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

    private async ValueTask<FirmwareContentRead> ReadFirmwareFileAsync(
            string path,
            long maximumBytes,
            CancellationToken cancellationToken)
    {
        try
        {
            SelectedFileContentInspection inspection = await _contentInspector
                .InspectAsync(path, maximumBytes, cancellationToken)
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

    private static long ResolveMaximumContentReadBytes(
        string path,
        IEnumerable<FirmwareInspectionSnapshotInput> inputs)
    {
        long maximum = CompiledInputArtifactInspectionService.MaximumContentReadBytes;
        foreach (FirmwareInspectionSnapshotInput input in inputs.Where(input =>
                     StringComparer.Ordinal.Equals(path, input.Path)))
        {
            string? addressSpaceId = ResolveCompiledAddressSpaceId(input);
            if (input.ExactCapability is null || addressSpaceId is null)
            {
                continue;
            }

            maximum = Math.Min(
                maximum,
                CompiledInputArtifactInspectionService.ResolveMaximumContentReadBytes(
                    input.ExactCapability.CompiledComposition,
                    addressSpaceId));
        }

        return maximum;
    }

    private static string? ResolveCompiledAddressSpaceId(FirmwareInspectionSnapshotInput input)
    {
        string?[] candidates =
        [
            input.AbMergeAddressSpaceId,
            input.DpReplaceAddressSpaceId,
            input.StandardMergeAddressSpaceId,
            input.CtrlRamReplaceAddressSpaceId,
        ];
        string[] declared = [.. candidates.OfType<string>()];
        return declared.Length switch
        {
            0 => null,
            1 => declared[0],
            _ => throw new InvalidOperationException(
                "A firmware inspection input cannot declare multiple compiled address-space bindings."),
        };
    }

    private readonly record struct FirmwareContentRead(
        FileStamp? FileStamp,
        byte[]? Image,
        bool IsStable);
}
