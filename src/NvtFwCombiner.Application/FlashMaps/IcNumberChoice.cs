namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>
/// A human-readable IC-count choice projected from the profile-owned branch tokens.
/// The token is retained for the planner; the label never becomes a firmware input.
/// </summary>
public sealed record IcNumberChoice(string Token, string DisplayLabel);
