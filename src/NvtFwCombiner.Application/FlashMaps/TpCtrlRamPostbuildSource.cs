using NvtFwCombiner.Application.ExternalTools;

namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>A physical CtrlRAM BIN consumed one or more times by an approved Postbuild plan.</summary>
public sealed record TpCtrlRamPostbuildSource(
    string SourceId,
    string SourceFileName,
    string StagedArtifactId,
    long RequiredLength,
    IReadOnlyList<LegacyCombinerBlockArgument> Blocks,
    IReadOnlyList<TpFlashMapRegion> Regions);
