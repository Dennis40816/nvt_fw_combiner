namespace NvtFwCombiner.Domain.Composition;

/// <summary>Describes how a profile region is exposed for authoring.</summary>
public enum RegionAccessKind
{
    /// <summary>Hides the region from the authoring surface.</summary>
    Hidden,

    /// <summary>Shows the region without permitting writes.</summary>
    ReadOnly,

    /// <summary>Allows whole-region access only.</summary>
    Whole,

    /// <summary>Allows access to declared region parts.</summary>
    Parts,

    /// <summary>Allows profile-approved explicit ranges.</summary>
    ExplicitRange,
}
