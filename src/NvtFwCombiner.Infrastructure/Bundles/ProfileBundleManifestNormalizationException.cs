namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Reports one schema-postcondition or manifest semantic failure with source path.</summary>
internal sealed class ProfileBundleManifestNormalizationException : Exception
{
    internal ProfileBundleManifestNormalizationException(
        string path,
        string message,
        Exception? innerException = null)
        : base($"{path}: {message}", innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path;
    }

    internal string Path { get; }
}
