namespace NvtFwCombiner.Domain.Composition;

/// <summary>Describes whether an experience accepts fixed or extensible inputs.</summary>
public enum InputPolicy
{
    /// <summary>Uses the fixed set of inputs required by the profile.</summary>
    Fixed,

    /// <summary>Allows profile-approved extensible input bindings.</summary>
    Extensible,
}
