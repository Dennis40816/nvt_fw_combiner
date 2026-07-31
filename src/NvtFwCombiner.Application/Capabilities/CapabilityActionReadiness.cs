using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Independent admission dimensions bound to one exact resolved route.</summary>
public sealed record CapabilityAdmissionSnapshot
{
    /// <summary>Creates one immutable readiness input without recomputing any firmware fact.</summary>
    public CapabilityAdmissionSnapshot(
        string routeId,
        string capabilityFingerprint,
        ResolutionToken resolutionToken,
        AuthoringRevision authoringRevision,
        CapabilityAuthoringAvailability authoringAvailability,
        bool executionAdmitted,
        CapabilityEvidenceStatus evidenceStatus,
        CapabilityPublicationStatus publicationStatus,
        CapabilityActionBlocker? executionBlocker = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityFingerprint);
        if (!CapabilityRouteIdentity.IsSha256(capabilityFingerprint))
        {
            throw new ArgumentException(
                "Capability admission requires a SHA-256 capability fingerprint.",
                nameof(capabilityFingerprint));
        }

        if (string.IsNullOrWhiteSpace(resolutionToken.Value))
        {
            throw new ArgumentException(
                "Capability admission requires a non-empty resolution token.",
                nameof(resolutionToken));
        }

        if (!Enum.IsDefined(authoringAvailability) ||
            !Enum.IsDefined(evidenceStatus) ||
            !Enum.IsDefined(publicationStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(authoringAvailability),
                "Capability admission dimensions must use defined values.");
        }

        if ((executionAdmitted && executionBlocker is not null) ||
            (executionBlocker is not null &&
             executionBlocker.Dimension !=
             CapabilityReadinessDimension.Execution))
        {
            throw new ArgumentException(
                "Only a blocked execution admission may carry one execution blocker.",
                nameof(executionBlocker));
        }

        RouteId = routeId;
        CapabilityFingerprint = capabilityFingerprint;
        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
        AuthoringAvailability = authoringAvailability;
        ExecutionAdmitted = executionAdmitted;
        ExecutionBlocker = executionBlocker;
        EvidenceStatus = evidenceStatus;
        PublicationStatus = publicationStatus;
    }

    /// <summary>Stable exact-route identity.</summary>
    public string RouteId { get; }

    /// <summary>Firmware-semantic identity of the compiled capability.</summary>
    public string CapabilityFingerprint { get; }

    /// <summary>Catalog publication identity.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>Current authoring-input revision.</summary>
    public AuthoringRevision AuthoringRevision { get; }

    /// <summary>Shared UI/CLI authoring policy.</summary>
    public CapabilityAuthoringAvailability AuthoringAvailability { get; }

    /// <summary>Compiler-proved engine execution admission.</summary>
    public bool ExecutionAdmitted { get; }

    /// <summary>Optional exact execution blocker used by every action consumer.</summary>
    public CapabilityActionBlocker? ExecutionBlocker { get; }

    /// <summary>Independent certification evidence classification.</summary>
    public CapabilityEvidenceStatus EvidenceStatus { get; }

    /// <summary>Independent publication/support classification.</summary>
    public CapabilityPublicationStatus PublicationStatus { get; }

    /// <summary>Projects the independently owned dimensions from a resolved capability.</summary>
    public static CapabilityAdmissionSnapshot FromResolvedCapability(
        ResolvedCapability capability,
        AuthoringRevision authoringRevision)
    {
        ArgumentNullException.ThrowIfNull(capability);
        return new CapabilityAdmissionSnapshot(
            capability.Identity.RouteId,
            capability.CapabilityFingerprint,
            capability.ResolutionToken,
            authoringRevision,
            capability.Authoring.Value,
            capability.ExecutionAdmitted,
            capability.Evidence.Value,
            capability.Publication.Value);
    }

    /// <summary>
    /// Temporary one-way bridge for a compiled V2 route that has not yet moved
    /// into the canonical capability catalog. #194 deletes this bridge.
    /// </summary>
    public static CapabilityAdmissionSnapshot FromCompiledMigration(
        string routeId,
        ResolutionToken resolutionToken,
        AuthoringRevision authoringRevision,
        CapabilityAuthoringAvailability authoringAvailability,
        bool executionAdmitted,
        CompiledComposition compiledComposition)
    {
        ArgumentNullException.ThrowIfNull(compiledComposition);
        return new CapabilityAdmissionSnapshot(
            routeId,
            compiledComposition.CompilationFingerprint,
            resolutionToken,
            authoringRevision,
            authoringAvailability,
            executionAdmitted,
            CapabilityEvidenceStatus.Missing,
            CapabilityPublicationStatus.Internal);
    }
}

