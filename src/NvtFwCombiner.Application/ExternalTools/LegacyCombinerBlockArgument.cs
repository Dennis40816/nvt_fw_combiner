using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>One block argument passed to Combiner.exe.</summary>
public sealed class LegacyCombinerBlockArgument
{
    /// <summary>Creates a block argument from postbuild evidence.</summary>
    public LegacyCombinerBlockArgument(
        string blockId,
        LegacyCombinerBlockSourceKind sourceKind,
        string sourceFileName,
        long sourceOffset,
        ByteRange firmwareRange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blockId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
        if (sourceFileName.IndexOfAny(['/', '\\', ':']) >= 0 ||
            sourceFileName is "." or ".." ||
            Path.GetFileName(sourceFileName) != sourceFileName)
        {
            throw new ArgumentException("Source file name must be a plain file name.", nameof(sourceFileName));
        }

        BlockId = blockId;
        SourceKind = sourceKind;
        SourceFileName = sourceFileName;
        SourceOffset = sourceOffset;
        FirmwareRange = firmwareRange;
    }

    /// <summary>Stable block id used in diagnostics.</summary>
    public string BlockId { get; }

    /// <summary>Source kind selected by the postbuild command.</summary>
    public LegacyCombinerBlockSourceKind SourceKind { get; }

    /// <summary>Plain source file name when <see cref="SourceKind" /> is <see cref="LegacyCombinerBlockSourceKind.StagedFile" />.</summary>
    public string SourceFileName { get; }

    /// <summary>Source offset passed to Combiner.exe.</summary>
    public long SourceOffset { get; }

    /// <summary>Destination range inside the staged firmware image.</summary>
    public ByteRange FirmwareRange { get; }
}
