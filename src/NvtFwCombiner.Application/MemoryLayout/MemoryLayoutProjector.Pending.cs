using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.MemoryLayout;

/// <summary>Unresolved workflow prerequisite, independent of display language.</summary>
public enum MemoryPendingPrerequisite
{
    /// <summary>DP source required.</summary>
    DpBin,
    /// <summary>Reference source required.</summary>
    BaseBin,
    /// <summary>At least one CtrlRAM replacement required.</summary>
    CtrlRamReplacement,
    /// <summary>General source and explicit mapping required.</summary>
    GeneralMergeSourceMapping,
}

/// <summary>Selected-input availability reported by the inspection request lifecycle.</summary>
public enum MemoryInputAvailabilityIssue
{
    /// <summary>No availability failure was reported.</summary>
    None,
    /// <summary>The selected content or its publication changed during inspection.</summary>
    ContentChanged,
    /// <summary>Inspection ended without publishing a usable result.</summary>
    InspectionFailed,
}

/// <summary>Current selection request and declared binding, carrying the original typed inspection.</summary>
public sealed record MemoryLayoutPendingInput(
    string SlotId, string? AddressSpaceId, bool Required, string? SelectedPath,
    AuthoringInputSlotStatus? Inspection = null, string? CompiledSlotId = null,
    MemoryInputAvailabilityIssue AvailabilityIssue = MemoryInputAvailabilityIssue.None, IReadOnlyList<CompositionIssue>? AuthoringIssues = null);

/// <summary>Non-geometric terminal display decision before an exact capability exists.</summary>
public sealed record MemoryLayoutPendingDisplay(
    string? SlotId, MemoryArtifactKind ArtifactKind, MemoryPendingPrerequisite FallbackPrerequisite,
    MemoryLayoutReadiness Readiness, MemoryLayoutNextAction NextAction, MemoryDiagnosticSeverity Severity,
    AuthoringInputSlotStatus? Inspection = null, MemoryInputAvailabilityIssue AvailabilityIssue = MemoryInputAvailabilityIssue.None,
    IReadOnlyList<CompositionIssue>? AuthoringIssues = null)
{
    /// <summary>Typed plan-row action; this cannot admit execution.</summary>
    public MemoryPlanActionKind Action => Readiness == MemoryLayoutReadiness.Blocked
        ? MemoryPlanActionKind.Blocked : MemoryPlanActionKind.Browse;
}

