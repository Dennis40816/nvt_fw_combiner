using System.Security.Cryptography;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Metadata;

/// <summary>One immutable full artifact supplied to the identity-bound BIN inspection factory.</summary>
public sealed class FirmwareBinInspectionArtifact
{
    private readonly byte[] _bytes;

    /// <summary>Snapshots one artifact and computes its content identity.</summary>
    public FirmwareBinInspectionArtifact(string artifactId, ReadOnlyMemory<byte> bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        if (bytes.IsEmpty)
        {
            throw new ArgumentException("BIN inspection artifacts cannot be empty.", nameof(bytes));
        }

        ArtifactId = artifactId;
        _bytes = bytes.ToArray();
        Sha256 = Hash(_bytes);
    }

    /// <summary>Stable metadata-plan artifact binding id.</summary>
    public string ArtifactId { get; }

    /// <summary>SHA-256 of the complete snapshotted artifact.</summary>
    public string Sha256 { get; }

    /// <summary>Exact snapshotted artifact length.</summary>
    public long LengthBytes => _bytes.LongLength;

    internal ReadOnlySpan<byte> Bytes => _bytes;

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}

/// <summary>One Application-formatted resolved structure with its exact immutable byte slice.</summary>
public sealed class FirmwareBinInspectionStructure
{
    private readonly byte[] _bytes;

    internal FirmwareBinInspectionStructure(
        FormattedMetadataStructure metadata,
        ReadOnlySpan<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Metadata = metadata;
        _bytes = bytes.ToArray();
    }

    /// <summary>Application-owned names, values, identity, and exact resolved geometry.</summary>
    public FormattedMetadataStructure Metadata { get; }

    /// <summary>Exact private copy of the resolved structure bytes.</summary>
    public ReadOnlyMemory<byte> Bytes => _bytes;
}

/// <summary>
/// One formatter-rooted BIN inspection snapshot that preserves publication identity and verifies
/// every displayed byte against the artifacts evaluated by the metadata inspection.
/// </summary>
public sealed class FirmwareBinInspectionSnapshot
{
    private FirmwareBinInspectionSnapshot(
        ResolutionToken resolutionToken,
        long authoringRevision,
        IEnumerable<FirmwareBinInspectionStructure> structures)
    {
        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
        Structures = Array.AsReadOnly([.. structures]);
    }

    /// <summary>Capability publication token evaluated by the source inspection.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Authoring revision evaluated by the source inspection.</summary>
    public long AuthoringRevision { get; }

    /// <summary>Ready resolved structures in canonical metadata-plan order.</summary>
    public IReadOnlyList<FirmwareBinInspectionStructure> Structures { get; }

    /// <summary>Formats one exact inspection and slices only hash-matched evaluated artifacts.</summary>
    public static FirmwareBinInspectionSnapshot Create(
        MetadataInspectionSnapshot inspection,
        IEnumerable<FirmwareBinInspectionArtifact> artifacts)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        ArgumentNullException.ThrowIfNull(artifacts);
        FirmwareBinInspectionArtifact[] artifactSnapshot = [.. artifacts];
        if (artifactSnapshot.Any(static artifact => artifact is null) ||
            artifactSnapshot.Select(static artifact => artifact.ArtifactId)
                .Distinct(StringComparer.Ordinal).Count() != artifactSnapshot.Length)
        {
            throw new ArgumentException(
                "BIN inspection artifacts must be non-null and uniquely bound.",
                nameof(artifacts));
        }

        Dictionary<string, FirmwareBinInspectionArtifact> artifactsById = artifactSnapshot.ToDictionary(
            static artifact => artifact.ArtifactId,
            StringComparer.Ordinal);
        if (inspection.ArtifactIdentities.Count != artifactSnapshot.Length ||
            inspection.ArtifactIdentities.Any(identity =>
                !artifactsById.TryGetValue(identity.ArtifactId, out FirmwareBinInspectionArtifact? artifact) ||
                !Matches(identity, artifact)))
        {
            throw new ArgumentException(
                "BIN inspection artifacts do not match the exact metadata inspection identity set.",
                nameof(artifacts));
        }

        FormattedMetadataInspectionSnapshot formatted = FirmwareMetadataInspectionFormatter.Format(inspection);
        FirmwareBinInspectionStructure[] structures =
        [
            .. formatted.Structures
                .Where(static metadata =>
                    metadata.State == MetadataInspectionState.Value &&
                    metadata.Readiness == ResolvedChildReadiness.Ready)
                .Select(metadata => CreateStructure(metadata, artifactsById)),
        ];
        return structures.Length > 0
            ? new FirmwareBinInspectionSnapshot(
                formatted.ResolutionToken,
                formatted.AuthoringRevision,
                structures)
            : throw new ArgumentException(
                "BIN inspection requires at least one ready resolved metadata structure.",
                nameof(inspection));
    }

    private static FirmwareBinInspectionStructure CreateStructure(
        FormattedMetadataStructure metadata,
        Dictionary<string, FirmwareBinInspectionArtifact> artifacts)
    {
        if (metadata.ArtifactIdentity is not { } identity ||
            metadata.AddressedRange is not { } addressedRange ||
            !artifacts.TryGetValue(metadata.ArtifactBindingId, out FirmwareBinInspectionArtifact? artifact) ||
            !Matches(identity, artifact) ||
            addressedRange.Range.EndExclusive > artifact.LengthBytes)
        {
            throw new ArgumentException(
                $"Resolved metadata structure '{metadata.BindingId}' is not bound to exact inspected bytes.",
                nameof(metadata));
        }

        int start = checked((int)addressedRange.Range.Start);
        int length = checked((int)addressedRange.Range.Length);
        return new FirmwareBinInspectionStructure(
            metadata,
            artifact.Bytes.Slice(start, length));
    }

    private static bool Matches(
        FirmwareArtifactIdentity identity,
        FirmwareBinInspectionArtifact artifact)
    {
        return StringComparer.Ordinal.Equals(identity.ArtifactId, artifact.ArtifactId) &&
            StringComparer.Ordinal.Equals(identity.Sha256, artifact.Sha256) &&
            identity.LengthBytes == artifact.LengthBytes;
    }
}
