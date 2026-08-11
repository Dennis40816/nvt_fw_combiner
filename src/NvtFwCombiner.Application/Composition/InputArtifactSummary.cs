using System.Text.Json.Serialization;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Report-safe summary of one bound input artifact.</summary>
public sealed class InputArtifactSummary(
    string addressSpaceId,
    string artifactId,
    long size,
    string sha256,
    string? originalFileName = null,
    InputArtifactExecutionSnapshotSummary? executionSnapshot = null)
{
    /// <summary>Address space populated by this artifact.</summary>
    public string AddressSpaceId { get; } = CompositionSummaryValue.NotBlank(
        addressSpaceId,
        nameof(addressSpaceId));

    /// <summary>Application-local artifact id, not a portable file path.</summary>
    public string ArtifactId { get; } = CompositionSummaryValue.NotBlank(artifactId, nameof(artifactId));

    /// <summary>Input size in bytes.</summary>
    public long Size { get; } = CompositionSummaryValue.NonNegative(size, nameof(size));

    /// <summary>Lowercase SHA-256 hash of the input bytes.</summary>
    public string Sha256 { get; } = CompositionSummaryValue.NotBlank(sha256, nameof(sha256));

    /// <summary>Original plain filename retained when the request provides V2 input provenance.</summary>
    public string? OriginalFileName { get; } = RequirePlainFileName(originalFileName);

    /// <summary>Accepted execution prefix and ignored outer tail when the compiled contract declares one.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InputArtifactExecutionSnapshotSummary? ExecutionSnapshot { get; } = RequireExecutionSnapshot(
        executionSnapshot,
        size);

    private static string? RequirePlainFileName(string? originalFileName)
    {
        return originalFileName is null ||
            (!string.IsNullOrWhiteSpace(originalFileName) &&
             originalFileName.IndexOfAny(['/', '\\', ':']) < 0 &&
             originalFileName is not ("." or "..") &&
             !originalFileName.Any(char.IsControl) &&
             Path.GetFileName(originalFileName) == originalFileName)
                ? originalFileName
                : throw new ArgumentException(
                    "Original file name must be a plain filename without path or control syntax.",
                    nameof(originalFileName));
    }

    private static InputArtifactExecutionSnapshotSummary? RequireExecutionSnapshot(
        InputArtifactExecutionSnapshotSummary? executionSnapshot,
        long size)
    {
        long? representedSourceEnd = executionSnapshot?.IgnoredTrailingRange?.EndExclusive ??
            executionSnapshot?.AcceptedRange.EndExclusive;
        return representedSourceEnd is null || representedSourceEnd == size
            ? executionSnapshot
            : throw new ArgumentException(
                "The execution snapshot ranges must cover the represented source size.",
                nameof(executionSnapshot));
    }
}

/// <summary>Report-safe identity of the immutable prefix accepted by execution.</summary>
public sealed class InputArtifactExecutionSnapshotSummary(
    ByteRange acceptedRange,
    string acceptedSha256,
    ByteRange? ignoredTrailingRange)
{
    /// <summary>Half-open range accepted from the selected source.</summary>
    public ByteRange AcceptedRange { get; } = acceptedRange.Start == 0
        ? acceptedRange
        : throw new ArgumentException(
            "The accepted execution snapshot must start at zero.",
            nameof(acceptedRange));

    /// <summary>Accepted prefix size in bytes.</summary>
    public long AcceptedSize => AcceptedRange.Length;

    /// <summary>Lowercase SHA-256 of the accepted prefix.</summary>
    public string AcceptedSha256 { get; } = RequireAcceptedSha256(acceptedSha256);

    /// <summary>Half-open outer source tail ignored by execution, when present.</summary>
    public ByteRange? IgnoredTrailingRange { get; } = ignoredTrailingRange is null ||
        ignoredTrailingRange.Value.Start == acceptedRange.EndExclusive
            ? ignoredTrailingRange
            : throw new ArgumentException(
                "The ignored trailing range must begin at the accepted range end.",
                nameof(ignoredTrailingRange));

    /// <summary>Number of selected source bytes ignored after the accepted prefix.</summary>
    public long IgnoredTrailingBytes => IgnoredTrailingRange?.Length ?? 0;

    private static string RequireAcceptedSha256(string acceptedSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptedSha256);
        return acceptedSha256.Length == 64 && acceptedSha256.All(static character =>
                character is (>= '0' and <= '9') or (>= 'a' and <= 'f'))
            ? acceptedSha256
            : throw new ArgumentException(
                "Accepted snapshot SHA-256 must be 64 lowercase hexadecimal characters.",
                nameof(acceptedSha256));
    }
}