public static partial class MemoryLayoutProjector
{
    /// <summary>Projects missing prerequisites without geometry, compilation or Presentation readiness flags.</summary>
    public static MemoryLayoutPendingDisplay ProjectPending(
        ActiveSessionSnapshot? authoring, IEnumerable<MemoryLayoutPendingInput> inputs,
        MemoryPendingPrerequisite fallbackPrerequisite, IReadOnlyList<string>? requiredAddressSpaces = null)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        MemoryLayoutGuard.Defined(fallbackPrerequisite, nameof(fallbackPrerequisite));
        (MemoryLayoutPendingInput Input, MemoryArtifactKind Kind, AuthoringSlotLifecycle Lifecycle)[] candidates = [.. inputs.Select(input =>
        {
            MemoryLayoutGuard.Defined(input.AvailabilityIssue, nameof(inputs));
            AuthoringSlotState? state = authoring?.Slots.FirstOrDefault(slot =>
                slot.DefinitionId == (input.CompiledSlotId ?? input.SlotId) && slot.SelectedPath == input.SelectedPath);
            AuthoringInputSlotStatus? inspection = input.Inspection;
            bool currentInspection = inspection is not null && inspection.AddressSpaceId == input.AddressSpaceId &&
                (inspection.SelectedPathHint is null || inspection.SelectedPathHint == input.SelectedPath);
            AuthoringSlotLifecycle lifecycle = input.SelectedPath is null ? AuthoringSlotLifecycle.Empty
                : input.AvailabilityIssue != MemoryInputAvailabilityIssue.None || input.AuthoringIssues is { Count: > 0 } ||
                    (currentInspection && inspection!.Readiness == ResolvedChildReadiness.Blocked) ? AuthoringSlotLifecycle.Error
                : currentInspection && inspection!.InspectionLifecycle is { } health ? health
                : state?.Lifecycle ?? AuthoringSlotLifecycle.Selected;
            return (Input: input, Kind: GetArtifactKind(input.AddressSpaceId), Lifecycle: lifecycle);
        })];
        (MemoryLayoutPendingInput Input, MemoryArtifactKind Kind, AuthoringSlotLifecycle Lifecycle) pending = candidates.FirstOrDefault(static item => item.Kind == MemoryArtifactKind.Reference && item.Lifecycle == AuthoringSlotLifecycle.Empty);
        if (pending.Input is null) { pending = candidates.FirstOrDefault(static item => item.Lifecycle == AuthoringSlotLifecycle.Error); }
        if (pending.Input is null)
        {
            pending = candidates.FirstOrDefault(item =>
                (requiredAddressSpaces?.Contains(item.Input.AddressSpaceId, StringComparer.Ordinal) ?? item.Input.Required) && item.Lifecycle == AuthoringSlotLifecycle.Empty);
        }
        if (pending.Input is null)
        {
            return new(null, MemoryArtifactKind.Other, fallbackPrerequisite,
                MemoryLayoutReadiness.PendingInput, MemoryLayoutNextAction.SelectInput, MemoryDiagnosticSeverity.Information);
        }
        (MemoryLayoutReadiness Readiness, MemoryLayoutPrerequisite Prerequisite, MemoryLayoutNextAction NextAction, MemoryDiagnosticSeverity Severity) = PendingLifecycle(pending.Lifecycle);
        AuthoringInputSlotStatus? diagnostic = pending.Input.Inspection;
        if (diagnostic?.AddressSpaceId != pending.Input.AddressSpaceId ||
            (diagnostic?.SelectedPathHint is { } path && path != pending.Input.SelectedPath)) { diagnostic = null; }
        return new(pending.Input.SlotId, pending.Kind, fallbackPrerequisite, Readiness, NextAction, Severity,
            diagnostic, pending.Input.AvailabilityIssue, Array.AsReadOnly([.. pending.Input.AuthoringIssues ?? []]));
    }

    private static (MemoryLayoutReadiness Readiness, MemoryLayoutPrerequisite Prerequisite,
        MemoryLayoutNextAction NextAction, MemoryDiagnosticSeverity Severity) PendingLifecycle(AuthoringSlotLifecycle lifecycle)
    {
        return lifecycle switch
        {
            AuthoringSlotLifecycle.Empty => (MemoryLayoutReadiness.PendingInput, MemoryLayoutPrerequisite.SelectInput,
                MemoryLayoutNextAction.SelectInput, MemoryDiagnosticSeverity.Information),
            AuthoringSlotLifecycle.Selected => (MemoryLayoutReadiness.PendingInput, MemoryLayoutPrerequisite.CompleteInspection,
                MemoryLayoutNextAction.RunInspection, MemoryDiagnosticSeverity.Information),
            AuthoringSlotLifecycle.Checking => (MemoryLayoutReadiness.PendingInput, MemoryLayoutPrerequisite.CompleteInspection,
                MemoryLayoutNextAction.WaitForInspection, MemoryDiagnosticSeverity.Information),
            AuthoringSlotLifecycle.Error => (MemoryLayoutReadiness.Blocked, MemoryLayoutPrerequisite.ResolveInputIssue,
                MemoryLayoutNextAction.ReviewInputIssue, MemoryDiagnosticSeverity.Error),
            AuthoringSlotLifecycle.Verified or AuthoringSlotLifecycle.Warning => throw new InvalidOperationException("Admitted inputs have no pending display facts."),
            _ => throw new InvalidOperationException("Unknown input lifecycle."),
        };
    }
}
