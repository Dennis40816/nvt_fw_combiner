using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Immutable CtrlRAM selections and advisory evidence retained by one exact compilation.</summary>
public sealed class AcceptedCtrlRamExecutionPlan
{
    private readonly CompositionIssue[] _advisoryIssues;

    /// <summary>Creates one accepted execution plan without retaining host paths or mutable bytes.</summary>
    public AcceptedCtrlRamExecutionPlan(
        IcNumberSelection icNumberSelection,
        CtrlRamAuthoringDraftState? firmwareVersionDraft,
        IEnumerable<CompositionIssue> advisoryIssues)
    {
        ArgumentNullException.ThrowIfNull(icNumberSelection);
        ArgumentNullException.ThrowIfNull(advisoryIssues);
        _advisoryIssues = [.. advisoryIssues];
        if (_advisoryIssues.Any(static issue => issue is null))
        {
            throw new ArgumentException(
                "CtrlRAM advisory evidence cannot contain null entries.",
                nameof(advisoryIssues));
        }

        IcNumberSelection = icNumberSelection;
        Draft = firmwareVersionDraft;
        AdvisoryIssues = Array.AsReadOnly(_advisoryIssues);
    }

    /// <summary>Exact IC-number selection compiled into the runtime-reference plan.</summary>
    public IcNumberSelection IcNumberSelection { get; }

    /// <summary>Optional firmware-version edit compiled into this exact plan.</summary>
    public CtrlRamAuthoringDraftState? Draft { get; }

    /// <summary>Legacy Standard-only version projection; AB callers use the complete <see cref="Draft"/>.</summary>
    public CtrlRamFirmwareVersionDraftState? FirmwareVersionDraft => Draft as CtrlRamFirmwareVersionDraftState;

    /// <summary>Accepted non-blocking input observations reported by execution.</summary>
    public IReadOnlyList<CompositionIssue> AdvisoryIssues { get; }
}
