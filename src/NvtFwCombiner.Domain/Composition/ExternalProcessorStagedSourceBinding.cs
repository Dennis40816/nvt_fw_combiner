namespace NvtFwCombiner.Domain.Composition;

/// <summary>Maps an address-space slice into bytes an external processor may stage as source material.</summary>
public sealed class ExternalProcessorStagedSourceBinding
{
    /// <summary>Creates a staged-source binding from a declared source range to the matching firmware image range.</summary>
    public ExternalProcessorStagedSourceBinding(
        string sourceSpaceId,
        ByteRange sourceRange,
        ByteRange firmwareRange)
    {
        SourceSpaceId = RequiredValue.NotBlank(sourceSpaceId);
        if (sourceRange.Length != firmwareRange.Length)
        {
            throw new ArgumentException("Staged source and firmware ranges must have the same length.", nameof(sourceRange));
        }

        SourceRange = sourceRange;
        FirmwareRange = firmwareRange;
    }

    /// <summary>Address space that provides the staged source bytes.</summary>
    public string SourceSpaceId { get; }

    /// <summary>Range read from <see cref="SourceSpaceId"/>.</summary>
    public ByteRange SourceRange { get; }

    /// <summary>Firmware image range these source bytes correspond to when the processor stages files.</summary>
    public ByteRange FirmwareRange { get; }
}
