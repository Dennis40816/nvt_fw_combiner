namespace NvtFwCombiner.Profiles.V2;

/// <summary>Closed profile address-space kind.</summary>
internal enum CompositionProfileSpaceKind
{
    InputArtifact,
    WorkBuffer,
    OutputImage,
}

/// <summary>Closed input-space instance policy.</summary>
internal enum CompositionProfileInstancePolicy
{
    Singleton,
    PerBinding,
}

/// <summary>Closed mutable-space capacity kind.</summary>
internal enum CompositionProfileCapacityKind
{
    ResolvedMap,
    Fixed,
}

/// <summary>Base value for one normalized mutable-space capacity.</summary>
internal abstract record CompositionProfileCapacity
{
    protected CompositionProfileCapacity(CompositionProfileCapacityKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown profile capacity kind.");
        }

        Kind = kind;
    }

    internal CompositionProfileCapacityKind Kind { get; }
}

/// <summary>Uses the uniquely resolved firmware map capacity.</summary>
internal sealed record ResolvedMapProfileCapacity()
    : CompositionProfileCapacity(CompositionProfileCapacityKind.ResolvedMap);

/// <summary>Uses one explicit positive profile-owned capacity.</summary>
internal sealed record FixedProfileCapacity : CompositionProfileCapacity
{
    internal FixedProfileCapacity(long bytes)
        : base(CompositionProfileCapacityKind.Fixed)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
        Bytes = bytes;
    }

    internal long Bytes { get; }
}

/// <summary>Closed engine-owned mutable-space initialization kind.</summary>
internal enum CompositionProfileInitializerKind
{
    Blank,
    Clone,
}

/// <summary>Base value for one normalized mutable-space initializer.</summary>
internal abstract record CompositionProfileInitializer
{
    protected CompositionProfileInitializer(CompositionProfileInitializerKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown profile initializer kind.");
        }

        Kind = kind;
    }

    internal CompositionProfileInitializerKind Kind { get; }
}

/// <summary>Initializes an engine-owned mutable space with one byte value.</summary>
internal sealed record BlankProfileInitializer : CompositionProfileInitializer
{
    internal BlankProfileInitializer(byte fillByte)
        : base(CompositionProfileInitializerKind.Blank)
    {
        FillByte = fillByte;
    }

    internal byte FillByte { get; }
}

/// <summary>Initializes an engine-owned mutable space by cloning one immutable input slot.</summary>
internal sealed record CloneProfileInitializer : CompositionProfileInitializer
{
    internal CloneProfileInitializer(string sourceSlotId)
        : base(CompositionProfileInitializerKind.Clone)
    {
        SourceSlotId = CompositionProfileValueRules.RequireId(sourceSlotId, nameof(sourceSlotId));
    }

    internal string SourceSlotId { get; }
}

/// <summary>Base value for one normalized profile address space.</summary>
internal abstract record CompositionProfileSpace
{
    protected CompositionProfileSpace(string spaceId, CompositionProfileSpaceKind kind)
    {
        SpaceId = CompositionProfileValueRules.RequireId(spaceId, nameof(spaceId));
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
        CompositionProfileInstancePolicy instancePolicy)
        : base(spaceId, CompositionProfileSpaceKind.InputArtifact)
    {
        SlotId = CompositionProfileValueRules.RequireId(slotId, nameof(slotId));
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

    internal CompositionProfileInstancePolicy InstancePolicy { get; }
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
