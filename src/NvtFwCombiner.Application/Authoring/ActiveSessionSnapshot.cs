using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>Coherent immutable state consumed by UI or CLI adapters.</summary>
public sealed class ActiveSessionSnapshot
{
    private readonly string[] _icChoices;
    private readonly string[] _icCountChoices;
    private readonly AuthoringSlotState[] _slots;
    private readonly AuthoringDerivedPublication[] _derivedPublications;
    private readonly AuthoringInputSlotStatus[] _inputSlotStatuses;

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
        IEnumerable<AuthoringDerivedPublication> derivedPublications,
        string? compilationFingerprint = null,
        IEnumerable<AuthoringInputSlotStatus>? inputSlotStatuses = null)
    {
        WorkflowId = workflowId;
        ResolutionToken = resolutionToken;
        AuthoringRevision = authoringRevision;
        SelectedRouteId = selectedRouteId;
        CapabilityFingerprint = capabilityFingerprint;
        CompilationFingerprint = compilationFingerprint;
        ExecutionAdmitted = executionAdmitted;
        SelectedIc = selectedIc;
        SelectedIcCount = selectedIcCount;
        SelectedMapVariant = selectedMapVariant;
        _icChoices = [.. icChoices];
        _icCountChoices = [.. icCountChoices];
        _slots = [.. slots];
        _derivedPublications = [.. derivedPublications];
        _inputSlotStatuses = [.. inputSlotStatuses ?? []];
        if (_inputSlotStatuses.Select(static status => status.SlotId)
                .Distinct(StringComparer.Ordinal).Count() != _inputSlotStatuses.Length)
        {
            throw new ArgumentException(
                "Session input-slot statuses must be uniquely identified.",
                nameof(inputSlotStatuses));
        }

        DraftState = draftState;
        DraftCapabilityFingerprint = draftCapabilityFingerprint;
        IcChoices = Array.AsReadOnly(_icChoices);
        IcCountChoices = Array.AsReadOnly(_icCountChoices);
        Slots = Array.AsReadOnly(_slots);
        DerivedPublications = Array.AsReadOnly(_derivedPublications);
        InputSlotStatuses = Array.AsReadOnly(_inputSlotStatuses);
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

    /// <summary>Exact compiled-composition identity for the active slot definitions.</summary>
    public string? CompilationFingerprint { get; }

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

    /// <summary>Complete per-slot readiness and terminal health for the current inspection batch.</summary>
    public IReadOnlyList<AuthoringInputSlotStatus> InputSlotStatuses { get; }
}
