using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Temporary projection from the atomic artifact to the legacy Application request shape.</summary>
internal static class CompiledCompositionRunAdapter
{
    internal static CompositionRunProfile ToLegacyRunProfile(CompiledComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        _ = composition.Authority is LegacyProfileCompilationAuthority &&
            composition.Eligibility == CompiledCompositionEligibility.LegacyRuntimeExecutable
                ? true
                : throw new InvalidOperationException(
                    "The legacy Application request adapter accepts only legacy-runtime executable artifacts.");

        return new CompositionRunProfile(
            composition.ProfileId,
            composition.ProfileVersion,
            composition.IcId,
            composition.ModeId,
            composition.ExperienceId,
            composition.CompositionKind,
            ToLegacyIcNumberInputMode(composition.IcNumberPolicy));
    }

    private static IcNumberInputMode? ToLegacyIcNumberInputMode(CompiledIcNumberPolicy policy)
    {
        return policy switch
        {
            CompiledIcNumberPolicy.NotApplicable => null,
            CompiledIcNumberPolicy.SingleSelector => IcNumberInputMode.SingleSelector,
            CompiledIcNumberPolicy.CascadeSelector => IcNumberInputMode.CascadeSelector,
            CompiledIcNumberPolicy.NumericSelector => IcNumberInputMode.NumericSelector,
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown compiled IC-number policy."),
        };
    }
}
