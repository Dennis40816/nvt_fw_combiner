namespace NvtFwCombiner.Application.Support;

/// <summary>Validates publication snapshot invariants independently from adapters.</summary>
public static class SupportPublicationPolicyValidator
{
    /// <summary>Rejects an invalid or ambiguous publication snapshot.</summary>
    public static void Validate(SupportPublicationPolicySnapshot policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
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
}
