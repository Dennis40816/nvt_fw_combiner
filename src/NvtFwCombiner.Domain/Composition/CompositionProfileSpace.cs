namespace NvtFwCombiner.Domain.Composition;

/// <summary>Closed profile address-space kind.</summary>
internal enum CompositionProfileSpaceKind
{
    InputArtifact,
    WorkBuffer,
    OutputImage,
}

/// <summary>Base value for one normalized mutable-space capacity.</summary>
internal abstract record CompositionProfileCapacity;

/// <summary>Uses the uniquely resolved firmware map capacity.</summary>
internal sealed record ResolvedMapProfileCapacity()
    : CompositionProfileCapacity;

/// <summary>Uses one explicit positive profile-owned capacity.</summary>
internal sealed record FixedProfileCapacity : CompositionProfileCapacity
{
    internal FixedProfileCapacity(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
        Bytes = bytes;
    }

    internal long Bytes { get; }
}

/// <summary>Requires one positive capacity supplied by a typed logical-output request at compilation time.</summary>
internal sealed record RuntimeRequestProfileCapacity()
    : CompositionProfileCapacity;

/// <summary>Base value for one normalized mutable-space initializer.</summary>
internal abstract record CompositionProfileInitializer;

/// <summary>Initializes an engine-owned mutable space with one byte value.</summary>
internal sealed record BlankProfileInitializer : CompositionProfileInitializer
{
    internal BlankProfileInitializer(byte fillByte)
    {
        FillByte = fillByte;
    }

    internal byte FillByte { get; }
}

/// <summary>Initializes an engine-owned mutable space by cloning one immutable input slot.</summary>
internal sealed record CloneProfileInitializer : CompositionProfileInitializer
{
    internal CloneProfileInitializer(string sourceSlotId)
    {
        SourceSlotId = CanonicalPolicyValueRules.RequireCanonicalId(sourceSlotId, nameof(sourceSlotId));
    }

    internal string SourceSlotId { get; }
}

/// <summary>Base value for one normalized profile address space.</summary>
internal abstract record CompositionProfileSpace
{
    protected CompositionProfileSpace(string spaceId, CompositionProfileSpaceKind kind)
    {
        SpaceId = CanonicalPolicyValueRules.RequireCanonicalId(spaceId, nameof(spaceId));
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown profile space kind.");
        }

        Kind = kind;
    }

    internal string SpaceId { get; }

    internal CompositionProfileSpaceKind Kind { get; }
}

/// <summary>One immutable artifact address space projected from an input slot.</summary>
internal sealed record InputArtifactProfileSpace : CompositionProfileSpace
{
    internal InputArtifactProfileSpace(
        string spaceId,
        string slotId,
        CompiledInputInstancePolicy instancePolicy)
        : base(spaceId, CompositionProfileSpaceKind.InputArtifact)
    {
        SlotId = CanonicalPolicyValueRules.RequireCanonicalId(slotId, nameof(slotId));
        if (!Enum.IsDefined(instancePolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(instancePolicy),
                instancePolicy,
                "Unknown input-space instance policy.");
        }

        InstancePolicy = instancePolicy;
    }

    internal string SlotId { get; }

    internal CompiledInputInstancePolicy InstancePolicy { get; }
}

/// <summary>One engine-owned work buffer or final output image.</summary>
internal sealed record MutableCompositionProfileSpace : CompositionProfileSpace
{
    internal MutableCompositionProfileSpace(
        string spaceId,
        CompositionProfileSpaceKind kind,
        CompositionProfileCapacity capacity,
        CompositionProfileInitializer initializer)
        : base(spaceId, ValidateMutableKind(kind))
    {
        ArgumentNullException.ThrowIfNull(capacity);
        ArgumentNullException.ThrowIfNull(initializer);
        Capacity = capacity;
        Initializer = initializer;
    }

    internal CompositionProfileCapacity Capacity { get; }

    internal CompositionProfileInitializer Initializer { get; }

    private static CompositionProfileSpaceKind ValidateMutableKind(CompositionProfileSpaceKind kind)
    {
        return kind is CompositionProfileSpaceKind.WorkBuffer or CompositionProfileSpaceKind.OutputImage
            ? kind
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Mutable spaces cannot be input artifacts.");
    }
}