/// <summary>One required input/selection child evaluated for the current authoring revision.</summary>
public sealed record CapabilityChildReadiness
{
    /// <summary>Creates one child result using the closed shared readiness vocabulary.</summary>
    public CapabilityChildReadiness(
        string childId,
        ResolvedChildReadiness readiness,
        string? issueCode = null,
        string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childId);
        if (!Enum.IsDefined(readiness))
        {
            throw new ArgumentOutOfRangeException(
                nameof(readiness),
                readiness,
                "Capability children must use the closed readiness vocabulary.");
        }

        if ((issueCode is null) != (message is null) ||
            (readiness == ResolvedChildReadiness.Blocked &&
             (string.IsNullOrWhiteSpace(issueCode) || string.IsNullOrWhiteSpace(message))) ||
            (readiness != ResolvedChildReadiness.Blocked &&
             (issueCode is not null || message is not null)))
        {
            throw new ArgumentException(
                "Only blocked child readiness carries one complete typed issue.");
        }

        ChildId = childId;
        Readiness = readiness;
        IssueCode = issueCode;
        Message = message;
    }

    /// <summary>Stable child/slot/prerequisite identity.</summary>
    public string ChildId { get; }

    /// <summary>Current child readiness.</summary>
    public ResolvedChildReadiness Readiness { get; }

    /// <summary>Stable source issue when blocked.</summary>
    public string? IssueCode { get; }

    /// <summary>Operator-safe source detail when blocked.</summary>
    public string? Message { get; }
}

/// <summary>Readiness dimension that owns one check-time blocker.</summary>
public enum CapabilityReadinessDimension
{
    /// <summary>Exact-route shared authoring policy.</summary>
    Authoring,

    /// <summary>Compiler-proved execution admission.</summary>
    Execution,

    /// <summary>Current authoring inputs, selections, and validation.</summary>
    Input,

    /// <summary>Refreshable current-machine dependency state.</summary>
    RuntimeDependency,
}

/// <summary>Typed next action for a check-time blocker.</summary>
public enum CapabilityReadinessNextAction
{
    /// <summary>Select an authoring-available exact route.</summary>
    SelectAvailableRoute,

    /// <summary>Load the named required input.</summary>
    LoadRequiredInput,

    /// <summary>Correct or replace the named invalid input.</summary>
    CorrectInput,

    /// <summary>Review the compiled route rather than guessing execution authority.</summary>
    ReviewCompilation,

    /// <summary>Refresh current-machine processor/tool discovery.</summary>
    RefreshRuntimeDependencies,
}

/// <summary>One typed pre-run blocker; this is never a Build Report issue.</summary>
public sealed record CapabilityActionBlocker(
    string Code,
    CapabilityReadinessDimension Dimension,
    string SubjectId,
    string Message,
    CapabilityReadinessNextAction NextAction);

/// <summary>Stable codes consumed identically by UI and CLI adapters.</summary>
public static class CapabilityActionReadinessIssueCodes
{
    /// <summary>The shared authoring policy marks the exact route unavailable.</summary>
    public const string AuthoringUnavailable = "capability.readiness.authoring-unavailable";

    /// <summary>The compiler did not admit the resolved composition for execution.</summary>
    public const string ExecutionNotAdmitted = "capability.readiness.execution-not-admitted";

    /// <summary>Selected General Replace targets require a Parent stage that is absent.</summary>
    public const string PostbuildStageAuthorityMissing =
        "capability.readiness.postbuild-stage-authority-missing";

    /// <summary>A required user input or selection is absent.</summary>
    public const string InputPending = "capability.readiness.input-pending";

    /// <summary>A supplied input or selection violates its declared contract.</summary>
    public const string InputBlocked = "capability.readiness.input-blocked";

