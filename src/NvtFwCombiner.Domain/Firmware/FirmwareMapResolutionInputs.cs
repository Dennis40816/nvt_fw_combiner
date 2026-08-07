using System.Security.Cryptography;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Immutable identity computed from one snapshotted firmware artifact.</summary>
public sealed record FirmwareArtifactIdentity
{
    internal FirmwareArtifactIdentity(string artifactId, string sha256, long lengthBytes)
    {
        ArtifactId = RequiredValue.NotBlank(artifactId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lengthBytes);

        Sha256 = CanonicalSha256.Require(sha256, nameof(sha256));
        LengthBytes = lengthBytes;
    }

    /// <summary>Stable family-declared artifact binding identifier.</summary>
    public string ArtifactId { get; }

    /// <summary>Lowercase SHA-256 of the immutable artifact bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Exact immutable artifact length.</summary>
    public long LengthBytes { get; }

}

/// <summary>One immutable firmware artifact payload used by map resolution or declared metadata inspection.</summary>
public sealed class FirmwareArtifactPayload
{
    private readonly byte[] _bytes;

    /// <summary>Snapshots bytes and computes the sole artifact identity.</summary>
    public FirmwareArtifactPayload(string artifactId, ReadOnlySpan<byte> bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        if (bytes.IsEmpty)
        {
            throw new ArgumentException("Firmware artifact payloads cannot be empty.", nameof(bytes));
        }

        _bytes = bytes.ToArray();
        string sha256 = Convert.ToHexString(SHA256.HashData(_bytes)).ToLowerInvariant();
        Identity = new FirmwareArtifactIdentity(artifactId, sha256, _bytes.LongLength);
    }

    /// <summary>Identity derived from the private byte snapshot.</summary>
    public FirmwareArtifactIdentity Identity { get; }

    /// <summary>Stable family-declared artifact binding identifier.</summary>
    public string ArtifactId => Identity.ArtifactId;

    /// <summary>Lowercase SHA-256 of the private byte snapshot.</summary>
    public string Sha256 => Identity.Sha256;

    /// <summary>Exact private byte-snapshot length.</summary>
    public long LengthBytes => Identity.LengthBytes;

    internal ReadOnlySpan<byte> Bytes => _bytes;
}

/// <summary>Single public atomic run-input boundary for firmware-map resolution.</summary>
public sealed class FirmwareMapResolutionInputs
{
    private readonly FirmwareArtifactPayload[] _artifacts;

    /// <summary>Creates immutable resolution inputs from requested selections and zero or more artifact bytes.</summary>
    public FirmwareMapResolutionInputs(
        string memberId,
        string modeId,
        long capacityBytes,
        TopologySelection? requestedTopology,
        IEnumerable<FirmwareArtifactPayload> artifacts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modeId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacityBytes);
        if (requestedTopology is not null && requestedTopology.Source != TopologySelectionSource.Requested)
        {
            throw new ArgumentException(
                "Map-resolution inputs may contain only caller-requested topology.",
                nameof(requestedTopology));
        }

        _artifacts = Composition.ImmutableReferenceSnapshot.CreateUnique(
            artifacts,
            static artifact => artifact.ArtifactId,
            "Resolution artifacts cannot contain null.",
            "Resolution artifact ids must be ordinally unique.",
            StringComparer.Ordinal);

        Array.Sort(_artifacts, static (left, right) =>
            StringComparer.Ordinal.Compare(left.ArtifactId, right.ArtifactId));

        MemberId = memberId;
        ModeId = modeId;
        CapacityBytes = capacityBytes;
        RequestedTopology = requestedTopology;
        Artifacts = Array.AsReadOnly(_artifacts);
    }

    /// <summary>Selected IC member id.</summary>
    public string MemberId { get; }

    /// <summary>Selected firmware mode id.</summary>
    public string ModeId { get; }

    /// <summary>Selected exact image capacity.</summary>
    public long CapacityBytes { get; }

    /// <summary>Optional caller-authored topology selection.</summary>
    public TopologySelection? RequestedTopology { get; }

    /// <summary>Immutable artifact payloads in ordinal binding-id order; may be empty for metadata-independent candidates.</summary>
    public IReadOnlyList<FirmwareArtifactPayload> Artifacts { get; }
}
