namespace NvtFwCombiner.Profiles.FirmwareFamilies;

internal sealed class FirmwareFamilyNormalizationException : Exception
{
    public FirmwareFamilyNormalizationException(string path, string message)
        : base($"{path}: {message}")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Path = path;
    }

    public FirmwareFamilyNormalizationException(string path, string message, Exception innerException)
        : base($"{path}: {message}", innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(innerException);
        Path = path;
    }

    public string Path { get; }
}
