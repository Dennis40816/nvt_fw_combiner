using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// Revalidates the concrete output filename immediately before an infrastructure writer commits it.
/// </summary>
/// <remarks>
/// Automatic output naming is resolved after input admission. The earlier Bootstrap preflight can
/// therefore only protect the profile's template filename; this adapter protects the rendered name.
/// </remarks>
internal sealed class ProtectedCompositionOutputWriter : ICompositionOutputWriter
{
    private readonly ICompositionOutputWriter _inner;
    private readonly string _outputDirectory;
    private readonly IReadOnlyList<ProtectedPathGuard.ProtectedPath> _protectedPaths;

    internal ProtectedCompositionOutputWriter(
        ICompositionOutputWriter inner,
        string outputDirectory,
        IEnumerable<ProtectedPathGuard.ProtectedPath> protectedPaths)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(protectedPaths);

        _inner = inner;
        _outputDirectory = Path.GetFullPath(outputDirectory);
        _protectedPaths = [.. protectedPaths];
    }

    /// <inheritdoc />
    public ValueTask<string> CommitAsync(
        string fileName,
        ReadOnlyMemory<byte> outputBytes,
        CancellationToken cancellationToken)
    {
        ProtectedPathGuard.EnsureDoesNotAlias(
            ProtectedPathGuard.CombineFullPath(_outputDirectory, fileName),
            "Output path",
            _protectedPaths,
            nameof(fileName));
        return _inner.CommitAsync(fileName, outputBytes, cancellationToken);
    }
}
