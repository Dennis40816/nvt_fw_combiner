namespace NvtFwCombiner.Domain.Composition;

/// <summary>Declares how a mapping handles overlap with existing writes.</summary>
public enum OverlapPolicy
{
    /// <summary>Rejects any overlap.</summary>
    Reject,

    /// <summary>Allows overlap only when declared by the profile.</summary>
    AllowDeclared,

    /// <summary>Allows this mapping to replace existing bytes.</summary>
    ReplaceExisting,
}
