namespace NvtFwCombiner.Domain.Firmware;

internal enum FirmwareMapPendingRequirementKind
{
    RequestedTopologyMissing,
    CommonFirmwareCategoryDerivationUnavailable,
    MetadataResolutionRequired,
}

internal sealed class FirmwareMapApplicabilityEvaluation
{
    private FirmwareMapApplicabilityEvaluation(
        FirmwareApplicabilityResult result,
        IReadOnlyList<FirmwareMapPendingRequirementKind> pendingRequirements)
    {
        Result = result;
        PendingRequirements = pendingRequirements;
    }

    internal FirmwareApplicabilityResult Result { get; }

    internal IReadOnlyList<FirmwareMapPendingRequirementKind> PendingRequirements { get; }

    internal static FirmwareMapApplicabilityEvaluation Match()
    {
        return new(FirmwareApplicabilityResult.Match, []);
    }

    internal static FirmwareMapApplicabilityEvaluation NoMatch()
    {
        return new(FirmwareApplicabilityResult.NoMatch, []);
    }

    internal static FirmwareMapApplicabilityEvaluation Pending(
        IEnumerable<FirmwareMapPendingRequirementKind> pendingRequirements)
    {
        ArgumentNullException.ThrowIfNull(pendingRequirements);
        FirmwareMapPendingRequirementKind[] snapshot = [.. pendingRequirements];
        if (snapshot.Any(static requirement => !ClosedEnum.IsDefined(requirement)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pendingRequirements),
                "Unknown pending requirement kind.");
        }

        DomainInvariant.Reject(
            snapshot.Length == 0 || snapshot.Distinct().Count() != snapshot.Length,
            snapshot.Length == 0
                ? "Only a pending applicability result may contain pending requirements."
                : "Pending applicability requirements must be unique.",
            nameof(pendingRequirements));
        Array.Sort(snapshot);
        return new(
            FirmwareApplicabilityResult.Pending,
            Array.AsReadOnly(snapshot));
    }
}
