// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>
/// One user segment before policy admission. Source offsets are relative to the named input slot;
/// output offsets are absolute in OutputAddressSpaceId. Length mismatches and malformed output
/// coordinates remain data failures returned by GeneralReplacePolicy, rather than constructor failures.
/// </summary>
public sealed record GeneralReplaceSegment
{
    /// <summary>Creates an immutable segment; no filename or content supplies firmware facts.</summary>
    public GeneralReplaceSegment(string segmentId, string sourceSlotId, ByteRange inputRange,
        string outputAddressSpaceId, long outputStart, long length)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segmentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSlotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputAddressSpaceId);
        SegmentId = segmentId;
        SourceSlotId = sourceSlotId;
        InputRange = inputRange;
        OutputAddressSpaceId = outputAddressSpaceId;
        OutputStart = outputStart;
        Length = length;
    }

    /// <summary>Stable row identity for diagnostics.</summary>
    public string SegmentId { get; }
    /// <summary>Input slot that owns InputRange's address space.</summary>
    public string SourceSlotId { get; }
    /// <summary>Half-open input range relative to SourceSlotId.</summary>
    public ByteRange InputRange { get; }
    /// <summary>Declared output address space.</summary>
    public string OutputAddressSpaceId { get; }
    /// <summary>Requested absolute output start, validated by the policy.</summary>
    public long OutputStart { get; }
    /// <summary>Requested exact copy length, validated by the policy.</summary>
    public long Length { get; }

    internal FirmwareAddressedRange? GetOutputRange()
    {
        return OutputStart < 0 || Length <= 0 || OutputStart > long.MaxValue - Length
            ? null
            : new FirmwareAddressedRange(OutputAddressSpaceId, new ByteRange(OutputStart, Length));
    }
}
