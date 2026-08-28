namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Focused workflow disclosure derived from one canonical capability publication.</summary>
public sealed record CapabilityWorkflowReadiness(
    bool IsAvailable,
    bool HasExactRoute,
    CapabilityEvidenceStatus EvidenceStatus,
    string Reason,
    string OpenCondition)
{
    /// <summary>True when the exact route has reviewed golden, alias, or synthetic evidence.</summary>
    public bool HasReviewedEvidence => EvidenceStatus is
        CapabilityEvidenceStatus.DirectGolden or
        CapabilityEvidenceStatus.ApprovedAlias or
        CapabilityEvidenceStatus.SyntheticOracle;

    /// <summary>True when authoring is available while its reviewed evidence remains open.</summary>
    public bool IsEvidencePending => IsAvailable && !HasReviewedEvidence;
}

/// <summary>Projects one workflow disclosure from the canonical Support Matrix contract.</summary>
public static class CapabilityWorkflowReadinessProjector
{
    /// <summary>Combines canonical onboarding exposure with exact-route evidence.</summary>
    public static CapabilityWorkflowReadiness Project(
        CanonicalSupportMatrixSnapshot? matrix,
        string icId,
        string? workflowId,
        bool authoringExposed,
        string unsupportedReason,
        string openCondition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(unsupportedReason);
        ArgumentException.ThrowIfNullOrWhiteSpace(openCondition);
        if (workflowId is null || matrix is null)
        {
            return Unavailable(unsupportedReason, openCondition);
        }

        CanonicalSupportMatrixRow[] exactRows =
        [
            .. matrix.Rows.Where(row =>
                StringComparer.Ordinal.Equals(row.Identity.IcId, icId) &&
                StringComparer.Ordinal.Equals(row.Identity.WorkflowId, workflowId)),
        ];
        if (!authoringExposed)
        {
            return new CapabilityWorkflowReadiness(
                false,
                exactRows.Length != 0,
                exactRows.Length == 0
                    ? CapabilityEvidenceStatus.Missing
                    : SummarizeEvidence(
                        [.. exactRows.Select(static row => row.Evidence.Value)]),
                unsupportedReason,
                openCondition);
        }

        CapabilityEvidenceStatus[] availableEvidence =
        [
            .. exactRows
                .Where(static row => row.Authoring.Value ==
                    CapabilityAuthoringAvailability.Available)
                .Select(static row => row.Evidence.Value),
        ];
        if (availableEvidence.Length == 0)
        {
            return new CapabilityWorkflowReadiness(
                true,
                false,
                CapabilityEvidenceStatus.Missing,
                "Authoring is available, but no exact canonical executable route is published for the current IC and workflow.",
                "Publish one owner-reviewed exact route before Preview or Build can be admitted.");
        }

        CapabilityEvidenceStatus evidenceStatus = SummarizeEvidence(availableEvidence);
        bool reviewed = evidenceStatus is
            CapabilityEvidenceStatus.DirectGolden or
            CapabilityEvidenceStatus.ApprovedAlias or
            CapabilityEvidenceStatus.SyntheticOracle;
        return new CapabilityWorkflowReadiness(
            true,
            true,
            evidenceStatus,
            reviewed
                ? "Direct or owner-approved fact-scoped golden parity is recorded for this workflow."
                : "The workflow is available, but its direct/fact-scoped golden or owner review is not closed.",
            reviewed
                ? "Golden verification does not grant product support; firmware-owner release review remains separate."
                : "Close the current evidence gaps and firmware-owner review; pending evidence alone does not ban authoring.");
    }

    private static CapabilityEvidenceStatus SummarizeEvidence(
        IReadOnlyList<CapabilityEvidenceStatus> evidence)
    {
        foreach (CapabilityEvidenceStatus status in new[]
                 {
                     CapabilityEvidenceStatus.DirectGolden,
                     CapabilityEvidenceStatus.ApprovedAlias,
                     CapabilityEvidenceStatus.SyntheticOracle,
                     CapabilityEvidenceStatus.ContractOnly,
                     CapabilityEvidenceStatus.Missing,
                 })
        {
            if (evidence.Contains(status))
            {
                return status;
            }
        }

        throw new InvalidOperationException("Canonical evidence uses an unknown status.");
    }

    private static CapabilityWorkflowReadiness Unavailable(
        string reason,
        string openCondition)
    {
        return new CapabilityWorkflowReadiness(
            false,
            false,
            CapabilityEvidenceStatus.Missing,
            reason,
            openCondition);
    }
}

/// <summary>Owner-defined relationship of one IC to a firmware family.</summary>
public enum CapabilityFamilyRelationship
{
    /// <summary>No cross-IC family fact is declared.</summary>
    Standalone,

    /// <summary>All facts in the declared family scope are reusable.</summary>
    PerfectAlias,

    /// <summary>Only the explicitly declared family scope is reusable.</summary>
    PartialAlias,
}

/// <summary>Focused Application contract for owner-defined IC-family disclosure.</summary>
public sealed record CapabilityFamilySummary(
    string? FamilyId,
    CapabilityFamilyRelationship Relationship,
    string? Scope);
