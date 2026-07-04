namespace NvtFwCombiner.Domain.Composition;

/// <summary>Runtime bytes supplied to an external processor as source material for a firmware range.</summary>
public sealed class ExternalProcessorStagedSource
{
    private readonly byte[] _bytes;

    /// <summary>Creates staged source bytes for a firmware image range.</summary>
    public ExternalProcessorStagedSource(ByteRange firmwareRange, ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length != firmwareRange.Length)
        {
            throw new ArgumentException("Staged source byte count must match the firmware range length.", nameof(bytes));
        }

        FirmwareRange = firmwareRange;
        _bytes = bytes.ToArray();
    }

    /// <summary>Firmware image range these source bytes correspond to.</summary>
    public ByteRange FirmwareRange { get; }

    /// <summary>Cloned staged source bytes.</summary>
    public ReadOnlyMemory<byte> Bytes => _bytes;
}
