using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.Composition;

internal static class BuiltInV2StandardMergeDiscovery
{
    internal static bool IsBuiltInV2StandardMergeMapCapacityPending(string icId)
    {
        string normalizedIcId = IcIdentifier.Normalize(icId);
        return BuiltInV2RegistrationRegistry.StandardMergeByIc.TryGetValue(
                normalizedIcId,
                out BuiltInV2Registration? registration) &&
            registration.HasMultipleMapCapacities;
    }

    internal static bool TryGetBuiltInV2StandardMergeContainerPolicy(
        string icId,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out V2StandardMergeContainerPolicy? policy)
    {
        if (!BuiltInV2RegistrationRegistry.StandardMergeByIc.TryGetValue(
                icId,
                out BuiltInV2Registration? registration))
        {
            policy = null;
            return false;
        }

        return registration.TryGetContainerPolicy(out policy);
    }

    internal static string FormatStandardMergeSupportedDpLengths(string icId)
    {
        return TryGetBuiltInV2StandardMergeContainerPolicy(icId, out V2StandardMergeContainerPolicy? policy)
            ? BuiltInV2Bundle.FormatCapacities(policy.SupportedCapacities)
            : "unavailable";
    }

    internal static bool TryGetBuiltInV2StandardMergeAuthoringDefaultCapacity(
        string icId,
        out long capacity,
        out IReadOnlyList<CompositionIssue> issues)
    {
        if (!BuiltInV2RegistrationRegistry.StandardMergeByIc.TryGetValue(
                icId,
                out BuiltInV2Registration? registration))
        {
            capacity = 0;
            issues = [];
            return false;
        }

        return registration.TryGetAuthoringDefaultCapacity(out capacity, out issues);
    }
}

internal sealed record V2StandardMergeContainerPolicy(
    IReadOnlyList<long> SupportedCapacities,
    ByteRange TpOverlayRange,
    ByteRange CustomerInfoRange);
