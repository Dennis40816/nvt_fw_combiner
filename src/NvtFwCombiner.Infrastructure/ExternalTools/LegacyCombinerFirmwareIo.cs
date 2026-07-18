namespace NvtFwCombiner.Infrastructure.ExternalTools;

internal interface ILegacyCombinerFirmwareIo
{
    long GetLength(string path);

    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken);

    Task<byte[]> ReadTailAsync(
        string path,
        long start,
        long length,
        CancellationToken cancellationToken);

    Task AppendTailAsync(
        string path,
        long expectedCurrentLength,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken);
}

internal sealed class PhysicalLegacyCombinerFirmwareIo : ILegacyCombinerFirmwareIo
{
    internal static PhysicalLegacyCombinerFirmwareIo Instance { get; } = new();

    private PhysicalLegacyCombinerFirmwareIo()
    {
    }

    public long GetLength(string path)
    {
        return new FileInfo(path).Length;
    }

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
    {
        return File.ReadAllBytesAsync(path, cancellationToken);
    }

    public async Task<byte[]> ReadTailAsync(
        string path,
        long start,
        long length,
        CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[checked((int)length)];
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        stream.Position = start;
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    public async Task AppendTailAsync(
        string path,
        long expectedCurrentLength,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);
        if (stream.Length != expectedCurrentLength)
        {
            throw new IOException("Staged firmware length changed before shortened-output normalization.");
        }

        stream.Position = expectedCurrentLength;
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }
}
