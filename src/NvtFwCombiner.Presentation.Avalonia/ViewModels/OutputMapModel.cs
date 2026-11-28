// Copyright (c) 2026 Dennis Liu. All rights reserved.
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Display facts for one output address space; no firmware facts are inferred here.</summary>
internal sealed class OutputMapModel
{
    /// <summary>Copies ordered inputs into an immutable display snapshot. Keys must be unique across blocks and regions.</summary>
    public OutputMapModel(string addressSpaceId, long outputLength,
        IEnumerable<OutputMapRegion>? regions = null, IEnumerable<OutputMapBlock>? blocks = null,
        IEnumerable<OutputMapSource>? sources = null, IEnumerable<OutputMapCrc>? crcAnnotations = null,
        OutputMapVersion? version = null, OutputMapHighlight? highlight = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentOutOfRangeException.ThrowIfNegative(outputLength);
        AddressSpaceId = addressSpaceId;
        OutputLength = outputLength;
        Regions = Array.AsReadOnly(regions?.ToArray() ?? []);
        Blocks = Array.AsReadOnly(blocks?.ToArray() ?? []);
        Sources = Array.AsReadOnly(sources?.ToArray() ?? []);
        CrcAnnotations = Array.AsReadOnly(crcAnnotations?.ToArray() ?? []);
        Version = version;
        Highlight = highlight;
    }

    /// <summary>Identity of the output address space.</summary>
    public string AddressSpaceId { get; }
    /// <summary>Exclusive output end, in bytes; zero represents an empty output.</summary>
    public long OutputLength { get; }
    /// <summary>Declared physical regions; empty for a logical output.</summary>
    public IReadOnlyList<OutputMapRegion> Regions { get; }
    /// <summary>Writes in step order; later entries cover earlier entries.</summary>
    public IReadOnlyList<OutputMapBlock> Blocks { get; }
    /// <summary>Sources in palette order, independent of block kind or file name.</summary>
    public IReadOnlyList<OutputMapSource> Sources { get; }
    /// <summary>Only explicitly supplied coverage and storage locations are drawn.</summary>
    public IReadOnlyList<OutputMapCrc> CrcAnnotations { get; }
    /// <summary>Optional declared version field.</summary>
    public OutputMapVersion? Version { get; }
    /// <summary>Optional issue-list highlight.</summary>
    public OutputMapHighlight? Highlight { get; }
}

/// <summary>A declared region in the output address space.</summary>
/// <param name="Key">Stable selection key, unique across regions and blocks.</param>
/// <param name="Name">Display name.</param>
/// <param name="Range">Half-open output range.</param>
/// <param name="IsReadOnly">Declared effective access, never inferred from contents; null when not declared.</param>
internal sealed record OutputMapRegion(string Key, string Name, ByteRange Range, bool? IsReadOnly = false);

/// <summary>A step's declared write and optional source read.</summary>
/// <param name="Key">Stable selection key, unique across regions and blocks.</param>
/// <param name="TargetRange">Half-open output range.</param>
/// <param name="Kind">Display operation kind.</param>
/// <param name="Label">Step label.</param>
/// <param name="SourceKey">Optional identity in Sources.</param>
/// <param name="SourceRange">Optional half-open range in that source.</param>
internal sealed record OutputMapBlock(string Key, ByteRange TargetRange, OutputMapBlockKind Kind,
    string Label, string? SourceKey = null, ByteRange? SourceRange = null);

/// <summary>Display operation kinds; these do not select an executor.</summary>
internal enum OutputMapBlockKind
{
    /// <summary>Copy from an explicitly identified source.</summary>
    Copy,
    /// <summary>Neutral fill.</summary>
    Fill,
    /// <summary>Patch bytes.</summary>
    Patch,
    /// <summary>Declared script effect.</summary>
    Script,
}

/// <summary>A source's display identity and byte length.</summary>
/// <param name="Key">Stable source key.</param>
/// <param name="FileName">Display file name.</param>
/// <param name="Length">Source length in bytes.</param>
internal sealed record OutputMapSource(string Key, string FileName, long Length);

/// <summary>CRC coverage and storage are independent optional facts.</summary>
/// <param name="Name">Annotation name.</param>
/// <param name="CoveredRange">Known half-open coverage; null produces no bracket.</param>
/// <param name="StorageRange">Known half-open storage location; null produces no storage marker.</param>
internal sealed record OutputMapCrc(string Name, ByteRange? CoveredRange = null, ByteRange? StorageRange = null);

/// <summary>A declared version field; the control never locates or decodes it.</summary>
/// <param name="Name">Field name.</param>
/// <param name="Range">Half-open output field range.</param>
/// <param name="ValueText">Already decoded display value; null when only the location is known.</param>
internal sealed record OutputMapVersion(string Name, ByteRange Range, string? ValueText);

/// <summary>Display severity supplied by an issue list.</summary>
internal enum OutputMapHighlightSeverity
{
    /// <summary>Warning.</summary>
    Warning,
    /// <summary>Error.</summary>
    Error,
}

/// <summary>Immutable issue highlight, with no inferred overlaps or validation rules.</summary>
internal sealed class OutputMapHighlight
{
    /// <summary>Copies the explicitly supplied output ranges.</summary>
    public OutputMapHighlight(IEnumerable<ByteRange> ranges, OutputMapHighlightSeverity severity)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        Ranges = Array.AsReadOnly(ranges.ToArray());
        Severity = severity;
    }

    /// <summary>Half-open output ranges to emphasize.</summary>
    public IReadOnlyList<ByteRange> Ranges { get; }
    /// <summary>Severity supplied by the caller.</summary>
    public OutputMapHighlightSeverity Severity { get; }
}
