using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

#pragma warning disable CS1591 // Infrastructure adapter contracts are not end-user API.

namespace NvtFwCombiner.Application.Authoring;

public interface IGeneralAuthoringPlanner
{
    bool CanPlanGeneralReplace(string icId);

    GeneralAuthoringAdmissionResult GetGeneralMergeAdmission(
        string icId,
        GeneralMergeDraftState draft);

    GeneralAuthoringAdmissionResult? GetGeneralReplaceAdmission(
        string icId,
        long referenceCapacity,
        GeneralMappingDraftState mappingDraft);

    GeneralMergeOutputInitializer GetGeneralMergeDefaultOutputInitializer(string icId);

    GeneralMergeAuthoringPlanResult PlanGeneralMerge(
        string icId,
        GeneralMergeDraftState draft,
        IReadOnlyDictionary<string, long> observedFileLengths,
        ResolvedCapability? retainedCapability);

    GeneralReplaceAuthoringPlanResult PlanGeneralReplace(
        string icId,
        string number,
        GeneralMappingDraftState draft,
        ReadOnlyMemory<byte> referenceBytes,
        IReadOnlyDictionary<string, long> observedFileLengths,
        ResolvedCapability? retainedCapability);
}

public interface IRuntimeDependencyReadinessLeaseProvider
{
    RuntimeDependencyReadinessLease AcquireCurrent();
}

public sealed record RuntimeDependencyReadinessLease(
    IRuntimeDependencyReadinessProvider ReadinessProvider,
    long Generation,
    Func<long, bool> GenerationIsCurrent);

public sealed record GeneralReplaceRuntimeAuthority(
    SavedRuleParentIdentity ParentBinding,
    IReadOnlyList<string> ProcessorStageIds,
    IReadOnlyList<ExternalProcessorDependencyReference> RuntimeDependencies);

public sealed record GeneralMergeAuthoringPlan(
    GeneralMappingDraftState MappingDraft,
    ResolvedCapability Capability,
    IReadOnlyList<GeneralInputResource> InputResources);

public sealed record GeneralMergeAuthoringPlanResult(
    GeneralMergeAuthoringPlan? Plan,
    GeneralAuthoringAdmissionResult? Admission,
    IReadOnlyList<CompositionIssue> Issues);

public sealed record GeneralReplaceAuthoringPlan(
    GeneralMappingDraftState MappingDraft,
    ResolvedCapability Capability,
    GeneralAuthoringAdmissionResult Admission,
    GeneralReplaceRuntimeAuthority RuntimeAuthority,
    bool RequiresPostbuild,
    CompositionIssue? PlanningIssue,
    long ReferenceCapacity);

public sealed record GeneralReplaceAuthoringPlanResult(
    GeneralReplaceAuthoringPlan? Plan,
    GeneralAuthoringAdmissionResult? Admission,
    IReadOnlyList<CompositionIssue> Issues);
