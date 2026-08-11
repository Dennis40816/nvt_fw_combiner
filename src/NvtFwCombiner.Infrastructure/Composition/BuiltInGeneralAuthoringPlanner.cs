using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Infrastructure.Composition;

internal sealed partial class BuiltInGeneralAuthoringPlanner(
    ICanonicalCapabilityQuery catalog,
    CanonicalCapabilityCompilerAdapter compiler,
    CanonicalCapabilityExperience projection) : IGeneralAuthoringPlanner
{
    private readonly ICanonicalCapabilityQuery _catalog =
        catalog ?? throw new ArgumentNullException(nameof(catalog));
    private readonly CanonicalCapabilityCompilerAdapter _compiler =
        compiler ?? throw new ArgumentNullException(nameof(compiler));
    private readonly CanonicalCapabilityExperience _projection =
        projection ?? throw new ArgumentNullException(nameof(projection));

    public bool CanPlanGeneralReplace(string icId)
    {
        return BuiltInV2RegistrationRegistry.GeneralReplaceByIc.ContainsKey(
            IcIdentifier.Normalize(icId));
    }

    public GeneralAuthoringAdmissionResult GetGeneralMergeAdmission(
        string icId,
        GeneralMergeDraftState draft)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(draft);
        string parentId = BuiltInV2RegistrationRegistry.GeneralMergeByIc.TryGetValue(
                icId,
                out GeneralMergeV2CandidateRegistration? registration)
            ? registration.ProfileId
            : $"{icId}:general-merge-unavailable";
        return GeneralAuthoringAdmissionUseCase.Resolve(
            draft.Mappings,
            draft.OutputInitializer.Capacity,
            GeneralAuthoringAdmissionUseCase.CreateTrustedParentPolicy(
                parentId,
                draft.Mappings));
    }

    public GeneralAuthoringAdmissionResult? GetGeneralReplaceAdmission(
        string icId,
        long referenceCapacity,
        GeneralMappingDraftState mappingDraft)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(referenceCapacity);
        ArgumentNullException.ThrowIfNull(mappingDraft);
        return !BuiltInV2RegistrationRegistry.GeneralReplaceByIc.TryGetValue(
                IcIdentifier.Normalize(icId),
                out GeneralReplaceV2Registration? registration)
            ? null
            : GeneralAuthoringAdmissionUseCase.Resolve(
                mappingDraft,
                referenceCapacity,
                GeneralAuthoringAdmissionUseCase.CreateTrustedParentPolicy(
                    registration.ExactParent.Admission.ParentBinding.ProfileId,
                    mappingDraft,
                    registration.ExactParent.Admission.ParentBinding));
    }

    public GeneralMergeOutputInitializer GetGeneralMergeDefaultOutputInitializer(
        string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        _ = _projection.FindStandardMergeProfileSummary(icId) ??
            throw new InvalidOperationException(
                $"No compiled V2 Standard Merge profile is registered for '{icId}'.");
        bool hasDefaultCapacity =
            BuiltInV2StandardMergeDiscovery.TryGetBuiltInV2StandardMergeAuthoringDefaultCapacity(
                icId,
                out long capacity,
                out IReadOnlyList<CompositionIssue> capacityIssues);
        return hasDefaultCapacity
            ? new GeneralMergeOutputInitializer(capacity)
            : capacityIssues.Count != 0
            ? throw new InvalidOperationException(FormatIssues(capacityIssues))
            : _compiler.TryCompileStandardMerge(
                icId,
                dpInputLength: null,
                out CompiledComposition? composition,
                out IReadOnlyList<CompositionIssue> issues)
            ? new GeneralMergeOutputInitializer(
                composition.Plan.OutputInitialization.Capacity)
            : throw new InvalidOperationException(FormatIssues(issues));
    }

    public GeneralMergeAuthoringPlanResult PlanGeneralMerge(
        string icId,
        GeneralMergeDraftState draft,
        IReadOnlyDictionary<string, long> observedFileLengths,
        ResolvedCapability? retainedCapability)
    {
        if (!BuiltInV2RegistrationRegistry.GeneralMergeByIc.TryGetValue(
                icId,
                out GeneralMergeV2CandidateRegistration? registration))
        {
            return new GeneralMergeAuthoringPlanResult(
                null,
                null,
                [new CompositionIssue(
                    CompositionPlanningIssueCodes.GeneralMergeMemberNotAdmitted,
                    "The General Merge V2 candidate is currently admitted only for explicitly registered members.",
                    icId)]);
        }

        GeneralMergePlanningResult candidate =
            PlanGeneralMergeDraft(
                _catalog,
                icId,
                registration,
                draft,
                draft.Mappings.SavedRuleResourcePolicy,
                observedFileLengths,
                retainedCapability);
        return new GeneralMergeAuthoringPlanResult(
            candidate.Plan is null
                ? null
                : new GeneralMergeAuthoringPlan(
                    candidate.Plan.MappingDraft,
                    candidate.Plan.Capability,
                    candidate.Plan.InputResources),
            candidate.Admission,
            candidate.Issues);
    }

    public GeneralReplaceAuthoringPlanResult PlanGeneralReplace(
        string icId,
        string number,
        GeneralMappingDraftState draft,
        ReadOnlyMemory<byte> referenceBytes,
        IReadOnlyDictionary<string, long> observedFileLengths,
        ResolvedCapability? retainedCapability)
    {
        if (!BuiltInV2RegistrationRegistry.GeneralReplaceByIc.TryGetValue(
                IcIdentifier.Normalize(icId),
                out GeneralReplaceV2Registration? registration))
        {
            return Failed(
                "The selected General Replace route is unavailable.",
                ExperienceIds.GeneralReplace,
                CompositionPlanningIssueCodes.ReplaceWorkflowNotSupported);
        }

        GeneralAuthoringAdmissionResult admission =
            GeneralAuthoringAdmissionUseCase.ResolveCandidate(
                draft,
                referenceBytes.Length,
                GeneralAuthoringAdmissionUseCase.CreateTrustedParentPolicy(
                    registration.ExactParent.Admission.ParentBinding.ProfileId,
                    draft,
                    registration.ExactParent.Admission.ParentBinding),
                observedFileLengths,
                draft.SavedRuleResourcePolicy);
        if (!admission.IsAdmitted)
        {
            return new GeneralReplaceAuthoringPlanResult(
                null,
                admission,
                admission.ToCompositionIssues());
        }

        if (!TryCreateGeneralReplaceMappings(
                admission,
                referenceBytes.Length,
                out IReadOnlyList<ExplicitMapping> mappings,
                out IReadOnlyList<AddressSpace> addressSpaces,
                out IReadOnlyList<InputArtifactBinding> mappingBindings,
                out IReadOnlyList<GeneralReplacePatchArtifact> patchArtifacts,
                out IReadOnlyList<CompositionIssue> mappingIssues,
                allowUnbound: true))
        {
            return new GeneralReplaceAuthoringPlanResult(null, admission, mappingIssues);
        }

        if (!TryMaterializeGeneralReplacePatchArtifacts(
                patchArtifacts,
                out IReadOnlyDictionary<string, byte[]> patchVirtualArtifacts,
                out IReadOnlyList<CompositionIssue> materializationIssues))
        {
            return new GeneralReplaceAuthoringPlanResult(
                null,
                admission,
                materializationIssues);
        }

        var selection = IcNumberSelection.FromToken(number);
        IReadOnlyList<TpFlashMapRegion> regions = BuiltInTpFlashMapCatalog.GetRegions(
            icId,
            selection,
            postbuildProfile: null);
        ResolvedCapability? capability = retainedCapability;
        if (capability is null &&
            !TryResolveGeneralReplaceCompiledRoute(
                _catalog,
                registration,
                referenceBytes.Length,
                selection,
                draft,
                regions,
                mappings,
                addressSpaces,
                out capability,
                out IReadOnlyList<CompositionIssue> compilationIssues))
        {
            return new GeneralReplaceAuthoringPlanResult(
                null,
                admission,
                compilationIssues);
        }

        capability = capability!.GeneralExecutionPlan is null
            ? capability.BindGeneralExecutionPlan(
                new AcceptedGeneralExecutionPlan(
                    admission,
                    mappingBindings,
                    patchVirtualArtifacts,
                    selection))
            : capability;

        FirmwareConfigMetadataSnapshot? baseFirmware =
            BuiltInFirmwareInspection.ReadFirmwareConfigMetadata(
                _projection,
                icId,
                referenceBytes.Span);
        bool profileResolved = BuiltInPostbuildProfileResolver.TryResolvePostbuildProfile(
            icId,
            BuiltInPostbuildProfileCatalog.GetProfiles(IcIdentifier.Normalize(icId)),
            baseFirmware is not null,
            baseFirmware?.CommonFwVersion,
            out LegacyCombinerPostbuildProfile? profile,
            out CompositionIssue? profileIssue);
        regions = BuiltInTpFlashMapCatalog.GetRegions(
            icId,
            selection,
            profileResolved ? profile : null);
        _ = TryPlanGeneralReplacePostbuild(
            icId,
            selection,
            referenceBytes.Length,
            regions,
            mappings,
            profileResolved,
            profile,
            profileIssue,
            out bool touchesTp,
            out CompositionIssue? planningIssue);
        return new GeneralReplaceAuthoringPlanResult(
            new GeneralReplaceAuthoringPlan(
                draft,
                capability,
                admission,
                registration.ExactParent.Runtime,
                touchesTp,
                planningIssue,
                referenceBytes.Length),
            admission,
            []);
    }

    private static GeneralReplaceAuthoringPlanResult Failed(
        string message,
        string? subject,
        string code)
    {
        return new GeneralReplaceAuthoringPlanResult(
            null,
            null,
            [new CompositionIssue(code, message, subject)]);
    }

    private static string FormatIssues(IEnumerable<CompositionIssue> issues)
    {
        return string.Join(
            Environment.NewLine,
            issues.Select(static issue => $"{issue.Code}: {issue.Message}"));
    }
}

internal sealed class BuiltInGeneralSelectedFileContentInspector :
    ISelectedFileContentInspector
{
    public ValueTask<SelectedFileContentInspection> InspectAsync(
        string selectedPath,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(selectedPath);
        var inspector = new FileContentSnapshotInspector(
            [Path.GetDirectoryName(fullPath)!]);
        return inspector.InspectAsync(fullPath, maximumBytes, cancellationToken);
    }
}
