using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>Closed kinds of derived authoring results guarded by one session lease.</summary>
public enum AuthoringDerivedResultKind
{
    /// <summary>Decoded input metadata and input health.</summary>
    Inspection,

    /// <summary>Resolved validation and readiness.</summary>
    Validation,

    /// <summary>Preview output and its report projection.</summary>
    Preview,

    /// <summary>Build output and its report projection.</summary>
    Build,
}

/// <summary>Closed selected-file lifecycle owned by one authoring slot.</summary>
public enum AuthoringSlotLifecycle
{
    /// <summary>No file is selected.</summary>
    Empty,

    /// <summary>A file is selected but no derived result is currently published.</summary>
    Selected,

    /// <summary>An inspection is in progress.</summary>
    Checking,

    /// <summary>The selected file passed inspection.</summary>
    Verified,

    /// <summary>The selected file is usable with a warning.</summary>
    Warning,

    /// <summary>The selected file has a blocking problem.</summary>
    Error,
}

/// <summary>Monotonic identity for one set of authoring inputs.</summary>
public readonly record struct AuthoringRevision
{
    /// <summary>Creates one non-negative revision.</summary>
    public AuthoringRevision(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Revision value used for equality and report provenance.</summary>
    public long Value { get; }

    /// <summary>Returns the next checked revision.</summary>
    public AuthoringRevision Next()
    {
        return new AuthoringRevision(checked(Value + 1));
    }
}

/// <summary>
/// Host-captured file identity. Application compares it but never reads the
/// filesystem or treats it as firmware evidence.
/// </summary>
public readonly record struct FileStamp
{
    /// <summary>Creates one caller-captured file stamp.</summary>
    public FileStamp(bool exists, long length, DateTimeOffset lastWriteTimeUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (!exists && length != 0)
        {
            throw new ArgumentException(
                "A missing file stamp cannot declare a non-zero length.",
                nameof(length));
        }

        if (lastWriteTimeUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "File-stamp timestamps must be normalized to UTC.",
                nameof(lastWriteTimeUtc));
        }

        Exists = exists;
        Length = length;
        LastWriteTimeUtc = lastWriteTimeUtc;
    }

    /// <summary>Whether the host observed the selected path.</summary>
    public bool Exists { get; }

    /// <summary>Observed file length.</summary>
    public long Length { get; }

    /// <summary>Observed UTC last-write time.</summary>
    public DateTimeOffset LastWriteTimeUtc { get; }
}

/// <summary>Reference to one canonical resolved input-slot definition.</summary>
public sealed record AuthoringSlotDefinitionReference
{
    internal AuthoringSlotDefinitionReference(string definitionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        DefinitionId = definitionId;
    }

    /// <summary>Stable slot-definition identity from the resolved input contract.</summary>
    public string DefinitionId { get; }
}

/// <summary>
/// Reference-only authoring route projection. Firmware semantics remain in the
/// resolved capability and compiled composition.
/// </summary>
public sealed record AuthoringCapabilityRoute
{
    private readonly AuthoringSlotDefinitionReference[] _slotDefinitions;

    internal AuthoringCapabilityRoute(
        CapabilityRouteIdentity identity,
        string capabilityFingerprint,
        bool executionAdmitted,
        IEnumerable<AuthoringSlotDefinitionReference> slotDefinitions)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityFingerprint);
        ArgumentNullException.ThrowIfNull(slotDefinitions);
        _slotDefinitions = [.. slotDefinitions];
        if (_slotDefinitions.Length == 0 ||
            _slotDefinitions.Any(static definition => definition is null) ||
            _slotDefinitions.Select(static definition => definition.DefinitionId)
                .Distinct(StringComparer.Ordinal).Count() != _slotDefinitions.Length)
        {
            throw new ArgumentException(
                "Authoring routes require non-empty, unique slot-definition references.",
                nameof(slotDefinitions));
        }

        Array.Sort(
            _slotDefinitions,
            static (left, right) =>
                StringComparer.Ordinal.Compare(left.DefinitionId, right.DefinitionId));
        Identity = identity;
        CapabilityFingerprint = capabilityFingerprint;
        ExecutionAdmitted = executionAdmitted;
        SlotDefinitions = Array.AsReadOnly(_slotDefinitions);
    }

    /// <summary>Exact canonical selection identity.</summary>
    public CapabilityRouteIdentity Identity { get; }

    /// <summary>Firmware-semantic identity of the resolved capability.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Whether the compiler admitted execution for this exact route.</summary>
    public bool ExecutionAdmitted { get; }

    /// <summary>Resolved input-slot definition references.</summary>
    public IReadOnlyList<AuthoringSlotDefinitionReference> SlotDefinitions { get; }
}

