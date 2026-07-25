namespace NvtFwCombiner.Application.Support;

/// <summary>Validates publication snapshot invariants independently from adapters.</summary>
public static class SupportPublicationPolicyValidator
{
    /// <summary>Rejects an invalid or ambiguous publication snapshot.</summary>
    public static void Validate(
        SupportPublicationPolicySnapshot policy,
        SupportPublicationPolicySnapshot? supersededPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ValidateSnapshot(policy);

        bool supersedesDecisions = policy.Decisions.Any(
            static decision => decision.SupersedesDecisionIds.Count != 0);
        bool supersedesPolicy = policy.SupersedesPolicyVersion is not null;
        if (supersedesPolicy != (policy.SupersedesPolicySha256 is not null))
        {
            throw new ArgumentException(
                "Policy supersession requires both the prior version and SHA-256.",
                nameof(policy));
        }

        if (!supersedesPolicy)
        {
            if (supersedesDecisions || supersededPolicy is not null)
            {
                throw new ArgumentException(
                    "Decision supersession requires an exact prior policy snapshot and version.",
                    nameof(policy));
            }

            return;
        }

        if (supersededPolicy is null)
        {
            throw new ArgumentException(
                "Policy supersession requires the exact prior policy snapshot.",
                nameof(supersededPolicy));
        }

        ValidateSnapshot(supersededPolicy);
        if (!StringComparer.Ordinal.Equals(
                policy.PolicyId,
                supersededPolicy.PolicyId) ||
            !StringComparer.Ordinal.Equals(
                policy.SupersedesPolicyVersion,
                supersededPolicy.PolicyVersion) ||
            !StringComparer.Ordinal.Equals(
                policy.SupersedesPolicySha256,
                supersededPolicy.Sha256))
        {
            throw new ArgumentException(
                "The supplied prior policy must match the superseded policy id, version, and SHA-256.",
                nameof(supersededPolicy));
        }

        HashSet<string> priorDecisionIds =
            [.. supersededPolicy.Decisions.Select(static decision => decision.DecisionId)];
        if (policy.Decisions
            .SelectMany(static decision => decision.SupersedesDecisionIds)
            .Any(supersededId => !priorDecisionIds.Contains(supersededId)))
        {
            throw new ArgumentException(
                "Every superseded decision id must exist in the supplied prior policy.",
                nameof(policy));
        }
    }

    private static void ValidateSnapshot(SupportPublicationPolicySnapshot policy)
    {
        ValidateText(policy.PolicyId, nameof(policy.PolicyId));
        ValidateText(policy.PolicyVersion, nameof(policy.PolicyVersion));
        if (policy.Sha256.Length != 64 ||
            policy.Sha256.Any(static character =>
                character is not ((>= '0' and <= '9') or (>= 'a' and <= 'f'))))
        {
            throw new ArgumentException(
                "Publication policy SHA-256 must be 64 lowercase hexadecimal characters.",
                nameof(policy));
        }

        if (policy.SupersedesPolicyVersion is not null)
        {
            ValidateText(
                policy.SupersedesPolicyVersion,
                nameof(policy.SupersedesPolicyVersion));
            if (StringComparer.Ordinal.Equals(
                    policy.PolicyVersion,
                    policy.SupersedesPolicyVersion))
            {
                throw new ArgumentException(
                    "Publication policy cannot supersede its own version.",
                    nameof(policy));
            }
        }

        if (policy.SupersedesPolicySha256 is not null)
        {
            ValidateSha256(
                policy.SupersedesPolicySha256,
                "Superseded publication policy SHA-256");
        }

        if (policy.Decisions.Any(static decision => decision is null) ||
            policy.Decisions.Select(static decision => decision.DecisionId)
                .Distinct(StringComparer.Ordinal).Count() != policy.Decisions.Count ||
            policy.Decisions.Select(static decision => decision.RouteId)
                .Distinct(StringComparer.Ordinal).Count() != policy.Decisions.Count)
        {
            throw new ArgumentException(
                "Publication decisions must have unique ids and route ids.",
                nameof(policy));
        }

        HashSet<string> currentDecisionIds =
            [.. policy.Decisions.Select(static decision => decision.DecisionId)];
        var supersededDecisionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (SupportPublicationDecision decision in policy.Decisions)
        {
            ValidateDecision(decision, currentDecisionIds, supersededDecisionIds);
        }
    }

    private static void ValidateDecision(
        SupportPublicationDecision decision,
        HashSet<string> currentDecisionIds,
        HashSet<string> supersededDecisionIds)
    {
        if (!SupportRouteIdentity.IsCanonicalId(decision.DecisionId) ||
            !SupportRouteIdentity.IsCanonicalId(decision.RouteId))
        {
            throw new ArgumentException(
                "Publication decision and route ids must be canonical ids.",
                nameof(decision));
        }

        if (!Enum.IsDefined(decision.Status) || decision.Provenance is null)
        {
            throw new ArgumentException("Publication decision is invalid.", nameof(decision));
        }

        if (!StringComparer.Ordinal.Equals(
                decision.Provenance.AuthorityKind,
                "owner-decision"))
        {
            throw new ArgumentException(
                "Publication provenance authority must be 'owner-decision'.",
                nameof(decision));
        }

        ValidateText(decision.Provenance.RecordedOn, nameof(decision.Provenance.RecordedOn));
        ValidateText(decision.Provenance.RecordRef, nameof(decision.Provenance.RecordRef));
        ValidateText(decision.Provenance.Rationale, nameof(decision.Provenance.Rationale));
        foreach (string supersededId in decision.SupersedesDecisionIds)
        {
            if (!SupportRouteIdentity.IsCanonicalId(supersededId) ||
                currentDecisionIds.Contains(supersededId) ||
                !supersededDecisionIds.Add(supersededId))
            {
                throw new ArgumentException(
                    "Superseded decision ids must refer uniquely to prior policy decisions.",
                    nameof(decision));
            }
        }
    }

    private static void ValidateText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
    }

    private static void ValidateSha256(string value, string fieldName)
    {
        if (value.Length != 64 ||
            value.Any(static character =>
                character is not ((>= '0' and <= '9') or (>= 'a' and <= 'f'))))
        {
            throw new ArgumentException(
                $"{fieldName} must be 64 lowercase hexadecimal characters.");
        }
    }
}
