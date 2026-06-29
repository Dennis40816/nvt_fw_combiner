namespace NvtFwCombiner.Application.Ports;

/// <summary>Commits successful build output bytes through an infrastructure adapter.</summary>
public interface ICompositionOutputWriter
{
    /// <summary>Writes output bytes atomically and returns the adapter-owned destination id.</summary>
    ValueTask<string> CommitAsync(
        string fileName,
        ReadOnlyMemory<byte> outputBytes,
        CancellationToken cancellationToken);
}
