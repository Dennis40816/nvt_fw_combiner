namespace NvtFwCombiner.Domain.Composition;

/// <summary>Profile-owned AB Code characteristics, independent of bank size or display geometry.</summary>
public sealed record AbCodeDefinition(bool IsFullySymmetric);
