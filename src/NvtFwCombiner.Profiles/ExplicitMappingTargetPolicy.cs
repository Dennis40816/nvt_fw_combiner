using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Profile-owned policy that grants explicit mapping writes into a target region.</summary>
public sealed class ExplicitMappingTargetPolicy
{
    private readonly RangeSet _allowedRanges;
    private readonly ByteRange[] _protectedRanges;

    /// <summary>Creates a target policy for general explicit mappings.</summary>
    public ExplicitMappingTargetPolicy(
        string regionId,
        string targetSpaceId,
        IReadOnlyList<ByteRange> allowedRanges,
        RegionAccessKind accessKind,
        RegionWritePolicy writePolicy,
        RegionAtomicity atomicity,
        int alignment,
        IReadOnlyList<ByteRange>? protectedRanges = null,
        IReadOnlyList<string>? requiredProcessorIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSpaceId);
        ArgumentNullException.ThrowIfNull(allowedRanges);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(alignment);

        if (allowedRanges.Count == 0)
        {
            throw new ArgumentException("At least one allowed range is required.", nameof(allowedRanges));
        }

        RegionId = regionId;
        TargetSpaceId = targetSpaceId;
        _allowedRanges = new RangeSet(allowedRanges);
        AccessKind = accessKind;
        WritePolicy = writePolicy;
        Atomicity = atomicity;
        Alignment = alignment;
        _protectedRanges = [.. protectedRanges ?? []];
        RequiredProcessorIds = [.. requiredProcessorIds ?? []];
    }

    /// <summary>Target region id that the request mapping must cite.</summary>
    public string RegionId { get; }

    /// <summary>Target address space that owns the allowed ranges.</summary>
    public string TargetSpaceId { get; }

    /// <summary>Ranges approved by the profile for explicit mappings.</summary>
    public IReadOnlyList<ByteRange> AllowedRanges => _allowedRanges.Ranges;

    /// <summary>Region access exposed to the request authoring surface.</summary>
    public RegionAccessKind AccessKind { get; }

    /// <summary>Write authority granted by this policy.</summary>
    public RegionWritePolicy WritePolicy { get; }

    /// <summary>Smallest permitted write unit for the region.</summary>
    public RegionAtomicity Atomicity { get; }

    /// <summary>Required byte alignment for explicit mapping target ranges.</summary>
    public int Alignment { get; }

    /// <summary>Protected ranges that request mappings must not touch.</summary>
    public IReadOnlyList<ByteRange> ProtectedRanges => _protectedRanges;

    /// <summary>Processors that must be proven before mappings can write this region.</summary>
    public IReadOnlyList<string> RequiredProcessorIds { get; }

    /// <summary>Returns true when the range is inside a profile-approved explicit mapping range.</summary>
    public bool ContainsAllowedRange(ByteRange targetRange)
    {
        return _allowedRanges.Contains(targetRange);
    }

    /// <summary>Returns true when the range overlaps a protected byte range.</summary>
    public bool OverlapsProtectedRange(ByteRange targetRange)
    {
        return _protectedRanges.Any(range => range.Overlaps(targetRange));
    }
}
