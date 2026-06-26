namespace NvtFwCombiner.Domain.Composition;

/// <summary>Declares write authority for a profile region.</summary>
public enum RegionWritePolicy
{
    /// <summary>Disallows writes to the region.</summary>
    Forbidden,

    /// <summary>Allows whole-region writes only.</summary>
    WholeOnly,

    /// <summary>Allows writes to declared parts only.</summary>
    DeclaredParts,

    /// <summary>Allows explicit mappings approved by the profile.</summary>
    GeneralExplicit,
}
