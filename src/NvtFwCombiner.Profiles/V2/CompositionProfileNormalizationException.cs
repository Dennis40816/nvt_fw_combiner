namespace NvtFwCombiner.Profiles.V2;

/// <summary>Reports one semantic failure in a schema-validated composition profile document.</summary>
internal sealed class CompositionProfileNormalizationException : Exception
{
    internal CompositionProfileNormalizationException(string path, string message)
        : base($"{path}: {message}")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Path = path;
    }

    internal CompositionProfileNormalizationException(string path, string message, Exception innerException)
        : base($"{path}: {message}", innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(innerException);
        Path = path;
    }

    internal string Path { get; }
}