    /// <summary>The dependency snapshot belongs to another route revision/publication.</summary>
    public const string RuntimeSnapshotStale = "capability.readiness.runtime-snapshot-stale";

    /// <summary>A compiled processor/tool dependency is not ready.</summary>
    public const string RuntimeDependencyBlocked = "capability.readiness.runtime-dependency-blocked";
}

/// <summary>One action's deterministic check-time availability.</summary>
public sealed class CapabilityActionAvailability
{
    private readonly CapabilityActionBlocker[] _blockers;

    internal CapabilityActionAvailability(IEnumerable<RankedBlocker> blockers)
    {
        _blockers =
        [
            .. blockers
                .OrderBy(static blocker => blocker.Priority)
                .ThenBy(static blocker => blocker.Blocker.SubjectId, StringComparer.Ordinal)
                .ThenBy(static blocker => blocker.Blocker.Code, StringComparer.Ordinal)
                .Select(static blocker => blocker.Blocker),
        ];
        Blockers = Array.AsReadOnly(_blockers);
    }

    /// <summary>True when this action has no current check-time blocker.</summary>
    public bool IsAvailable => _blockers.Length == 0;

    /// <summary>Highest-priority blocker, or null when available.</summary>
    public CapabilityActionBlocker? PrimaryBlocker =>
        _blockers.Length == 0 ? null : _blockers[0];

    /// <summary>All blockers in stable display priority.</summary>
    public IReadOnlyList<CapabilityActionBlocker> Blockers { get; }
}

/// <summary>Shared Preview/Build projection for one exact route and authoring revision.</summary>
public sealed record CapabilityActionReadinessSnapshot(
    string RouteId,
    string CapabilityFingerprint,
    ResolutionToken ResolutionToken,
    AuthoringRevision AuthoringRevision,
    long RuntimeDependencyGeneration,
    CapabilityActionAvailability Preview,
    CapabilityActionAvailability Build);

/// <summary>Derives actions from independent canonical readiness dimensions.</summary>
public static class CapabilityActionReadinessResolver
{
    /// <summary>
    /// Produces check-time action state. Evidence/publication are intentionally
    /// not Build inputs, and this method never creates a run report.
    /// </summary>
    public static CapabilityActionReadinessSnapshot Resolve(
        CapabilityAdmissionSnapshot admission,
        IEnumerable<CapabilityChildReadiness> inputChildren,
        RuntimeDependencyReadinessSnapshot runtimeDependencies,
        long currentRuntimeDependencyGeneration)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(inputChildren);
        ArgumentNullException.ThrowIfNull(runtimeDependencies);
        ArgumentOutOfRangeException.ThrowIfLessThan(currentRuntimeDependencyGeneration, 1);
        CapabilityChildReadiness[] inputs = [.. inputChildren];
        if (inputs.Any(static input => input is null) ||
            inputs.Select(static input => input.ChildId)
                .Distinct(StringComparer.Ordinal).Count() != inputs.Length)
        {
            throw new ArgumentException(
                "Capability input readiness children must be non-null and unique.",
                nameof(inputChildren));
        }

        List<RankedBlocker> preview = [];
        AddAuthoringBlocker(admission, preview);
        AddPendingInputBlockers(inputs, preview);

        List<RankedBlocker> build = [];
        AddAuthoringBlocker(admission, build);
        AddExecutionBlocker(admission, build);
        AddInputBlockers(inputs, build);
        AddRuntimeBlockers(
            admission,
            runtimeDependencies,
            currentRuntimeDependencyGeneration,
            build);

