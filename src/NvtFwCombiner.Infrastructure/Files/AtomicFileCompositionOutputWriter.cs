using NvtFwCombiner.Application.Ports;
using System.Security.Cryptography;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>Commits successful composition output atomically under a configured output directory.</summary>
public sealed class AtomicFileCompositionOutputWriter : ICompositionOutputWriter
{
    private readonly bool _overwrite;
    private readonly string _outputRoot;

    /// <summary>Creates an atomic output writer constrained to one output directory.</summary>
    public AtomicFileCompositionOutputWriter(string outputDirectory, bool overwrite = false)
    {
        _outputRoot = FileSystemPathGuard.ResolveRoot(outputDirectory);
        _overwrite = overwrite;
    }

    /// <inheritdoc />
    public async ValueTask<CompositionOutputCommitReceipt> CommitAsync(
        string fileName,
        ReadOnlyMemory<byte> outputBytes,
        CancellationToken cancellationToken)
    {
        string destinationPath = FileSystemPathGuard.ResolveFileNameUnderRoot(fileName, _outputRoot);
        string tempPath = Path.Combine(
            _outputRoot,
            $".{fileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 131_072,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await stream.WriteAsync(outputBytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, destinationPath, _overwrite);
            return new CompositionOutputCommitReceipt(
                destinationPath,
                fileName,
                outputBytes.Length,
                Convert.ToHexStringLower(SHA256.HashData(outputBytes.Span)));
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }
}
