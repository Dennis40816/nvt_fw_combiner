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
        ByteRange firmwareRange,
        string? stagedArtifactId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blockId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
        if (!Enum.IsDefined(sourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, "Unknown block source kind.");
        }

        if (sourceFileName.IndexOfAny(['/', '\\', ':']) >= 0 ||
            sourceFileName is "." or ".." ||
            Path.GetFileName(sourceFileName) != sourceFileName)
        {
            throw new ArgumentException("Source file name must be a plain file name.", nameof(sourceFileName));
        }

        if (sourceKind == LegacyCombinerBlockSourceKind.StagedArtifact && stagedArtifactId is null)
        {
            ExternalProcessorStagedArtifact.ValidateArtifactId(
                string.Empty,
                nameof(stagedArtifactId));
        }
        else if (stagedArtifactId is not null)
        {
            if (sourceKind is not (LegacyCombinerBlockSourceKind.StagedFile or LegacyCombinerBlockSourceKind.StagedArtifact))
            {
                throw new ArgumentException(
                    "Only staged-file and staged-artifact blocks can declare a staged artifact id.",
                    nameof(stagedArtifactId));
            }

            ExternalProcessorStagedArtifact.ValidateArtifactId(stagedArtifactId, nameof(stagedArtifactId));
        }

        BlockId = blockId;
        SourceKind = sourceKind;
        SourceFileName = sourceFileName;
        SourceOffset = sourceOffset;
        FirmwareRange = firmwareRange;
        StagedArtifactId = stagedArtifactId;
    }

    /// <summary>Stable block id used in diagnostics.</summary>
    public string BlockId { get; }

    /// <summary>Source kind selected by the postbuild command.</summary>
    public LegacyCombinerBlockSourceKind SourceKind { get; }

    /// <summary>Plain source file name under the staged BIN directory when this block does not read the firmware image.</summary>
    public string SourceFileName { get; }

    /// <summary>Required staged-artifact id, or an optional exact-file override id for a staged-file block.</summary>
    public string? StagedArtifactId { get; }

    /// <summary>Source offset passed to Combiner.exe.</summary>
    public long SourceOffset { get; }

    /// <summary>Destination range inside the staged firmware image.</summary>
    public ByteRange FirmwareRange { get; }
}
