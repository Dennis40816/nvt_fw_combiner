using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>
/// Complete identity captured when an explicit selected-file inspection or
/// reload begins.
/// </summary>
public sealed class AuthoringSlotInspectionLease
{
    internal AuthoringSlotInspectionLease(
        object sessionIdentity,
        ResolutionToken resolutionToken,
        AuthoringRevision authoringRevision,
        string selectedRouteId,
        string capabilityFingerprint,
        string? compilationFingerprint,
        ReviewedDiscoveryTransition? discoveryTransition,
        string definitionId,
        string selectedPath)
    {
        SessionIdentity = sessionIdentity;
        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
        SelectedRouteId = selectedRouteId;
        CapabilityFingerprint = capabilityFingerprint;
        CompilationFingerprint = compilationFingerprint;
        DiscoveryTransition = discoveryTransition;
        DefinitionId = definitionId;
        SelectedPath = selectedPath;
    }

    internal object SessionIdentity { get; }

    /// <summary>Canonical publication identity at inspection start.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Authoring revision advanced by the explicit selection/reload.</summary>
    public AuthoringRevision AuthoringRevision { get; }

    /// <summary>Exact selected route at inspection start.</summary>
    public string SelectedRouteId { get; }

    /// <summary>Reviewed capability-definition fingerprint at inspection start.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Exact compilation active when the selected-file inspection began.</summary>
    public string? CompilationFingerprint { get; }

    /// <summary>Reviewed discovery-to-exact transition captured before prerequisite resolution.</summary>
    public ReviewedDiscoveryTransition? DiscoveryTransition { get; }

    /// <summary>Resolved slot definition being inspected.</summary>
    public string DefinitionId { get; }

    /// <summary>Selected path hint used only to reject stale results.</summary>
    public string SelectedPath { get; }
}

/// <summary>Result of beginning one explicit selected-file inspection/reload.</summary>
public sealed record AuthoringSlotInspectionStartResult(
    ActiveSessionSnapshot? Snapshot,
    AuthoringSlotInspectionLease? Lease,
    AuthoringSessionIssue? Issue)
{
    /// <summary>True only when a current Checking snapshot and lease exist.</summary>
    public bool Succeeded => Snapshot is not null && Lease is not null && Issue is null;
}

/// <summary>Result of beginning one atomic batch of selected-file inspections/reloads.</summary>
public sealed record AuthoringSlotInspectionBatchStartResult(
    ActiveSessionSnapshot? Snapshot,
    IReadOnlyList<AuthoringSlotInspectionLease> Leases,
    AuthoringSessionIssue? Issue)
{
    /// <summary>True only when every requested slot has a lease for the same current revision.</summary>
    public bool Succeeded => Snapshot is not null && Leases.Count > 0 && Issue is null;
}
