using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Immutable DP execution selection retained by one exact compilation.</summary>
public sealed class AcceptedDpExecutionPlan
{
    /// <summary>Creates one accepted plan without retaining caller-owned selection storage.</summary>
    public AcceptedDpExecutionPlan(IcNumberSelection icNumberSelection)
    {
        ArgumentNullException.ThrowIfNull(icNumberSelection);
        IcNumberSelection = new IcNumberSelection(
            icNumberSelection.Mode,
            icNumberSelection.Parts);
    }

    /// <summary>Exact IC-number selection used when the DP compilation was accepted.</summary>
    public IcNumberSelection IcNumberSelection { get; }
}
