// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.InputInspection;

/// <summary>Path-free occurrence identity within one slot, independent of list position or message text.</summary>
/// <param name="SlotId">The authoring slot that owns the finding.</param>
/// <param name="AddressSpaceId">The input address space that was inspected.</param>
/// <param name="IssueCode">The stable issue code.</param>
/// <param name="SourceRange">The source range the finding is about, when it has one.</param>
/// <param name="SubjectId">
/// What the finding is about inside the slot: the validation rule id for a range finding, the admission
/// operation id or else the address space for the inspection summary, and the slot id for advisories and
/// unreadable sources. Use it only as part of the identity, not as a typed reference.
/// </param>
public sealed record CompiledInputArtifactFindingIdentity(
    string SlotId,
    string AddressSpaceId,
    string IssueCode,
    ByteRange? SourceRange,
    string? SubjectId);

/// <summary>One evaluated diagnostic; the inspection result separately retains its admission summary.</summary>
public sealed record CompiledInputArtifactInspectionFinding(
    CompiledInputArtifactFindingIdentity Identity,
    string Severity,
    CompiledInputArtifactInspectionNextAction NextAction,
    InputDiagnosticEvidence? DiagnosticEvidence,
    CompositionIssue? Issue = null)
{
    internal static CompiledInputArtifactInspectionFinding FromSummary(
        CompiledInputArtifactInspectionResult inspection)
    {
        return new CompiledInputArtifactInspectionFinding(
            new CompiledInputArtifactFindingIdentity(
                inspection.SlotId,
                inspection.AddressSpaceId,
                inspection.IssueCode,
                inspection.DiagnosticEvidence?.SourceRange ??
                    (inspection.AdmissionIssue is null ? inspection.IgnoredTrailingRange : null),
                inspection.AdmissionIssue?.OperationId ?? inspection.AddressSpaceId),
            inspection.BlocksBuild ? CompositionIssueSeverity.Error : CompositionIssueSeverity.Warning,
            inspection.NextAction,
            inspection.DiagnosticEvidence,
            inspection.AdmissionIssue);
    }
}
