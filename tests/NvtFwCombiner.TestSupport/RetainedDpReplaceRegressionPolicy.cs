using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Capabilities;

namespace NvtFwCombiner.TestSupport;

/// <summary>
/// Re-enables retained DP Replace routes only inside byte/UI/adapter regression
/// hosts after the shipped authoring policy hides them.
/// </summary>
public static class RetainedDpReplaceRegressionPolicy
{
    /// <summary>Loads the exact shipped policy with one explicit test-only authoring override.</summary>
    public static CanonicalCapabilityPolicySnapshot Load()
    {
        CanonicalCapabilityPolicySnapshot shipped =
            BuiltInCanonicalCapabilityPolicy.Load();
        CanonicalCapabilityPolicyRoute[] routes =
        [
            .. shipped.Routes.Select(static route =>
                StringComparer.Ordinal.Equals(
                    route.Identity.WorkflowId,
                    ExperienceIds.DpReplace)
                    ? route with
                    {
                        Authoring = new PinnedCapabilityDecision<CapabilityAuthoringAvailability>(
                            $"{route.Authoring.DecisionId}-retained-regression",
                            route.Identity.RouteId,
                            route.CapabilityFingerprint,
                            CapabilityAuthoringAvailability.Available,
                            "test-only:retained-dp-replace-regression"),
                    }
                    : route),
        ];
        return shipped with
        {
            CatalogId = "retained-dp-replace-regression-policy",
            SourceSha256 = new string('d', 64),
            Routes = Array.AsReadOnly(routes),
        };
    }
}
