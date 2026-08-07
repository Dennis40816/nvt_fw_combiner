namespace NvtFwCombiner.Domain.Composition;

/// <summary>Runtime bytes supplied to an external processor as source material for a firmware range.</summary>
public sealed class ExternalProcessorStagedSource
{
    private readonly byte[] _bytes;

    /// <summary>Creates staged source bytes for a firmware image range.</summary>
    public ExternalProcessorStagedSource(ByteRange firmwareRange, ReadOnlyMemory<byte> bytes)
        : this(firmwareRange, ClonePublicBytes(firmwareRange, bytes))
    {
    }

    private ExternalProcessorStagedSource(ByteRange firmwareRange, byte[] ownedBytes)
    {
        ArgumentNullException.ThrowIfNull(ownedBytes);
        DomainInvariant.Reject(
            ownedBytes.Length != firmwareRange.Length,
            "Staged source byte count must match the firmware range length.",
            nameof(ownedBytes));

        FirmwareRange = firmwareRange;
        _bytes = ownedBytes;
    }

    /// <summary>Firmware image range these source bytes correspond to.</summary>
    public ByteRange FirmwareRange { get; }

    /// <summary>Cloned staged source bytes.</summary>
    public ReadOnlyMemory<byte> Bytes => _bytes;

    internal static ExternalProcessorStagedSource FromOwnedBytes(ByteRange firmwareRange, byte[] ownedBytes)
    {
        return new ExternalProcessorStagedSource(firmwareRange, ownedBytes);
    }

    private static byte[] ClonePublicBytes(ByteRange firmwareRange, ReadOnlyMemory<byte> bytes)
    {
        return bytes.Length != firmwareRange.Length
            ? throw new ArgumentException("Staged source byte count must match the firmware range length.", nameof(bytes))
            : bytes.ToArray();
    }
}
