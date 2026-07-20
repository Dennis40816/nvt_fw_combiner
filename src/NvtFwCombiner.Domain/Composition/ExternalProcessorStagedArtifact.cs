namespace NvtFwCombiner.Domain.Composition;

/// <summary>Engine-created immutable bytes materialized as one named external-processor staging artifact.</summary>
public sealed class ExternalProcessorStagedArtifact
{
    private readonly byte[] _bytes;

    /// <summary>Creates one named immutable staging artifact.</summary>
    public ExternalProcessorStagedArtifact(string artifactId, ReadOnlyMemory<byte> bytes)
        : this(artifactId, ClonePublicBytes(artifactId, bytes))
    {
    }

    private ExternalProcessorStagedArtifact(string artifactId, byte[] ownedBytes)
    {
        ValidateArtifactId(artifactId, nameof(artifactId));
        ArgumentNullException.ThrowIfNull(ownedBytes);
        if (ownedBytes.Length == 0)
        {
            throw new ArgumentException("Staged artifact bytes must not be empty.", nameof(ownedBytes));
        }

        ArtifactId = artifactId;
        _bytes = ownedBytes;
    }

    /// <summary>Closed identifier referenced by a manifest staging-artifact token.</summary>
    public string ArtifactId { get; }

    /// <summary>Cloned bytes written only into the host-created staging directory.</summary>
    public ReadOnlyMemory<byte> Bytes => _bytes;

    internal static ExternalProcessorStagedArtifact FromOwnedBytes(string artifactId, byte[] ownedBytes)
    {
        return new ExternalProcessorStagedArtifact(artifactId, ownedBytes);
    }

    private static byte[] ClonePublicBytes(string artifactId, ReadOnlyMemory<byte> bytes)
    {
        ValidateArtifactId(artifactId, nameof(artifactId));
        return bytes.IsEmpty ? throw new ArgumentException("Staged artifact bytes must not be empty.", nameof(bytes)) : bytes.ToArray();
    }

    /// <summary>Rejects a value that cannot safely identify a host-created staging artifact.</summary>
    public static void ValidateArtifactId(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!IsValidArtifactId(value))
        {
            throw new ArgumentException(
                "Staged artifact id must be lowercase hyphen-separated ASCII words beginning with a letter.",
                parameterName);
        }
    }

    /// <summary>Returns whether a value is a closed staging-artifact identifier.</summary>
    public static bool IsValidArtifactId(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            IsAsciiLower(value[0]) &&
            value.All(character => IsAsciiLower(character) || char.IsAsciiDigit(character) || character == '-') &&
            value[^1] != '-' &&
            !value.Contains("--", StringComparison.Ordinal);
    }

    private static bool IsAsciiLower(char value)
    {
        return value is >= 'a' and <= 'z';
    }
}
