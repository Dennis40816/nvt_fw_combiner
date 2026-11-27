// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.MemoryLayout;

/// <summary>Availability of one immutable collection of declared layout facts.</summary>
public enum MemoryLayoutFactStatus
{
    /// <summary>The declaration resolved to one or more values.</summary>
    KnownWithValues,
    /// <summary>The declaration resolved successfully and contains no values.</summary>
    KnownEmpty,
    /// <summary>The required declaration or resolution is unavailable.</summary>
    Unavailable,
}

/// <summary>Typed absence reasons; an unavailable fact never implies an empty declaration.</summary>
public enum MemoryLayoutFactUnavailableReason
{
    /// <summary>The fact is available.</summary>
    None,
    /// <summary>The active profile declares no matching fact.</summary>
    NotDeclared,
    /// <summary>The logical output has no canonical physical access policy.</summary>
    NoPhysicalMap,
    /// <summary>The composite context declares layout authority without a complete active authoring policy.</summary>
    ActiveAuthoringPolicyNotResolved,
    /// <summary>The actual output extends past the canonical authoring access map.</summary>
    OutputExtentNotCoveredByAccessPolicy,
    /// <summary>The current metadata inspection has not resolved the declared field.</summary>
    MetadataNotResolved,
    /// <summary>The current plan does not prove a complete output location.</summary>
    OutputLocationNotResolved,
}

/// <summary>Owned immutable values with distinct known-empty and unavailable states.</summary>
public sealed class MemoryLayoutFactSet<T> where T : class
{
    private MemoryLayoutFactSet(IEnumerable<T> values, MemoryLayoutFactUnavailableReason reason)
    {
        T[] snapshot = [.. values];
        if (snapshot.Any(static value => value is null))
        {
            throw new ArgumentException("Layout facts cannot contain null.", nameof(values));
        }
        Values = Array.AsReadOnly(snapshot);
        UnavailableReason = reason;
    }

    /// <summary>Availability derived from the owned values and absence reason.</summary>
    public MemoryLayoutFactStatus Status => UnavailableReason != MemoryLayoutFactUnavailableReason.None
        ? MemoryLayoutFactStatus.Unavailable
        : Values.Count == 0 ? MemoryLayoutFactStatus.KnownEmpty : MemoryLayoutFactStatus.KnownWithValues;

    /// <summary>Resolved values; empty when unavailable, without asserting known-empty.</summary>
    public IReadOnlyList<T> Values { get; }

    /// <summary>Typed reason, or None when the collection is known.</summary>
    public MemoryLayoutFactUnavailableReason UnavailableReason { get; }

    internal static MemoryLayoutFactSet<T> Known(IEnumerable<T> values)
    {
        return new(values, MemoryLayoutFactUnavailableReason.None);
    }

    internal static MemoryLayoutFactSet<T> Unavailable(MemoryLayoutFactUnavailableReason reason)
    {
        MemoryLayoutGuard.Defined(reason, nameof(reason));
        return reason == MemoryLayoutFactUnavailableReason.None
            ? throw new ArgumentException("Unavailable facts require an absence reason.", nameof(reason))
            : new([], reason);
    }
}

/// <summary>An effective canonical authoring prohibition, independent of planned writes.</summary>
public sealed record MemoryLayoutReadOnlyRange
{
    internal MemoryLayoutReadOnlyRange(CompiledReadOnlyRange declaration, FirmwareAddressedRange outputRange)
    {
        Declaration = declaration;
        OutputRange = outputRange;
    }

    /// <summary>Exact Domain-owned restriction and its policy/physical provenance.</summary>
    public CompiledReadOnlyRange Declaration { get; }
    /// <summary>Canonical region identity containing this prohibited slice.</summary>
    public string RegionId => Declaration.Region.RegionId;
    /// <summary>Typed canonical reason, including default and physical denial.</summary>
    public CompiledReadOnlyReasonKind ReasonKind => Declaration.ReasonKind;
    /// <summary>Half-open range in the snapshot's output address space.</summary>
    public FirmwareAddressedRange OutputRange { get; }
}

/// <summary>A DP version field placement or declared extractor carrier; this asserts no version value.</summary>
public abstract record MemoryLayoutVersionLocation
{
    private protected MemoryLayoutVersionLocation(FirmwareAddressedRange outputRange)
    {
        OutputRange = outputRange;
    }

    /// <summary>Exact canonical field or extractor-carrier identity.</summary>
    public abstract string FieldId { get; }
    /// <summary>Half-open byte carrier range in the snapshot's output address space.</summary>
    public FirmwareAddressedRange OutputRange { get; }
}

/// <summary>One declared metadata version field at its proven output placement.</summary>
public sealed record MemoryLayoutMetadataVersionLocation : MemoryLayoutVersionLocation
{
    internal MemoryLayoutMetadataVersionLocation(string bindingId, string structureId,
        FirmwareMetadataField field, FirmwareAddressedRange outputRange) : base(outputRange)
    {
        BindingId = bindingId;
        StructureId = structureId;
        Field = field;
    }

    /// <summary>Exact active profile metadata binding.</summary>
    public string BindingId { get; }
    /// <summary>Canonical metadata structure binding identity.</summary>
    public string StructureId { get; }
    /// <summary>Exact canonical field, including its byte carrier and optional bit slice.</summary>
    public FirmwareMetadataField Field { get; }
    /// <inheritdoc/>
    public override string FieldId => Field.FieldId;
}

/// <summary>Declared AB CMI extractor carrier, including tracker bits; geometry does not assert a readable version.</summary>
public sealed record MemoryLayoutVersionCarrierLocation : MemoryLayoutVersionLocation
{
    internal MemoryLayoutVersionCarrierLocation(CompiledInputVersionKind versionKind, FirmwareRegion canonicalRegion,
        FirmwareAddressedRange outputRange) : base(outputRange)
    {
        VersionKind = versionKind;
        CanonicalRegion = canonicalRegion;
    }

    /// <summary>Existing extractor's typed DP bank identity.</summary>
    public CompiledInputVersionKind VersionKind { get; }
    /// <summary>Exact canonical carrier declaration in the complete output map.</summary>
    public FirmwareRegion CanonicalRegion { get; }
    /// <inheritdoc/>
    public override string FieldId => CanonicalRegion.RegionId;
}
