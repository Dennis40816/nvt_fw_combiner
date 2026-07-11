namespace NvtFwCombiner.Profiles.FirmwareFamilies;

/// <summary>Reports one semantic failure in an already schema-validated firmware family document.</summary>
public sealed class FirmwareFamilyNormalizationException : Exception
{
    /// <summary>Creates a path-scoped semantic normalization failure.</summary>
    public FirmwareFamilyNormalizationException(string path, string message)
        : base($"{path}: {message}")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Path = path;
    }

    /// <summary>Creates a path-scoped failure while preserving the lower-level invariant exception.</summary>
    public FirmwareFamilyNormalizationException(string path, string message, Exception innerException)
        : base($"{path}: {message}", innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(innerException);
        Path = path;
    }

    /// <summary>JSON-style source path of the invalid semantic value.</summary>
    public string Path { get; }
}
