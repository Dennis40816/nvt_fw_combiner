using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// Revalidates the concrete output filename immediately before an infrastructure writer commits it.
/// </summary>
/// <remarks>
/// Automatic output naming is resolved after input admission. The earlier Bootstrap preflight can
/// therefore only protect the profile's template filename; this adapter protects the rendered name.
/// </remarks>
internal sealed class ProtectedCompositionOutputWriter : ICompositionOutputWriter, ICompositionOutputCommitPreflight
{
    private readonly ICompositionOutputWriter _inner;
    private readonly string _outputDirectory;
    private readonly IReadOnlyList<ProtectedPathGuard.ProtectedPath> _protectedPaths;
    private readonly Action<string, OutputNamingSummary?>? _additionalPreflight;

    internal ProtectedCompositionOutputWriter(
        ICompositionOutputWriter inner,
        string outputDirectory,
        IEnumerable<ProtectedPathGuard.ProtectedPath> protectedPaths,
        Action<string, OutputNamingSummary?>? additionalPreflight = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(protectedPaths);

        _inner = inner;
        _outputDirectory = Path.GetFullPath(outputDirectory);
        _protectedPaths = [.. protectedPaths];
        _additionalPreflight = additionalPreflight;
    }

    /// <inheritdoc />
    public void EnsureCanCommit(string fileName, OutputNamingSummary? outputNaming)
    {
        string outputPath = EnsurePrimaryOutputPath(fileName);
        _additionalPreflight?.Invoke(outputPath, outputNaming);
    }

    private string EnsurePrimaryOutputPath(string fileName)
    {
        string outputPath = ProtectedPathGuard.CombineFullPath(_outputDirectory, fileName);
        ProtectedPathGuard.EnsureDoesNotAlias(
            outputPath,
            "Output path",
            _protectedPaths,
            nameof(fileName));
        return outputPath;
    }

    /// <inheritdoc />
    public ValueTask<string> CommitAsync(
        string fileName,
        ReadOnlyMemory<byte> outputBytes,
        CancellationToken cancellationToken)
    {
        _ = EnsurePrimaryOutputPath(fileName);
        return _inner.CommitAsync(fileName, outputBytes, cancellationToken);
    }
}