/// <summary>Immutable workflow-specific authoring catalog for one publication.</summary>
public sealed class AuthoringCapabilityCatalogSnapshot
{
    private readonly AuthoringCapabilityRoute[] _routes;
    private readonly string[] _icChoices;

    internal AuthoringCapabilityCatalogSnapshot(
        string workflowId,
        ResolutionToken resolutionToken,
        IEnumerable<AuthoringCapabilityRoute> routes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolutionToken.Value);
        ArgumentNullException.ThrowIfNull(routes);
        _routes = [.. routes];
        if (_routes.Any(static route => route is null) ||
            _routes.Any(route => !StringComparer.Ordinal.Equals(
                route.Identity.WorkflowId,
                workflowId)) ||
            _routes.Select(static route => route.Identity.RouteId)
                .Distinct(StringComparer.Ordinal).Count() != _routes.Length)
        {
            throw new ArgumentException(
                "Authoring catalog routes must be non-null, unique, and workflow-matched.",
                nameof(routes));
        }

        Array.Sort(_routes, CompareRoutes);
        _icChoices =
        [
            .. _routes.Select(static route => route.Identity.IcId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        WorkflowId = workflowId;
        ResolutionToken = resolutionToken;
        Routes = Array.AsReadOnly(_routes);
        IcChoices = Array.AsReadOnly(_icChoices);
    }

    /// <summary>Workflow owned by this catalog projection.</summary>
    public string WorkflowId { get; }

    /// <summary>Canonical publication identity shared by every route.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Exact authoring routes in stable selection order.</summary>
    public IReadOnlyList<AuthoringCapabilityRoute> Routes { get; }

    /// <summary>Distinct authoring IC choices in stable order.</summary>
    public IReadOnlyList<string> IcChoices { get; }

    /// <summary>
    /// Projects one canonical catalog without copying firmware ranges, metadata,
    /// or compiled operations.
    /// </summary>
    public static AuthoringCapabilityCatalogSnapshot FromCanonical(
        CanonicalCapabilityCatalogSnapshot snapshot,
        string workflowId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        AuthoringCapabilityRoute[] routes =
        [
            .. snapshot.Capabilities
                .Where(capability =>
                    StringComparer.Ordinal.Equals(
                        capability.Identity.WorkflowId,
                        workflowId) &&
                    capability.Authoring.Value ==
                        CapabilityAuthoringAvailability.Available)
                .Select(static capability =>
                {
                    CompiledInputContract inputContract = capability.CompiledComposition.V2Details?
                        .InputContract ?? throw new InvalidOperationException(
                            "Authoring routes require one canonical compiled V2 input contract.");
                    return new AuthoringCapabilityRoute(
                        capability.Identity,
                        capability.CapabilityFingerprint,
                        capability.ExecutionAdmitted,
                        inputContract.Slots.Select(static slot =>
                            new AuthoringSlotDefinitionReference(slot.SlotId)));
                }),
        ];
        return new AuthoringCapabilityCatalogSnapshot(
            workflowId,
            snapshot.ResolutionToken,
            routes);
    }

    internal IReadOnlyList<string> GetIcCountChoices(string icId)
    {
        return
        [
            .. _routes.Where(route =>
                    StringComparer.Ordinal.Equals(route.Identity.IcId, icId))
                .Select(static route => route.Identity.IcCountVariant)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    internal AuthoringCapabilityRoute[] FindRoutes(
        string icId,
        string icCountVariant)
    {
        return
        [
            .. _routes.Where(route =>
                StringComparer.Ordinal.Equals(route.Identity.IcId, icId) &&
                StringComparer.Ordinal.Equals(
                    route.Identity.IcCountVariant,
                    icCountVariant)),
        ];
    }

    private static int CompareRoutes(
        AuthoringCapabilityRoute left,
        AuthoringCapabilityRoute right)
    {
        int ic = StringComparer.Ordinal.Compare(
            left.Identity.IcId,
            right.Identity.IcId);
        if (ic != 0)
        {
            return ic;
        }

        int count = StringComparer.Ordinal.Compare(
            left.Identity.IcCountVariant,
            right.Identity.IcCountVariant);
        return count != 0
            ? count
            : StringComparer.Ordinal.Compare(
                left.Identity.MapVariant,
                right.Identity.MapVariant);
    }
}

/// <summary>Selected-file state for one resolved slot definition.</summary>
public sealed record AuthoringSlotState
{
    internal AuthoringSlotState(
        string definitionId,
        string? selectedPath,
        FileStamp? fileStamp,
        AuthoringSlotLifecycle lifecycle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        if ((selectedPath is null) != (fileStamp is null))
        {
            throw new ArgumentException(
                "Selected path and file stamp must be supplied or cleared together.",
                nameof(selectedPath));
        }

        if (!Enum.IsDefined(lifecycle) ||
            (selectedPath is null && lifecycle != AuthoringSlotLifecycle.Empty) ||
            (selectedPath is not null && lifecycle == AuthoringSlotLifecycle.Empty))
        {
            throw new ArgumentException(
                "Authoring slot lifecycle must match selected-file state.",
                nameof(lifecycle));
        }

        DefinitionId = definitionId;
        SelectedPath = selectedPath;
        FileStamp = fileStamp;
        Lifecycle = lifecycle;
    }

    /// <summary>Referenced canonical slot-definition identity.</summary>
    public string DefinitionId { get; }

    /// <summary>Caller-selected path, or null when empty.</summary>
    public string? SelectedPath { get; }

    /// <summary>Host-captured identity for the selected file.</summary>
    public FileStamp? FileStamp { get; }

    /// <summary>Current selected-file lifecycle.</summary>
    public AuthoringSlotLifecycle Lifecycle { get; }
}

/// <summary>One successfully published derived-result reference.</summary>
public sealed record AuthoringDerivedPublication
{
    /// <summary>Creates one payload-free derived result reference.</summary>
    public AuthoringDerivedPublication(
        AuthoringDerivedResultKind kind,
        string resultReference)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unknown authoring result kind.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(resultReference);
        Kind = kind;
        ResultReference = resultReference;
    }

    /// <summary>Closed result kind.</summary>
    public AuthoringDerivedResultKind Kind { get; }

    /// <summary>Opaque reference to the separately owned immutable result.</summary>
    public string ResultReference { get; }
}

/// <summary>
/// Immutable typed draft carried by one authoring session. Concrete draft
/// contracts own their fields; the session owns only lifetime and invalidation.
/// </summary>
public abstract record AuthoringDraftState
{
    /// <summary>Creates one typed draft with a stable closed-contract identity.</summary>
    protected AuthoringDraftState(string draftKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(draftKind);
        DraftKind = draftKind;
    }

    /// <summary>Stable identity of the concrete typed draft contract.</summary>
    public string DraftKind { get; }
}

/// <summary>Coherent immutable state consumed by UI or CLI adapters.</summary>
public sealed class ActiveSessionSnapshot
{
    private readonly string[] _icChoices;
    private readonly string[] _icCountChoices;
    private readonly AuthoringSlotState[] _slots;
    private readonly AuthoringDerivedPublication[] _derivedPublications;

    internal ActiveSessionSnapshot(
        string workflowId,
        ResolutionToken resolutionToken,
        AuthoringRevision authoringRevision,
        string selectedRouteId,
        string capabilityFingerprint,
        bool executionAdmitted,
        string selectedIc,
        string selectedIcCount,
        string selectedMapVariant,
        IEnumerable<string> icChoices,
        IEnumerable<string> icCountChoices,
        IEnumerable<AuthoringSlotState> slots,
        AuthoringDraftState? draftState,
        string? draftCapabilityFingerprint,
        IEnumerable<AuthoringDerivedPublication> derivedPublications)
    {
        WorkflowId = workflowId;
        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
        SelectedRouteId = selectedRouteId;
        CapabilityFingerprint = capabilityFingerprint;
        ExecutionAdmitted = executionAdmitted;
        SelectedIc = selectedIc;
        SelectedIcCount = selectedIcCount;
        SelectedMapVariant = selectedMapVariant;
        _icChoices = [.. icChoices];
        _icCountChoices = [.. icCountChoices];
        _slots = [.. slots];
        _derivedPublications = [.. derivedPublications];
        DraftState = draftState;
        DraftCapabilityFingerprint = draftCapabilityFingerprint;
        IcChoices = Array.AsReadOnly(_icChoices);
        IcCountChoices = Array.AsReadOnly(_icCountChoices);
        Slots = Array.AsReadOnly(_slots);
        DerivedPublications = Array.AsReadOnly(_derivedPublications);
    }

    /// <summary>Mode/workflow identity for this isolated session.</summary>
    public string WorkflowId { get; }

    /// <summary>Canonical catalog publication identity.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Current authoring-input revision.</summary>
    public AuthoringRevision AuthoringRevision { get; }

    /// <summary>Selected exact canonical route identity.</summary>
    public string SelectedRouteId { get; }

    /// <summary>Selected firmware-semantic identity.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Whether Build may proceed after remaining readiness checks.</summary>
    public bool ExecutionAdmitted { get; }

    /// <summary>Selected canonical IC.</summary>
    public string SelectedIc { get; }

    /// <summary>Selected IC Count variant.</summary>
    public string SelectedIcCount { get; }

    /// <summary>Resolved map variant retained for traceability, not user inference.</summary>
    public string SelectedMapVariant { get; }

    /// <summary>Current workflow IC choices.</summary>
    public IReadOnlyList<string> IcChoices { get; }

    /// <summary>IC Count choices for the selected IC.</summary>
    public IReadOnlyList<string> IcCountChoices { get; }

    /// <summary>Resolved slot states.</summary>
    public IReadOnlyList<AuthoringSlotState> Slots { get; }

    /// <summary>Current immutable typed draft, or null when this mode has none.</summary>
    public AuthoringDraftState? DraftState { get; }

    internal string? DraftCapabilityFingerprint { get; }

    /// <summary>Derived result references admitted for this exact snapshot.</summary>
    public IReadOnlyList<AuthoringDerivedPublication> DerivedPublications { get; }
}

/// <summary>Stable authoring-session issue.</summary>
public sealed record AuthoringSessionIssue(
    string Code,
    string Message,
    string? Subject = null);

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

    /// <summary>The asynchronous result belongs to older session state.</summary>
    public const string StalePublication = "authoring.session.publication-stale";

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

/// <summary>
/// Complete identity required before an asynchronous result may publish.
/// Carries no firmware bytes or derived result.
/// </summary>
public sealed class AuthoringPublicationLease
{
    private readonly AuthoringSlotPublicationIdentity[] _slots;

    internal AuthoringPublicationLease(
        object sessionIdentity,
        AuthoringDerivedResultKind kind,
        ResolutionToken resolutionToken,
        AuthoringRevision authoringRevision,
        string selectedRouteId,
        string capabilityFingerprint,
        IEnumerable<AuthoringSlotPublicationIdentity> slots)
    {
        ArgumentNullException.ThrowIfNull(sessionIdentity);
        SessionIdentity = sessionIdentity;
        Kind = kind;
        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
        SelectedRouteId = selectedRouteId;
        CapabilityFingerprint = capabilityFingerprint;
        _slots = [.. slots];
        Slots = Array.AsReadOnly(_slots);
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

    /// <summary>Captured slot definition, path, and file-stamp identities.</summary>
    public IReadOnlyList<AuthoringSlotPublicationIdentity> Slots { get; }
}

/// <summary>Typed result of one derived-result publication attempt.</summary>
public sealed record AuthoringPublicationResult(
    bool Succeeded,
    AuthoringSessionIssue? Issue);
