namespace NvtFwCombiner.Domain.Composition;

/// <summary>Maps an address-space slice into bytes an external processor may stage as source material.</summary>
public sealed class ExternalProcessorStagedSourceBinding(
    string sourceSpaceId,
    ByteRange sourceRange,
    ByteRange firmwareRange)
{
    /// <summary>Address space that provides the staged source bytes.</summary>
    public string SourceSpaceId { get; } = RequiredValue.NotBlank(sourceSpaceId);

    /// <summary>Range read from <see cref="SourceSpaceId"/>.</summary>
    public ByteRange SourceRange { get; } = RequireEqualLength(sourceRange, firmwareRange);

    /// <summary>Firmware image range these source bytes correspond to when the processor stages files.</summary>
    public ByteRange FirmwareRange { get; } = firmwareRange;

    private static ByteRange RequireEqualLength(ByteRange sourceRange, ByteRange firmwareRange)
    {
        return sourceRange.Length == firmwareRange.Length
            ? sourceRange
            : throw new ArgumentException(
                "Staged source and firmware ranges must have the same length.",
                nameof(sourceRange));
    }
}
