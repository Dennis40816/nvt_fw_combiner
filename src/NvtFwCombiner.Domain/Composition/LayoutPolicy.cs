namespace NvtFwCombiner.Domain.Composition;

/// <summary>Constrains how source content may map into the output layout.</summary>
public enum LayoutPolicy
{
    /// <summary>Uses a fixed profile-defined layout.</summary>
    Fixed,

    /// <summary>Allows only constrained profile-approved layout choices.</summary>
    Constrained,

    /// <summary>Allows explicit user-defined layout mappings within policy.</summary>
    UserDefined,
}
