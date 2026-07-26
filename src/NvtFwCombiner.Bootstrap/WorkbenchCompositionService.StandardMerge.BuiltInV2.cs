using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryGetBuiltInV2StandardMergeCompilation(
        string icId,
        long? dpInputLength,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCompilePublishedStandardMergeCapability(
                icId,
                out composition,
                out issues) ||
            TryCompileStandardMergeThroughMigrationAdapter(
                icId,
                dpInputLength,
                out composition,
                out issues);
    }

    private static bool TryCompileStandardMergeThroughMigrationAdapter(
        string icId,
        long? dpInputLength,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        if (!BuiltInV2RegistrationRegistry.StandardMergeByIc.TryGetValue(
                icId,
                out BuiltInV2Registration? registration))
        {
            composition = null;
            issues = [];
            return false;
        }

        registration.TryCompile(dpInputLength, out composition, out issues);
        return true;
    }

    private static bool IsBuiltInV2StandardMergeMapCapacityPending(string icId)
    {
        return BuiltInV2RegistrationRegistry.StandardMergeByIc.TryGetValue(
                icId,
                out BuiltInV2Registration? registration) &&
            registration.HasMultipleMapCapacities;
    }

    private static bool TryGetBuiltInV2StandardMergeContainerPolicy(
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

    private static string FormatStandardMergeSupportedDpLengths(string icId)
    {
        return TryGetBuiltInV2StandardMergeContainerPolicy(icId, out V2StandardMergeContainerPolicy? policy)
            ? BuiltInV2Bundle.FormatCapacities(policy.SupportedCapacities)
            : "unavailable";
    }

    private static bool TryGetBuiltInV2StandardMergeAuthoringDefaultCapacity(
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
