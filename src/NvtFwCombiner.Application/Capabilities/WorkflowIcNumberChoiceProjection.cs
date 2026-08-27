using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Validates one compiler-produced workflow count choice.</summary>
internal static class WorkflowIcNumberChoiceProjection
{
    internal static void ValidateCompilation(
        CapabilityNumberChoice? choice,
        CompiledComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        if (choice is null)
        {
            return;
        }

        IcNumberInputMode expected = IcNumberSelection.FromToken(choice.Token).Mode;
        if (composition.V2Details.IcNumberInputMode != expected)
        {
            throw new ArgumentException(
                "Compiled IC-number mode does not match the route's declared count variant.",
                nameof(composition));
        }
    }
}
