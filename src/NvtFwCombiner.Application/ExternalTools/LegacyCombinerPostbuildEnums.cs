namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Legacy combiner command families observed in owner-provided postbuild scripts.</summary>
public enum LegacyCombinerCommandFamily
{
    /// <summary>Earliest normal mode, represented by CRC_Enable, CRC32_Enable, or CRC_Disable.</summary>
    NormalMode,

    /// <summary>Merge-only command shape used by NT51927 postbuild before CRC generation.</summary>
    MergeMode,

    /// <summary>Newer NTxxxxxBASED_NORMAL_MODE command shape with an explicit CRC method argument.</summary>
    NtBasedNormalMode,

    /// <summary>CRC-only command shape used after staged merge commands.</summary>
    CrcOnlyMode,
}

/// <summary>Postbuild branch selected from IC number context.</summary>
public enum LegacyCombinerPostbuildBranch
{
    /// <summary>Single-chip postbuild branch.</summary>
    SingleChip,

    /// <summary>Cascade or multi-chip postbuild branch.</summary>
    Cascade,

    /// <summary>Explicit two-chip postbuild branch.</summary>
    TwoChip,

    /// <summary>Explicit three-chip postbuild branch.</summary>
    ThreeChip,
}

/// <summary>How a transformed postbuild firmware image becomes the final Replace output.</summary>
public enum LegacyCombinerPostbuildAssemblyKind
{
    /// <summary>The staged firmware image already uses final flash coordinates.</summary>
    InPlaceFirmwareImage,

    /// <summary>The staged firmware image is refreshed TP_FW and must be assembled back with base DP bytes.</summary>
    RefreshedTpThenStandardMerge,
}

/// <summary>How a postbuild profile matches FWConfig Common FW versions.</summary>
public enum LegacyCombinerCommonFwVersionMatchKind
{
    /// <summary>The profile applies to exactly one Common FW semantic version.</summary>
    Exact,

    /// <summary>The profile applies to a major Common FW family, such as 1.x.x.</summary>
    Major,
}

/// <summary>Where a combiner block argument reads its source bytes from.</summary>
public enum LegacyCombinerBlockSourceKind
{
    /// <summary>The block source is the staged firmware image itself.</summary>
    FirmwareImage,

    /// <summary>The block source is a generated file under the staged BIN directory.</summary>
    StagedFile,

    /// <summary>The block source is an engine-created immutable artifact materialized under the staged BIN directory.</summary>
    StagedArtifact,
}
