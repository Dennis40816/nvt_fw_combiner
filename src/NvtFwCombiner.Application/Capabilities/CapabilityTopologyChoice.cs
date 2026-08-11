using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>One explicit topology choice admitted by a canonical capability route.</summary>
public sealed record CapabilityTopologyChoice(
    string Token,
    TopologySelection Selection)
{
    /// <summary>User-facing topology label from the typed selection.</summary>
    public string DisplayLabel => Selection.Label;
}
