namespace NvtFwCombiner.Domain.Composition;

/// <summary>Defines the smallest safe write unit for a region.</summary>
public enum RegionAtomicity
{
    /// <summary>The whole region must move as one unit.</summary>
    Whole,

    /// <summary>The region may move by declared partitions.</summary>
    Partitioned,

    /// <summary>The region may move by explicit mappings.</summary>
    ExplicitMapping,
}
