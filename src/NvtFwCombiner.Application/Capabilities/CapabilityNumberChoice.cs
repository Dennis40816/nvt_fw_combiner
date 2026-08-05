namespace NvtFwCombiner.Application.Capabilities;

/// <summary>One stable profile-derived IC-number choice exposed to presentation clients.</summary>
public sealed record CapabilityNumberChoice(string Token, string DisplayLabel);
