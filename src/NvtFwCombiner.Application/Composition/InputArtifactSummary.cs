using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Report-safe summary of one bound input artifact.</summary>
public sealed class InputArtifactSummary
{
    /// <summary>Creates an input summary without a host path.</summary>
    public InputArtifactSummary(
        string addressSpaceId,
        string artifactId,
        long size,
        string sha256,
        string? originalFileName = null,
        InputArtifactExecutionSnapshotSummary? executionSnapshot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        if (originalFileName is not null &&
            (string.IsNullOrWhiteSpace(originalFileName) ||
             originalFileName.IndexOfAny(['/', '\\', ':']) >= 0 ||
             originalFileName is "." or ".." ||
             originalFileName.Any(char.IsControl) ||
             Path.GetFileName(originalFileName) != originalFileName))
        {
            throw new ArgumentException(
                "Original file name must be a plain filename without path or control syntax.",
                nameof(originalFileName));
        }

        if (executionSnapshot is not null)
        {
            long representedSourceEnd = executionSnapshot.IgnoredTrailingRange?.EndExclusive ??
                executionSnapshot.AcceptedRange.EndExclusive;
            if (representedSourceEnd != size)
            {
                throw new ArgumentException(
                    "The execution snapshot ranges must cover the represented source size.",
                    nameof(executionSnapshot));
            }
        }

        AddressSpaceId = addressSpaceId;
        ArtifactId = artifactId;
        Size = size;
        Sha256 = sha256;
        OriginalFileName = originalFileName;
        ExecutionSnapshot = executionSnapshot;
    }

    /// <summary>Address space populated by this artifact.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Application-local artifact id, not a portable file path.</summary>
    public string ArtifactId { get; }

    /// <summary>Input size in bytes.</summary>
    public long Size { get; }

    /// <summary>Lowercase SHA-256 hash of the input bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Original plain filename retained when the request provides V2 input provenance.</summary>
    public string? OriginalFileName { get; }

    /// <summary>Accepted execution prefix and ignored outer tail when the compiled contract declares one.</summary>
    public InputArtifactExecutionSnapshotSummary? ExecutionSnapshot { get; }
}

/// <summary>Report-safe identity of the immutable prefix accepted by execution.</summary>
public sealed class InputArtifactExecutionSnapshotSummary
{
    /// <summary>Creates a snapshot summary from a half-open accepted range and optional ignored tail.</summary>
    public InputArtifactExecutionSnapshotSummary(
        ByteRange acceptedRange,
        string acceptedSha256,
        ByteRange? ignoredTrailingRange)
    {
        if (acceptedRange.Start != 0)
        {
            throw new ArgumentException("The accepted execution snapshot must start at zero.", nameof(acceptedRange));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(acceptedSha256);
        if (acceptedSha256.Length != 64 || acceptedSha256.Any(static character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "Accepted snapshot SHA-256 must be 64 lowercase hexadecimal characters.",
                nameof(acceptedSha256));
        }

        if (ignoredTrailingRange is { } ignored && ignored.Start != acceptedRange.EndExclusive)
        {
            throw new ArgumentException(
                "The ignored trailing range must begin at the accepted range end.",
                nameof(ignoredTrailingRange));
        }

        AcceptedRange = acceptedRange;
        AcceptedSha256 = acceptedSha256;
        IgnoredTrailingRange = ignoredTrailingRange;
    }

    /// <summary>Half-open range accepted from the selected source.</summary>
    public ByteRange AcceptedRange { get; }

    /// <summary>Accepted prefix size in bytes.</summary>
    public long AcceptedSize => AcceptedRange.Length;

    /// <summary>Lowercase SHA-256 of the accepted prefix.</summary>
    public string AcceptedSha256 { get; }

    /// <summary>Half-open outer source tail ignored by execution, when present.</summary>
    public ByteRange? IgnoredTrailingRange { get; }

    /// <summary>Number of selected source bytes ignored after the accepted prefix.</summary>
    public long IgnoredTrailingBytes => IgnoredTrailingRange?.Length ?? 0;
}
