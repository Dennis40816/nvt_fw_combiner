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
        string definitionId,
        string selectedPath)
    {
        SessionIdentity = sessionIdentity;
        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
        SelectedRouteId = selectedRouteId;
        CapabilityFingerprint = capabilityFingerprint;
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
