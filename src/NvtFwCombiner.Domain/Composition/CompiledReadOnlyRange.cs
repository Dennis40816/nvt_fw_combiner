// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Composition;

/// <summary>Canonical cause of an effective authoring prohibition.</summary>
public enum CompiledReadOnlyReasonKind
{
    /// <summary>No active profile access rule authorizes the range.</summary>
    NoAccessRule,
    /// <summary>An applicable profile access rule prohibits the range.</summary>
    ProfileAccess,
    /// <summary>A canonical physical region or ancestor prohibits the range.</summary>
    PhysicalWriteConstraint,
}

/// <summary>One effective authoring prohibition derived by the same rules used for compilation.</summary>
public sealed record CompiledReadOnlyRange
{
    internal CompiledReadOnlyRange(FirmwareRegion region, FirmwareAddressedRange range,
        CompiledReadOnlyReasonKind reasonKind, FirmwareRegion governingRegion,
        CompiledRegionAccessRequirement? accessRule = null)
    {
        Region = region;
        Range = range;
        ReasonKind = reasonKind;
        GoverningRegion = governingRegion;
        AccessRule = accessRule;
    }

    /// <summary>Smallest canonical region containing this prohibited slice.</summary>
    public FirmwareRegion Region { get; }
    /// <summary>Checked half-open range in the canonical map address space.</summary>
    public FirmwareAddressedRange Range { get; }
    /// <summary>Typed reason; no missing rule is treated as permission.</summary>
    public CompiledReadOnlyReasonKind ReasonKind { get; }
    /// <summary>Exact canonical region or ancestor responsible for the restriction.</summary>
    public FirmwareRegion GoverningRegion { get; }
    /// <summary>Exact profile rule for ProfileAccess; absent for physical or default denial.</summary>
    public CompiledRegionAccessRequirement? AccessRule { get; }
}
