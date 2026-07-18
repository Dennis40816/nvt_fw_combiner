using System.Security.Cryptography;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// One immutable, inspection-only snapshot of a selected firmware artifact.
/// It is never accepted as Build input authority.
/// </summary>
public sealed class WorkbenchFirmwareArtifactSnapshot
{
    private const int HeaderProbeLength = 256 * 1024;
    private readonly ReadOnlyMemory<byte> _bytes;

    internal WorkbenchFirmwareArtifactSnapshot(string artifactPath, ReadOnlyMemory<byte> bytes)
    {
        ArtifactPath = artifactPath;
        _bytes = bytes;
        Sha256 = Convert.ToHexString(SHA256.HashData(bytes.Span)).ToLowerInvariant();
    }

    /// <summary>Gets the selected artifact path captured by this UI-only snapshot.</summary>
    public string ArtifactPath { get; }

    /// <summary>Gets the exact captured byte length.</summary>
    public int Length => _bytes.Length;

    /// <summary>Gets the SHA-256 identity of the captured bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Gets a bounded read-only header view for non-authoritative UI marker detection.</summary>
    public ReadOnlyMemory<byte> GetHeaderProbe()
    {
        return _bytes[..Math.Min(_bytes.Length, HeaderProbeLength)];
    }

    internal ReadOnlySpan<byte> Bytes => _bytes.Span;
}

/// <summary>Typed firmware facts projected from one immutable artifact snapshot for one IC context.</summary>
public sealed record WorkbenchFirmwareInspection(
    string IcId,
    WorkbenchFirmwareConfigMetadata? FirmwareConfig,
    WorkbenchFirmwareContextSuggestion? ContextSuggestion,
    WorkbenchDpVersionMetadata? DpVersion,
    WorkbenchCmiDpCodeMetadata? CmiDpCode);

/// <summary>One already-inspected firmware candidate used by FlashCode output naming.</summary>
public sealed record WorkbenchInspectedOutputNameCandidate(
    WorkbenchOutputNameCandidateKind Kind,
    WorkbenchFirmwareInspection? Inspection);

public static partial class WorkbenchCompositionService
{
    /// <summary>
    /// Captures one selected file asynchronously for UI inspection. Build deliberately performs its
    /// own authoritative artifact read and hash through the composition use case.
    /// </summary>
    public static async Task<WorkbenchFirmwareArtifactSnapshot?> TryCaptureFirmwareArtifactAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return null;
            }

            var reader = new FileArtifactReader([directory]);
            return await TryCaptureFirmwareArtifactAsync(reader, fullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Captures one selected file synchronously for compatibility callers and deterministic tests.</summary>
    public static WorkbenchFirmwareArtifactSnapshot? TryCaptureFirmwareArtifact(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            return new WorkbenchFirmwareArtifactSnapshot(Path.GetFullPath(path), bytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    internal static async Task<WorkbenchFirmwareArtifactSnapshot?> TryCaptureFirmwareArtifactAsync(
        IArtifactReader reader,
        string artifactId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);

        try
        {
            ReadOnlyMemory<byte> bytes = await reader.ReadAsync(artifactId, cancellationToken).ConfigureAwait(false);
            return new WorkbenchFirmwareArtifactSnapshot(artifactId, bytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          ArgumentException or NotSupportedException or KeyNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Projects every firmware display fact needed by the shell from captured bytes.</summary>
    public static WorkbenchFirmwareInspection InspectFirmwareArtifact(
        string icId,
        WorkbenchFirmwareArtifactSnapshot snapshot,
        WorkbenchFirmwareArtifactSnapshot? tpSnapshot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(snapshot);

        string normalizedIc = IcSupportCatalog.NormalizeIcId(icId);

        bool hasFirmwareConfig = TryReadFirmwareConfigBackupMetadata(
            normalizedIc,
            snapshot.Bytes,
            out FirmwareConfigMetadata firmwareConfigMetadata);
        WorkbenchFirmwareConfigMetadata? firmwareConfig = hasFirmwareConfig
            ? TryCreateFirmwareConfigMetadata(normalizedIc, firmwareConfigMetadata)
            : null;
        WorkbenchFirmwareContextSuggestion? contextSuggestion = hasFirmwareConfig
            ? TryCreateFirmwareContextSuggestion(normalizedIc, firmwareConfigMetadata)
            : null;
        WorkbenchDpVersionMetadata? dpVersion = TryReadDpVersionMetadata(normalizedIc, snapshot.Bytes);
        byte? chipNumber = ReferenceEquals(tpSnapshot, snapshot) && hasFirmwareConfig
            ? firmwareConfigMetadata.ChipNumber
            : tpSnapshot is null
                ? null
                : TryReadFirmwareConfigChipNumber(normalizedIc, tpSnapshot.Bytes);
        WorkbenchCmiDpCodeMetadata? cmiDpCode = TryReadCmiDpCodeMetadata(
            normalizedIc,
            snapshot.Bytes,
            chipNumber);

        return new WorkbenchFirmwareInspection(
            normalizedIc,
            firmwareConfig,
            contextSuggestion,
            dpVersion,
            cmiDpCode);
    }

    private static byte? TryReadFirmwareConfigChipNumber(string icId, ReadOnlySpan<byte> image)
    {
        return !image.IsEmpty &&
            TryReadFirmwareConfigBackupMetadata(icId, image, out FirmwareConfigMetadata metadata)
                ? metadata.ChipNumber
                : null;
    }
}
