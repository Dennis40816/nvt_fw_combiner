namespace NvtFwCombiner.Contracts;

/// <summary>References a versioned firmware profile contract.</summary>
/// <param name="ProfileId">Stable profile identifier.</param>
/// <param name="ProfileVersion">Profile schema or content version.</param>
public sealed record ProfileReference(string ProfileId, string ProfileVersion);
