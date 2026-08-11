using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>Closed authoring-draft contracts admitted by session policy.</summary>
public enum AuthoringDraftKind
{
    /// <summary>One typed General Merge/Replace explicit-mapping draft.</summary>
    GeneralMapping,
    /// <summary>One exact General Merge initializer plus shared mapping draft.</summary>
    GeneralMerge,
    /// <summary>One owner-confirmed CtrlRAM TP firmware-version edit.</summary>
    CtrlRamFirmwareVersionEdit,
}

/// <summary>Typed CtrlRAM firmware-version compiler input owned by one authoring revision.</summary>
public sealed record CtrlRamFirmwareVersionDraftState : AuthoringDraftState
{
    /// <summary>Creates the exact TP firmware-version values compiled for Build.</summary>
    public CtrlRamFirmwareVersionDraftState(
        byte firmwareVersion,
        byte firmwareSubVersion)
        : base(AuthoringDraftKind.CtrlRamFirmwareVersionEdit)
    {
        FirmwareVersion = firmwareVersion;
        FirmwareSubVersion = firmwareSubVersion;
    }

    /// <summary>Owner-confirmed firmware version byte.</summary>
    public byte FirmwareVersion { get; }

    /// <summary>Owner-confirmed firmware sub-version byte.</summary>
    public byte FirmwareSubVersion { get; }

    internal override AuthoringDraftState CreateImmutableSnapshot()
    {
        return this;
    }

    internal override bool HasSameValue(AuthoringDraftState other)
    {
        return other is CtrlRamFirmwareVersionDraftState edit &&
            FirmwareVersion == edit.FirmwareVersion &&
            FirmwareSubVersion == edit.FirmwareSubVersion;
    }
}

/// <summary>Result of compiling and re-inspecting one typed CtrlRAM authoring transition.</summary>
public sealed record CtrlRamAuthoringTransitionResult(
    ActiveSessionSnapshot? Session,
    IReadOnlyList<CompositionIssue> Issues)
{
    /// <summary>True only when the new exact compilation owns current accepted input inspection.</summary>
    public bool Succeeded =>
        Session?.GetAcceptedCapability(AuthoringDerivedResultKind.Inspection) is not null &&
        Issues.Count == 0;
}

/// <summary>Closed typed draft carried by one authoring session.</summary>
public abstract record AuthoringDraftState
{
    internal AuthoringDraftState(AuthoringDraftKind draftKind)
    {
        if (!Enum.IsDefined(draftKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(draftKind), draftKind, "Unknown authoring draft kind.");
        }
        DraftKind = draftKind;
    }

    /// <summary>Stable identity of the concrete typed draft contract.</summary>
    public AuthoringDraftKind DraftKind { get; }

    /// <summary>Defensively projects caller-owned state before publication.</summary>
    internal abstract AuthoringDraftState CreateImmutableSnapshot();

    /// <summary>Compares the complete typed authoring value without relying on collection identity.</summary>
    internal virtual bool HasSameValue(AuthoringDraftState other)
    {
        return Equals(this, other);
    }
}

/// <summary>Stable authoring-session issue.</summary>
public sealed record AuthoringSessionIssue(string Code, string Message, string? Subject = null);

/// <summary>Stable issue codes shared by UI and CLI session adapters.</summary>
public static class AuthoringSessionIssueCodes
{
    /// <summary>The workflow has no authorable exact route.</summary>
    public const string CatalogUnavailable = "authoring.session.catalog-unavailable";

    /// <summary>The selected IC and IC Count do not identify a route.</summary>
    public const string RouteUnavailable = "authoring.session.route-unavailable";

    /// <summary>The selected axes identify multiple map variants.</summary>
    public const string RouteAmbiguous = "authoring.session.route-ambiguous";

    /// <summary>The selected slot definition is absent from the active route.</summary>
    public const string SlotUnavailable = "authoring.session.slot-unavailable";

    /// <summary>The workflow does not declare authoring-draft semantics.</summary>
    public const string DraftUnavailable = "authoring.session.draft-unavailable";

    /// <summary>The asynchronous result belongs to older session state.</summary>
    public const string StalePublication = "authoring.session.publication-stale";

    /// <summary>The selected-file inspection belongs to older session state.</summary>
    public const string StaleInspection = "authoring.session.inspection-stale";

    /// <summary>The result kind does not match its captured lease.</summary>
    public const string InvalidPublication = "authoring.session.publication-invalid";
}

/// <summary>Typed outcome of one authoring-state transition.</summary>
public sealed record AuthoringSessionTransitionResult(
    ActiveSessionSnapshot? Snapshot,
    AuthoringSessionIssue? Issue)
{
    /// <summary>True only when a coherent new or unchanged snapshot is available.</summary>
    public bool Succeeded => Snapshot is not null && Issue is null;
}

/// <summary>One slot identity captured with an asynchronous publication lease.</summary>
public sealed record AuthoringSlotPublicationIdentity(
    string DefinitionId,
    string? SelectedPath,
    FileStamp? FileStamp);

/// <summary>Complete identity required before an asynchronous result may publish.</summary>
public sealed class AuthoringPublicationLease
{
    internal AuthoringPublicationLease(
        object sessionIdentity,
        AuthoringDerivedResultKind kind,
        ResolutionToken resolutionToken,
        AuthoringRevision authoringRevision,
        string selectedRouteId,
        string capabilityFingerprint,
        IEnumerable<AuthoringSlotPublicationIdentity> slots,
        string? compilationFingerprint)
    {
        ArgumentNullException.ThrowIfNull(sessionIdentity);
        if (compilationFingerprint is not null &&
            !CapabilityRouteIdentity.IsSha256(compilationFingerprint))
        {
            throw new ArgumentException(
                "Compilation fingerprint must be a lowercase SHA-256 value.",
                nameof(compilationFingerprint));
        }
        SessionIdentity = sessionIdentity;
        Kind = kind;
        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
        SelectedRouteId = selectedRouteId;
        CapabilityFingerprint = capabilityFingerprint;
        CompilationFingerprint = compilationFingerprint;
        Slots = Array.AsReadOnly([.. slots]);
    }

    internal object SessionIdentity { get; }

    /// <summary>Expected result kind.</summary>
    public AuthoringDerivedResultKind Kind { get; }

    /// <summary>Captured canonical publication identity.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Captured authoring-input revision.</summary>
    public AuthoringRevision AuthoringRevision { get; }

    /// <summary>Captured exact route.</summary>
    public string SelectedRouteId { get; }

    /// <summary>Captured firmware-semantic identity.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Expected exact compilation identity, when work is compilation-bound.</summary>
    public string? CompilationFingerprint { get; }

    /// <summary>Captured slot definition, path, and file-stamp identities.</summary>
    public IReadOnlyList<AuthoringSlotPublicationIdentity> Slots { get; }
}

/// <summary>Typed result of one derived-result publication attempt.</summary>
public sealed record AuthoringPublicationResult(bool Succeeded, AuthoringSessionIssue? Issue);