        return new CapabilityActionReadinessSnapshot(
            admission.RouteId,
            admission.CapabilityFingerprint,
            admission.ResolutionToken,
            admission.AuthoringRevision,
            currentRuntimeDependencyGeneration,
            new CapabilityActionAvailability(preview),
            new CapabilityActionAvailability(build));
    }

    private static void AddAuthoringBlocker(
        CapabilityAdmissionSnapshot admission,
        List<RankedBlocker> blockers)
    {
        if (admission.AuthoringAvailability == CapabilityAuthoringAvailability.Available)
        {
            return;
        }

        blockers.Add(new RankedBlocker(
            0,
            new CapabilityActionBlocker(
                CapabilityActionReadinessIssueCodes.AuthoringUnavailable,
                CapabilityReadinessDimension.Authoring,
                admission.RouteId,
                "The selected route is unavailable for authoring.",
                CapabilityReadinessNextAction.SelectAvailableRoute)));
    }

    private static void AddExecutionBlocker(
        CapabilityAdmissionSnapshot admission,
        List<RankedBlocker> blockers)
    {
        if (admission.ExecutionAdmitted)
        {
            return;
        }

        blockers.Add(new RankedBlocker(
            1,
            admission.ExecutionBlocker ??
            new CapabilityActionBlocker(
                CapabilityActionReadinessIssueCodes.ExecutionNotAdmitted,
                CapabilityReadinessDimension.Execution,
                admission.RouteId,
                "The selected capability is not admitted for engine execution.",
                CapabilityReadinessNextAction.ReviewCompilation)));
    }

    private static void AddInputBlockers(
        IEnumerable<CapabilityChildReadiness> inputs,
        List<RankedBlocker> blockers)
    {
        foreach (CapabilityChildReadiness input in inputs)
        {
            switch (input.Readiness)
            {
                case ResolvedChildReadiness.Blocked:
                    blockers.Add(new RankedBlocker(
                        2,
                        new CapabilityActionBlocker(
                            CapabilityActionReadinessIssueCodes.InputBlocked,
                            CapabilityReadinessDimension.Input,
                            input.ChildId,
                            input.Message!,
                            CapabilityReadinessNextAction.CorrectInput)));
                    break;
                case ResolvedChildReadiness.PendingInput:
                    blockers.Add(new RankedBlocker(
                        3,
                        new CapabilityActionBlocker(
                            CapabilityActionReadinessIssueCodes.InputPending,
                            CapabilityReadinessDimension.Input,
                            input.ChildId,
                            "Load the required input before continuing.",
                            CapabilityReadinessNextAction.LoadRequiredInput)));
                    break;
                case ResolvedChildReadiness.NotApplicable:
                case ResolvedChildReadiness.Ready:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(inputs),
                        input.Readiness,
                        "Unknown child readiness.");
            }
        }
    }

    private static void AddPendingInputBlockers(
        IEnumerable<CapabilityChildReadiness> inputs,
        List<RankedBlocker> blockers)
    {
        foreach (CapabilityChildReadiness input in inputs.Where(static input =>
                     input.Readiness == ResolvedChildReadiness.PendingInput))
        {
            blockers.Add(new RankedBlocker(
                3,
                new CapabilityActionBlocker(
                    CapabilityActionReadinessIssueCodes.InputPending,
                    CapabilityReadinessDimension.Input,
                    input.ChildId,
                    "Load the required input before continuing.",
                    CapabilityReadinessNextAction.LoadRequiredInput)));
        }
    }

    private static void AddRuntimeBlockers(
        CapabilityAdmissionSnapshot admission,
        RuntimeDependencyReadinessSnapshot runtimeDependencies,
        long currentRuntimeDependencyGeneration,
        List<RankedBlocker> blockers)
    {
        if (!runtimeDependencies.Matches(
                admission,
                currentRuntimeDependencyGeneration))
        {
            blockers.Add(new RankedBlocker(
                4,
                new CapabilityActionBlocker(
                    CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale,
                    CapabilityReadinessDimension.RuntimeDependency,
                    admission.RouteId,
                    "Runtime dependency status is stale for the selected capability.",
                    CapabilityReadinessNextAction.RefreshRuntimeDependencies)));
            return;
        }

        foreach (RuntimeDependencyEntry entry in runtimeDependencies.Entries)
        {
            if (entry.Readiness is ResolvedChildReadiness.Ready or
                ResolvedChildReadiness.NotApplicable)
            {
                continue;
            }

            blockers.Add(new RankedBlocker(
                5,
                new CapabilityActionBlocker(
                    CapabilityActionReadinessIssueCodes.RuntimeDependencyBlocked,
                    CapabilityReadinessDimension.RuntimeDependency,
                    $"{entry.ProcessorId}:{entry.ToolBindingId}",
                    entry.Message ?? "Refresh the required runtime dependency.",
                    CapabilityReadinessNextAction.RefreshRuntimeDependencies)));
        }
    }
}

internal sealed record RankedBlocker(int Priority, CapabilityActionBlocker Blocker);
