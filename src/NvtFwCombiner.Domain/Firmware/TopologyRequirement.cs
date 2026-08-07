namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Topology predicate kind used by firmware-map applicability.</summary>
public enum TopologyRequirementKind
{
    /// <summary>The fact is independent of chip topology.</summary>
    None,

    /// <summary>The fact requires exactly one chip.</summary>
    SingleChip,

    /// <summary>The fact requires a bounded or unbounded cascade.</summary>
    Cascade,

    /// <summary>The fact requires one exact chip count.</summary>
    ExactCount,
}

/// <summary>Provenance of a selected chip topology.</summary>
public enum TopologySelectionSource
{
    /// <summary>The caller selected the topology explicitly.</summary>
    Requested,

    /// <summary>The topology was decoded from approved firmware metadata.</summary>
    Derived,
}

/// <summary>One immutable selected chip count plus its provenance.</summary>
public sealed record TopologySelection
{
    /// <summary>Creates a selected topology.</summary>
    public TopologySelection(
        int chipCount,
        string label,
        TopologySelectionSource source,
        string sourceId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chipCount);
        Label = RequiredValue.NotBlank(label);
        ClosedEnum.ThrowIfUndefined(source, "Unknown topology selection source.");

        SourceId = RequiredValue.NotBlank(sourceId);
        ChipCount = chipCount;
        Source = source;
    }

    /// <summary>Selected positive chip count.</summary>
    public int ChipCount { get; }

    /// <summary>Stable topology label selected by the request or decoded metadata.</summary>
    public string Label { get; }

    /// <summary>How the chip count was selected.</summary>
    public TopologySelectionSource Source { get; }

    /// <summary>Stable request field or metadata fact identifier.</summary>
    public string SourceId { get; }
}

/// <summary>Closed topology requirement independent from a runtime selection.</summary>
public sealed record TopologyRequirement
{
    private TopologyRequirement(
        TopologyRequirementKind kind,
        int? minimumChipCount,
        int? maximumChipCount,
        int? exactChipCount)
    {
        Kind = kind;
        MinimumChipCount = minimumChipCount;
        MaximumChipCount = maximumChipCount;
        ExactChipCount = exactChipCount;
    }

    /// <summary>Requirement kind.</summary>
    public TopologyRequirementKind Kind { get; }

    /// <summary>Inclusive cascade minimum, when applicable.</summary>
    public int? MinimumChipCount { get; }

    /// <summary>Inclusive cascade maximum, when applicable.</summary>
    public int? MaximumChipCount { get; }

    /// <summary>Required exact count, when applicable.</summary>
    public int? ExactChipCount { get; }

    /// <summary>Canonical contract identifier for this requirement kind.</summary>
    public string CanonicalId => Kind switch
    {
        TopologyRequirementKind.None => "none",
        TopologyRequirementKind.SingleChip => "single",
        TopologyRequirementKind.Cascade => "cascade",
        TopologyRequirementKind.ExactCount => "exact-count",
        _ => throw new InvalidOperationException("Unknown topology requirement kind."),
    };

    /// <summary>Creates a topology-independent requirement.</summary>
    public static TopologyRequirement NoTopologyConstraint()
    {
        return new TopologyRequirement(TopologyRequirementKind.None, null, null, null);
    }

    /// <summary>Creates a single-chip requirement.</summary>
    public static TopologyRequirement RequireSingleChip()
    {
        return new TopologyRequirement(TopologyRequirementKind.SingleChip, 1, 1, 1);
    }

    /// <summary>Creates a cascade requirement with an optional inclusive maximum.</summary>
    public static TopologyRequirement RequireCascade(int minimumChipCount = 2, int? maximumChipCount = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumChipCount, 2);
        _ = maximumChipCount is null || maximumChipCount >= minimumChipCount
            ? true
            : throw new ArgumentOutOfRangeException(
                nameof(maximumChipCount),
                maximumChipCount,
                "Cascade maximum cannot be smaller than its minimum.");

        return new TopologyRequirement(
            TopologyRequirementKind.Cascade,
            minimumChipCount,
            maximumChipCount,
            null);
    }

    /// <summary>Creates an exact-count requirement.</summary>
    public static TopologyRequirement RequireExactCount(int chipCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chipCount);
        return new TopologyRequirement(TopologyRequirementKind.ExactCount, chipCount, chipCount, chipCount);
    }

    /// <summary>Returns whether a runtime selection satisfies this requirement.</summary>
    public bool Matches(TopologySelection? selection)
    {
        return Kind switch
        {
            TopologyRequirementKind.None => true,
            TopologyRequirementKind.SingleChip => selection?.ChipCount == 1,
            TopologyRequirementKind.Cascade =>
                selection is not null &&
                selection.ChipCount >= MinimumChipCount &&
                (MaximumChipCount is null || selection.ChipCount <= MaximumChipCount),
            TopologyRequirementKind.ExactCount => selection?.ChipCount == ExactChipCount,
            _ => false,
        };
    }
}
