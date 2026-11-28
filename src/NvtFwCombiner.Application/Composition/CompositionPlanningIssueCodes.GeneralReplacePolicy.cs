// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace NvtFwCombiner.Application.Composition;

public static partial class CompositionPlanningIssueCodes
{
    /// <summary>A segment cannot form a positive, checked output range.</summary>
    public const string GeneralReplaceSegmentRangeInvalid = "general-replace.segment.range-invalid";

    /// <summary>A segment exceeds the immutable base image.</summary>
    public const string GeneralReplaceImageOutOfBounds = "general-replace.segment.image-out-of-bounds";

    /// <summary>A segment is outside the declared output address space.</summary>
    public const string GeneralReplaceAddressSpaceOutOfBounds = "general-replace.segment.address-space-out-of-bounds";

    /// <summary>User-authored output segments overlap.</summary>
    public const string GeneralReplaceSegmentOverlap = "general-replace.segment.overlap";

    /// <summary>The supplied source cannot satisfy the segment's exact read length.</summary>
    public const string GeneralReplaceSourceLengthMismatch = "general-replace.segment.source-length-mismatch";

    /// <summary>A segment touches a canonical named region.</summary>
    public const string GeneralReplaceNamedRegionHit = "general-replace.warning.named-region-hit";

    /// <summary>A segment touches a canonically protected range.</summary>
    public const string GeneralReplaceProtectedRegionHit = "general-replace.warning.protected-region-hit";

    /// <summary>The recompute route may overwrite authored bytes.</summary>
    public const string GeneralReplaceRecomputedRangeHit = "general-replace.warning.recomputed-range-hit";

    /// <summary>Authored bytes are written without refreshing declared integrity write ranges.</summary>
    public const string GeneralReplaceWriteAsIs = "general-replace.warning.write-as-is";

    /// <summary>The IC declares no recompute route.</summary>
    public const string GeneralReplaceNoRecomputeRule = "general-replace.warning.no-recompute-rule";

    /// <summary>Unknown recompute facts cannot establish safe integrity handling.</summary>
    public const string GeneralReplaceRecomputeUnknown = "general-replace.recompute.unknown";
}
