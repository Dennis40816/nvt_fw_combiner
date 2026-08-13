using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

internal interface IGeneralAuthoringPlanner
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

internal interface IRuntimeDependencyReadinessLeaseProvider
{
    RuntimeDependencyReadinessLease AcquireCurrent();
}

internal sealed record RuntimeDependencyReadinessLease(
    IRuntimeDependencyReadinessProvider ReadinessProvider,
    long Generation,
    Func<long, bool> GenerationIsCurrent);

internal sealed record GeneralReplaceRuntimeAuthority(
    SavedRuleParentIdentity ParentBinding,
    IReadOnlyList<string> ProcessorStageIds,
    IReadOnlyList<ExternalProcessorDependencyReference> RuntimeDependencies);

internal sealed record GeneralMergeAuthoringPlan(
    GeneralMappingDraftState MappingDraft,
    ResolvedCapability Capability,
    IReadOnlyList<GeneralInputResource> InputResources);

internal sealed record GeneralMergeAuthoringPlanResult(
    GeneralMergeAuthoringPlan? Plan,
    GeneralAuthoringAdmissionResult? Admission,
    IReadOnlyList<CompositionIssue> Issues);

internal sealed record GeneralReplaceAuthoringPlan(
    GeneralMappingDraftState MappingDraft,
    ResolvedCapability Capability,
    GeneralAuthoringAdmissionResult Admission,
    GeneralReplaceRuntimeAuthority RuntimeAuthority,
    bool RequiresPostbuild,
    CompositionIssue? PlanningIssue,
    long ReferenceCapacity);

internal sealed record GeneralReplaceAuthoringPlanResult(
    GeneralReplaceAuthoringPlan? Plan,
    GeneralAuthoringAdmissionResult? Admission,
    IReadOnlyList<CompositionIssue> Issues);
