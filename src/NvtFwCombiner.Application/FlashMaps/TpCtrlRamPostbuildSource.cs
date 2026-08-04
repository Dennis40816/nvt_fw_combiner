using NvtFwCombiner.Application.ExternalTools;

namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>Profile-resolved semantic role of one staged postbuild artifact.</summary>
public enum TpCtrlRamPostbuildArtifactRole
{
    /// <summary>A regular physical CtrlRAM source.</summary>
    CtrlRam,

    /// <summary>The cascade DiffDLM artifact governed by the reviewed DiffDLM policy.</summary>
    DiffDlm,
}

/// <summary>A physical CtrlRAM BIN consumed one or more times by an approved Postbuild plan.</summary>
public sealed record TpCtrlRamPostbuildSource(
    string SourceId,
    string SourceFileName,
    string StagedArtifactId,
    long RequiredLength,
    IReadOnlyList<LegacyCombinerBlockArgument> Blocks,
    IReadOnlyList<TpFlashMapRegion> Regions,
    TpCtrlRamPostbuildArtifactRole ArtifactRole);
