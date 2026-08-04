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

/// <summary>
/// Opaque reference to one issue owned by an immutable inspection or validation result.
/// Carries no duplicated diagnostic text or firmware fact.
/// </summary>
public sealed record AuthoringSlotIssueReference
{
    /// <summary>Creates one reference to an issue inside a separately owned result.</summary>
    public AuthoringSlotIssueReference(
        AuthoringDerivedResultKind resultKind,
        string resultReference,
        string issueId)
    {
        if (resultKind is not (
            AuthoringDerivedResultKind.Inspection or
            AuthoringDerivedResultKind.Validation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resultKind),
                resultKind,
                "Slot issues must be owned by an inspection or validation result.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(resultReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(issueId);
        ResultKind = resultKind;
        ResultReference = resultReference;
        IssueId = issueId;
    }

    /// <summary>Kind of immutable result that owns the issue.</summary>
    public AuthoringDerivedResultKind ResultKind { get; }

    /// <summary>Opaque reference to the separately owned immutable result.</summary>
    public string ResultReference { get; }

    /// <summary>Stable issue identity inside the referenced result.</summary>
    public string IssueId { get; }
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

/// <summary>Reference to one canonical resolved input-slot definition.</summary>
public sealed record AuthoringSlotDefinitionReference
{
    internal AuthoringSlotDefinitionReference(
        string definitionId,
        long? expectedLength = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        if (expectedLength is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedLength.Value);
        }
        DefinitionId = definitionId;
        ExpectedLength = expectedLength;
    }

    /// <summary>Stable slot-definition identity from the resolved input contract.</summary>
    public string DefinitionId { get; }

    /// <summary>Exact pre-binding length compiled for this slot, when required.</summary>
    public long? ExpectedLength { get; }
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
        IEnumerable<AuthoringSlotDefinitionReference> slotDefinitions,
        string? compilationFingerprint = null,
        ReviewedDiscoveryTransition? discoveryTransition = null,
        ResolvedCapability? exactCapability = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityFingerprint);
        ArgumentNullException.ThrowIfNull(slotDefinitions);
        if (compilationFingerprint is not null &&
            !CapabilityRouteIdentity.IsSha256(compilationFingerprint))
        {
            throw new ArgumentException(
                "Compilation fingerprint must be a lowercase SHA-256 value.",
                nameof(compilationFingerprint));
        }
        if (exactCapability is not null &&
            (!Equals(exactCapability.Identity, identity) ||
             !StringComparer.Ordinal.Equals(
                 exactCapability.CapabilityFingerprint,
                 capabilityFingerprint) ||
             !StringComparer.Ordinal.Equals(
                 exactCapability.CompiledComposition.CompilationFingerprint,
                 compilationFingerprint)))
        {
            throw new ArgumentException(
                "The retained exact capability must own this route and compilation.",
                nameof(exactCapability));
        }
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
        CompilationFingerprint = compilationFingerprint;
        DiscoveryTransition = discoveryTransition;
        ExactCapability = exactCapability;
        ExecutionAdmitted = executionAdmitted;
        SlotDefinitions = Array.AsReadOnly(_slotDefinitions);
    }

    /// <summary>Exact canonical selection identity.</summary>
    public CapabilityRouteIdentity Identity { get; }

    /// <summary>Reviewed capability-definition fingerprint of the resolved route.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Exact compiled-composition identity for this authoring projection.</summary>
    public string? CompilationFingerprint { get; }

    /// <summary>Reviewed discovery-to-exact transition proof, when compilation needs a prerequisite.</summary>
    public ReviewedDiscoveryTransition? DiscoveryTransition { get; }

    /// <summary>Exact immutable capability retained for this compiled route.</summary>
    public ResolvedCapability? ExactCapability { get; }

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
                    CompiledInputContract inputContract =
                        capability.CompiledComposition.V2Details.InputContract;
                    return new AuthoringCapabilityRoute(
                        capability.Identity,
                        capability.CapabilityFingerprint,
                        capability.ExecutionAdmitted,
                        inputContract.SpaceBindings
                            .Select(static binding => new AuthoringSlotDefinitionReference(
                                binding.InstancePolicy == CompiledInputInstancePolicy.PerBinding
                                    ? binding.AddressSpaceId
                                    : binding.SlotId))
                            .DistinctBy(static definition => definition.DefinitionId),
                        capability.CompiledComposition.CompilationFingerprint,
                        exactCapability: capability);
                }),
        ];
        return new AuthoringCapabilityCatalogSnapshot(
            workflowId,
            snapshot.ResolutionToken,
            routes);
    }

    /// <summary>Projects one exact per-authoring compilation into a single-route session catalog.</summary>
    public static AuthoringCapabilityCatalogSnapshot FromResolvedCapability(
        ResolvedCapability capability,
        ReviewedDiscoveryTransition? discoveryTransition = null)
    {
        ArgumentNullException.ThrowIfNull(capability);
        CompiledInputContract inputContract =
            capability.CompiledComposition.V2Details.InputContract;
        return CreateSingleRouteCatalog(
            capability.Identity,
            capability.ResolutionToken,
            capability.CapabilityFingerprint,
            capability.ExecutionAdmitted,
            inputContract.SpaceBindings.Select(static binding =>
                new AuthoringSlotDefinitionReference(
                    binding.InstancePolicy == CompiledInputInstancePolicy.PerBinding
                        ? binding.AddressSpaceId
                        : binding.SlotId)).DistinctBy(static definition => definition.DefinitionId),
            capability.CompiledComposition.CompilationFingerprint,
            discoveryTransition,
            capability);
    }

    /// <summary>Projects an exact capability with request-scoped General binding identities.</summary>
    public static AuthoringCapabilityCatalogSnapshot FromResolvedCapability(
        ResolvedCapability capability,
        IReadOnlyDictionary<string, long> slotLengths)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(slotLengths);
        return CreateSingleRouteCatalog(
            capability.Identity,
            capability.ResolutionToken,
            capability.CapabilityFingerprint,
            capability.ExecutionAdmitted,
            slotLengths.Select(static item =>
                new AuthoringSlotDefinitionReference(item.Key, item.Value)),
            capability.CompiledComposition.CompilationFingerprint,
            exactCapability: capability);
    }

    /// <summary>
    /// Projects reviewed pre-compilation membership without claiming one exact
    /// compiled composition.
    /// </summary>
    public static AuthoringCapabilityCatalogSnapshot FromDiscovery(
        ResolvedCapability discoveryCapability,
        IEnumerable<string> slotDefinitionIds,
        ReviewedDiscoveryTransition? discoveryTransition = null)
    {
        ArgumentNullException.ThrowIfNull(discoveryCapability);
        ArgumentNullException.ThrowIfNull(slotDefinitionIds);
        return CreateSingleRouteCatalog(
            discoveryCapability.Identity,
            discoveryCapability.ResolutionToken,
            discoveryCapability.CapabilityFingerprint,
            discoveryCapability.ExecutionAdmitted,
            slotDefinitionIds.Select(static slotId =>
                new AuthoringSlotDefinitionReference(slotId)),
            compilationFingerprint: null,
            discoveryTransition);
    }

    /// <summary>Projects reviewed dynamic-route membership before exact compilation.</summary>
    public static AuthoringCapabilityCatalogSnapshot FromDynamicRoute(
        ResolvedCapabilityRoute route,
        IEnumerable<string> slotDefinitionIds)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(slotDefinitionIds);
        return CreateSingleRouteCatalog(
            route.Identity,
            route.ResolutionToken,
            route.CapabilityFingerprint,
            executionAdmitted: false,
            slotDefinitionIds.Select(static slotId =>
                new AuthoringSlotDefinitionReference(slotId)));
    }

    private static AuthoringCapabilityCatalogSnapshot CreateSingleRouteCatalog(
        CapabilityRouteIdentity identity,
        ResolutionToken resolutionToken,
        string capabilityFingerprint,
        bool executionAdmitted,
        IEnumerable<AuthoringSlotDefinitionReference> slots,
        string? compilationFingerprint = null,
        ReviewedDiscoveryTransition? discoveryTransition = null,
        ResolvedCapability? exactCapability = null)
    {
        return new(identity.WorkflowId, resolutionToken, [new AuthoringCapabilityRoute(
                identity,
                capabilityFingerprint,
                executionAdmitted,
                slots,
                compilationFingerprint,
                discoveryTransition,
                exactCapability)]);
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
        AuthoringSlotLifecycle lifecycle,
        AuthoringSlotIssueReference? blockingIssue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        if (selectedPath is null && fileStamp is not null)
        {
            throw new ArgumentException(
                "A file stamp cannot exist without a selected path.",
                nameof(fileStamp));
        }

        if (!Enum.IsDefined(lifecycle) ||
            (selectedPath is null && lifecycle != AuthoringSlotLifecycle.Empty) ||
            (selectedPath is not null && lifecycle == AuthoringSlotLifecycle.Empty) ||
            (lifecycle is AuthoringSlotLifecycle.Verified or
                AuthoringSlotLifecycle.Warning && fileStamp is null))
        {
            throw new ArgumentException(
                "Authoring slot lifecycle must match selected-file state.",
                nameof(lifecycle));
        }

        bool hasBlockingIssue = blockingIssue is not null;
        if (hasBlockingIssue != (lifecycle == AuthoringSlotLifecycle.Error))
        {
            throw new ArgumentException(
                "Only an error lifecycle requires one blocking inspection or validation issue reference.",
                nameof(blockingIssue));
        }

        DefinitionId = definitionId;
        SelectedPath = selectedPath;
        FileStamp = fileStamp;
        Lifecycle = lifecycle;
        BlockingIssue = blockingIssue;
    }

    /// <summary>Referenced canonical slot-definition identity.</summary>
    public string DefinitionId { get; }

    /// <summary>Caller-selected path, or null when empty.</summary>
    public string? SelectedPath { get; }

    /// <summary>Host-captured identity for the selected file.</summary>
    public FileStamp? FileStamp { get; }

    /// <summary>Current selected-file lifecycle.</summary>
    public AuthoringSlotLifecycle Lifecycle { get; }

    /// <summary>Actual blocking issue reference, present only for an error lifecycle.</summary>
    public AuthoringSlotIssueReference? BlockingIssue { get; }
}

/// <summary>One successfully published derived-result reference.</summary>
public sealed record AuthoringDerivedPublication
{
    /// <summary>Creates one payload-free derived result reference.</summary>
    public AuthoringDerivedPublication(
        AuthoringDerivedResultKind kind,
        string resultReference,
        string? compilationFingerprint = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unknown authoring result kind.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(resultReference);
        if (compilationFingerprint is not null &&
            !CapabilityRouteIdentity.IsSha256(compilationFingerprint))
        {
            throw new ArgumentException(
                "Compilation fingerprint must be a lowercase SHA-256 value.",
                nameof(compilationFingerprint));
        }
        Kind = kind;
        ResultReference = resultReference;
        CompilationFingerprint = compilationFingerprint;
    }

    /// <summary>Closed result kind.</summary>
    public AuthoringDerivedResultKind Kind { get; }

    /// <summary>Opaque reference to the separately owned immutable result.</summary>
    public string ResultReference { get; }

    /// <summary>
    /// Exact compiled-composition identity for a compilation-bound result, or
    /// null when the result kind has no compiled projection.
    /// </summary>
    public string? CompilationFingerprint { get; }

}
