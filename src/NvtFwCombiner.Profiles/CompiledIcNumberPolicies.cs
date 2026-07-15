using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

/// <summary>Profiles-owned conversion from validated selector declarations to executable artifact policy.</summary>
internal static class CompiledIcNumberPolicies
{
    internal static CompiledIcNumberPolicy From(IcNumberInputMode? inputMode)
    {
        return inputMode switch
        {
            null => CompiledIcNumberPolicy.NotApplicable,
            IcNumberInputMode.SingleSelector => CompiledIcNumberPolicy.SingleSelector,
            IcNumberInputMode.CascadeSelector => CompiledIcNumberPolicy.CascadeSelector,
            IcNumberInputMode.NumericSelector => CompiledIcNumberPolicy.NumericSelector,
            _ => throw new ArgumentOutOfRangeException(
                nameof(inputMode),
                inputMode,
                "Unknown profile IC-number input mode."),
        };
    }
}
