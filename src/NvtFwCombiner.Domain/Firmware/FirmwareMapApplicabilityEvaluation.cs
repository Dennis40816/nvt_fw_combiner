namespace NvtFwCombiner.Domain.Firmware;

/// <summary>Closed reason why one candidate applicability shape cannot yet be decided.</summary>
public enum FirmwareMapPendingRequirementKind
{
    /// <summary>The candidate requires topology but the caller supplied no requested selection.</summary>
    RequestedTopologyMissing,

    /// <summary>The candidate requires Common FW category derivation, whose closed contract is unavailable.</summary>
    CommonFirmwareCategoryDerivationUnavailable,

    /// <summary>Candidate-scoped metadata structures and predicates still require evaluation.</summary>
    MetadataResolutionRequired,
}

/// <summary>Immutable detailed result of the static applicability stage for one map candidate.</summary>
public sealed class FirmwareMapApplicabilityEvaluation : IEquatable<FirmwareMapApplicabilityEvaluation>
{
    private readonly FirmwareMapPendingRequirementKind[] _pendingRequirements;

    private FirmwareMapApplicabilityEvaluation(
        FirmwareApplicabilityResult result,
        IEnumerable<FirmwareMapPendingRequirementKind> pendingRequirements)
    {
        ClosedEnum.ThrowIfUndefined(result, "Unknown applicability result.");

        ArgumentNullException.ThrowIfNull(pendingRequirements);
        _pendingRequirements = [.. pendingRequirements];
        if (_pendingRequirements.Any(static requirement => !ClosedEnum.IsDefined(requirement)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pendingRequirements),
                "Unknown pending requirement kind.");
        }

        if (_pendingRequirements.Distinct().Count() != _pendingRequirements.Length)
        {
            throw new ArgumentException(
                "Pending applicability requirements must be unique.",
                nameof(pendingRequirements));
        }

        Array.Sort(_pendingRequirements);
        bool isPending = result == FirmwareApplicabilityResult.Pending;
        if (isPending != (_pendingRequirements.Length != 0))
        {
            throw new ArgumentException(
                "Only a pending applicability result may contain pending requirements.",
                nameof(pendingRequirements));
        }

        Result = result;
        PendingRequirements = Array.AsReadOnly(_pendingRequirements);
    }

    /// <summary>Three-state static applicability result.</summary>
    public FirmwareApplicabilityResult Result { get; }

    /// <summary>Canonical pending requirements; empty for match and no-match results.</summary>
    public IReadOnlyList<FirmwareMapPendingRequirementKind> PendingRequirements { get; }

    /// <inheritdoc />
    public bool Equals(FirmwareMapApplicabilityEvaluation? other)
    {
        return other is not null &&
            Result == other.Result &&
            _pendingRequirements.SequenceEqual(other._pendingRequirements);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as FirmwareMapApplicabilityEvaluation);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Result);
        foreach (FirmwareMapPendingRequirementKind requirement in _pendingRequirements)
        {
            hash.Add(requirement);
        }

        return hash.ToHashCode();
    }

    internal static FirmwareMapApplicabilityEvaluation Match()
    {
        return new FirmwareMapApplicabilityEvaluation(FirmwareApplicabilityResult.Match, []);
    }

    internal static FirmwareMapApplicabilityEvaluation NoMatch()
    {
        return new FirmwareMapApplicabilityEvaluation(FirmwareApplicabilityResult.NoMatch, []);
    }

    internal static FirmwareMapApplicabilityEvaluation Pending(
        IEnumerable<FirmwareMapPendingRequirementKind> pendingRequirements)
    {
        return new FirmwareMapApplicabilityEvaluation(
            FirmwareApplicabilityResult.Pending,
            pendingRequirements);
    }
}
