
using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Bootstrap;

internal static class BuiltInV2RegistrationRegistry
{
    internal static ReadOnlyCollection<BuiltInV2Registration> StandardMerge { get; } =
        CreateRegistrations(IcWorkflowIds.StandardMerge);

    internal static ReadOnlyDictionary<string, BuiltInV2Registration> StandardMergeByIc { get; } =
        new(StandardMerge.ToDictionary(static registration => registration.IcId, StringComparer.Ordinal));

    internal static ReadOnlyCollection<BuiltInV2Registration> AbMerge { get; } =
        CreateRegistrations(IcWorkflowIds.AbMerge);

    internal static ReadOnlyDictionary<string, BuiltInV2Registration> AbMergeByIc { get; } =
        new(AbMerge.ToDictionary(static registration => registration.IcId, StringComparer.Ordinal));

    internal static Lazy<ReadOnlyDictionary<string, BuiltInV2Registration>> DpReplaceByIc { get; } =
        new(() => new ReadOnlyDictionary<string, BuiltInV2Registration>(
            CreateRegistrations(IcWorkflowIds.DpReplace)
                .ToDictionary(static registration => registration.IcId, StringComparer.Ordinal)));

    internal static ReadOnlyDictionary<string, GeneralMergeV2CandidateRegistration> GeneralMergeByIc { get; } =
        new(SelectRegistrations(IcWorkflowIds.GeneralMerge)
            .Select(static item => new GeneralMergeV2CandidateRegistration(
                item.Registration.IcId,
                item.Registration.FamilyId!,
                item.Registration.ProfileId,
                item.Registration.ProfileVersion,
                BuiltInV2BundleRegistry.All[item.Bundle.BundleDirectory]))
            .ToDictionary(static registration => registration.IcId, StringComparer.Ordinal));

    internal static ReadOnlyDictionary<string, GeneralReplaceV2Registration> GeneralReplaceByIc { get; } =
        new(SelectRegistrations(IcWorkflowIds.GeneralReplace)
            .Select(static item => new GeneralReplaceV2Registration(
                item.Registration.IcId,
                item.Registration.ProfileId,
                item.Registration.ProfileVersion,
                BuiltInV2BundleRegistry.All[item.Bundle.BundleDirectory]))
            .ToDictionary(static registration => registration.IcId, StringComparer.Ordinal));

    private static ReadOnlyCollection<BuiltInV2Registration> CreateRegistrations(string workflowId)
    {
        CompositionKind compositionKind = workflowId == IcWorkflowIds.DpReplace
            ? CompositionKind.Replace
            : CompositionKind.Merge;
        return Array.AsReadOnly(
        [
            .. SelectRegistrations(workflowId)
                .Select(item => new BuiltInV2Registration(
                    item.Registration.IcId,
                    item.Registration.ProfileId,
                    item.Registration.ProfileVersion,
                    item.Registration.MapVariantSetId,
                    BuiltInV2BundleRegistry.All[item.Bundle.BundleDirectory],
                    compositionKind,
                    workflowId))
                .OrderBy(static registration => registration.IcId, StringComparer.Ordinal),
        ]);
    }

    private static IEnumerable<(
        ProfileBundlePackageTrustEntry Bundle,
        ProfileBundleRuntimeRegistration Registration)> SelectRegistrations(string workflowId)
    {
        return BuiltInV2BundleRegistry.TrustIndex.Bundles
            .SelectMany(
                static bundle => bundle.RuntimeRegistrations,
                static (bundle, registration) => (Bundle: bundle, Registration: registration))
            .Where(item => StringComparer.Ordinal.Equals(
                item.Registration.WorkflowId,
                workflowId));
    }
}

internal sealed class BuiltInV2Registration
{
    private const string StandardMergeFallbackOutputFileName =
        "nvt-fw-combiner-output.bin";

    private readonly Lazy<V2CompositionPlanCompileResult> _summaryCompilation;
    private readonly BuiltInV2Bundle _bundle;

    internal BuiltInV2Registration(
        string icId,
        string profileId,
        string profileVersion,
        string? mapVariantSetId,
        BuiltInV2Bundle bundle,
        CompositionKind compositionKind,
        string? workflowId = null)
    {
        if (compositionKind is not (CompositionKind.Merge or CompositionKind.Replace))
        {
            throw new ArgumentOutOfRangeException(nameof(compositionKind));
        }

        IcId = icId;
        ProfileId = profileId;
        ProfileVersion = profileVersion;
        MapVariantSetId = mapVariantSetId;
        _bundle = bundle;
        CompositionKind = compositionKind;
        WorkflowId = workflowId ?? (compositionKind == CompositionKind.Merge
            ? IcWorkflowIds.StandardMerge
            : IcWorkflowIds.DpReplace);
        bool isKnownWorkflow = WorkflowId is IcWorkflowIds.StandardMerge or IcWorkflowIds.AbMerge or IcWorkflowIds.DpReplace;
        bool kindMatchesWorkflow = WorkflowId == IcWorkflowIds.DpReplace
            ? compositionKind == CompositionKind.Replace
            : compositionKind == CompositionKind.Merge;
        if (!isKnownWorkflow || !kindMatchesWorkflow)
        {
            throw new ArgumentException("Built-in registration workflow and composition kind are inconsistent.", nameof(workflowId));
        }

        _summaryCompilation = new(CompileSummary);
    }

    internal string IcId { get; }

    internal string ProfileId { get; }

    private CompositionKind CompositionKind { get; }

    internal string WorkflowId { get; }

    internal string ProfileVersion { get; }

    internal string? MapVariantSetId { get; }

    internal string BundleContentHash => _bundle.ContentHash;

    internal bool HasReportClassificationMetadata =>
        _bundle.ProfileDeclaresMetadataPurpose(
            ProfileId,
            ProfileVersion,
            CompositionProfileMetadataPurpose.ReportClassification);

    private bool IsStandardMerge => WorkflowId == IcWorkflowIds.StandardMerge;

    private bool IsAbMerge => WorkflowId == IcWorkflowIds.AbMerge;

    private bool IsDpReplace => WorkflowId == IcWorkflowIds.DpReplace;

    private string ProfileLabel => WorkflowId switch
    {
        IcWorkflowIds.StandardMerge => "Standard Merge profile",
        IcWorkflowIds.AbMerge => "AB Merge profile",
        IcWorkflowIds.DpReplace => "DP Replace profile",
        _ => throw new InvalidOperationException("Unknown built-in workflow."),
    };

    internal bool HasMultipleMapCapacities
    {
        get
        {
            IReadOnlyList<long> capacities = GetMapCapacities(out IReadOnlyList<CompositionIssue> issues);
            return issues.Count == 0 && capacities.Count > 1;
        }
    }

    internal IReadOnlyList<string> InputSelectionGroupMemberSlotIds =>
        _bundle.GetInputSelectionGroupMemberSlotIds(ProfileId, ProfileVersion);

    internal string? SelectionGroupMapVariantSetId
    {
        get
        {
            bool hasSelectionGroup = InputSelectionGroupMemberSlotIds.Count != 0;
            return hasSelectionGroup == (MapVariantSetId is not null)
                ? MapVariantSetId
                : throw new InvalidDataException(
                    $"Built-in registration '{WorkflowId}/{IcId}' selection-group and map-variant-set declarations disagree.");
        }
    }

    internal bool MatchesSelector(string selector)
    {
        return string.Equals(ProfileId, selector, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(IcId, selector, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(CliCompositionRunSupport.GetIcNumber(IcId), selector, StringComparison.OrdinalIgnoreCase);
    }

    internal IReadOnlyList<long> GetMapCapacities(out IReadOnlyList<CompositionIssue> issues)
    {
        return _bundle.GetMapCapacities(ProfileId, ProfileVersion, IcId, WorkflowId, out issues);
    }

    internal IReadOnlyList<FirmwareImageMap> GetMapVariants(
        out IcNumberInputMode? icNumberInputMode,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return _bundle.GetMapVariants(
            ProfileId,
            ProfileVersion,
            IcId,
            WorkflowId,
            out icNumberInputMode,
            out issues);
    }

    internal bool TryGetContainerPolicy(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out V2StandardMergeContainerPolicy? policy)
    {
        IReadOnlyList<long> capacities = GetMapCapacities(out IReadOnlyList<CompositionIssue> issues);
        CompiledComposition? composition = _summaryCompilation.Value.CompiledComposition;
        FirmwareImageMap? map = composition?.V2Details.Provenance.ResolvedMap.ImageMap;
        FirmwareRegion? tpOverlay = map?.Regions.SingleOrDefault(static region => region.RegionId == "tp-overlay");
        FirmwareRegion? customerInfo = map?.Regions.SingleOrDefault(static region => region.RegionId == "customer-info");
        if (!IsStandardMerge || issues.Count != 0 || capacities.Count <= 1 || tpOverlay is null || customerInfo is null)
        {
            policy = null;
            return false;
        }

        policy = new V2StandardMergeContainerPolicy(capacities, tpOverlay.Range, customerInfo.Range);
        return true;
    }

    internal void TryCompile(
        long? inputLength,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        TryCompile(
            inputLength,
            requestedTopology: null,
            selectedInputSlotIds: null,
            out composition,
            out issues);
    }

    internal void TryCompile(
        long? inputLength,
        IReadOnlyCollection<string> selectedInputSlotIds,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        TryCompile(
            inputLength,
            requestedTopology: null,
            selectedInputSlotIds,
            out composition,
            out issues);
    }

    internal void TryCompile(
        long? inputLength,
        TopologySelection? requestedTopology,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        TryCompile(
            inputLength,
            requestedTopology,
            selectedInputSlotIds: null,
            out composition,
            out issues);
    }

    internal void TryCompile(
        long? inputLength,
        TopologySelection? requestedTopology,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        if (requestedTopology is not null && !IsAbMerge)
        {
            composition = null;
            issues =
            [
                new CompositionIssue(
                    "profile.v2.builtin.topology-not-admitted",
                    "Only AB Merge built-in registrations admit an explicit topology selection."),
            ];
            return;
        }

        IReadOnlyList<long> capacities = GetMapCapacities(out issues);
        if (issues.Count != 0)
        {
            composition = null;
            return;
        }

        long? requestedCapacity = null;
        TopologySelection? effectiveTopology = requestedTopology;
        if ((IsStandardMerge || IsAbMerge) && capacities.Count > 1)
        {
            if (IsStandardMerge && InputSelectionGroupMemberSlotIds.Count != 0)
            {
                requestedCapacity = null;
            }
            else if (inputLength is null)
            {
                if (IsStandardMerge && InputSelectionGroupMemberSlotIds.Count == 0)
                {
                    composition = null;
                    issues = [];
                    return;
                }

                requestedCapacity = requestedTopology is null ? capacities[0] : null;
                effectiveTopology ??= CreateSummaryTopology();
            }
            else if (!capacities.Contains(inputLength.Value))
            {
                composition = null;
                issues =
                [
                    new CompositionIssue(
                        IsStandardMerge
                            ? WorkbenchIssueCodes.StandardMergeDpLengthUnsupported
                            : CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                        $"Selected DP BIN length 0x{inputLength.Value:X} is unsupported; {IcId} {ProfileLabel} accepts DP input lengths {BuiltInV2Bundle.FormatCapacities(capacities)}."),
                ];
                return;
            }
            else
            {
                requestedCapacity = inputLength;
            }
        }
        else if (IsDpReplace)
        {
            if (inputLength is null || !capacities.Contains(inputLength.Value))
            {
                composition = null;
                issues =
                [
                    new CompositionIssue(
                        CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                        $"{IcId} DP Replace base flash BIN length must be one of {BuiltInV2Bundle.FormatCapacities(capacities)} (actual 0x{inputLength.GetValueOrDefault():X})."),
                ];
                return;
            }

            requestedCapacity = inputLength;
        }

        V2CompositionPlanCompileResult compilation = CompileExecutable(
            requestedCapacity,
            effectiveTopology,
            selectedInputSlotIds);
        composition = compilation.CompiledComposition;
        issues = compilation.Issues;
    }

    internal bool TryGetAuthoringDefaultCapacity(
        out long capacity,
        out IReadOnlyList<CompositionIssue> issues)
    {
        IReadOnlyList<long> capacities = GetMapCapacities(out issues);
        if (!IsStandardMerge || issues.Count != 0 || capacities.Count <= 1)
        {
            capacity = 0;
            return false;
        }

        capacity = capacities[^1];
        return true;
    }

    internal CapabilityProfileSummary CreateProfileSummary()
    {
        V2CompositionPlanCompileResult compilation = _summaryCompilation.Value;
        return compilation.CompiledComposition is { } composition
            ? CanonicalCapabilityProjection.FromCompiled(composition)
            : new CapabilityProfileSummary(
                ProfileId,
                IcId,
                CompositionKind,
                [],
                IsStandardMerge
                    ? StandardMergeFallbackOutputFileName
                    : IsDpReplace
                        ? $"nt{IcId[2..].ToLowerInvariant()}-dp-replace.bin"
                        : $"nt{IcId[2..].ToLowerInvariant()}-ab-merge.bin",
                IsDpReplace ? CompiledIcNumberPolicy.SingleSelector : null,
                CompileSucceeded: false,
                Array.AsReadOnly(compilation.Issues.Select(static issue => issue.Code).ToArray()));
    }

    internal MetadataPlanDefinition CreateMetadataPlan(
        CompiledComposition composition)
    {
        return _bundle.CreateMetadataPlan(
            ProfileId,
            ProfileVersion,
            composition);
    }

    private V2CompositionPlanCompileResult CompileSummary()
    {
        IReadOnlyList<long> capacities = GetMapCapacities(out IReadOnlyList<CompositionIssue> issues);
        return (issues.Count, capacities.Count) switch
        {
            ( > 0, _) => V2CompositionPlanCompileResult.Failed(issues),
            (_, 0) => V2CompositionPlanCompileResult.Failed(
                [new CompositionIssue(
                    BuiltInV2Bundle.CompilationFailed,
                    $"The built-in V2 {ProfileLabel} for {IcId} has no declared {(IsDpReplace ? "base" : "map")} capacities.")]),
            _ => CompileExecutable(
                IsDpReplace || ((IsStandardMerge || IsAbMerge) && capacities.Count > 1) ? capacities[0] : null,
                IsAbMerge && capacities.Count > 1 ? CreateSummaryTopology() : null),
        };
    }

    private V2CompositionPlanCompileResult CompileExecutable(
        long? requestedMapCapacity,
        TopologySelection? requestedTopology = null,
        IReadOnlyCollection<string>? selectedInputSlotIds = null)
    {
        return IsAbMerge
            ? _bundle.CompileAbMergeFunctionOpen(
                ProfileId,
                ProfileVersion,
                IcId,
                requestedMapCapacity,
                requestedTopology,
                $"The built-in V2 {ProfileLabel} for {IcId} did not produce an executable composition.")
            : _bundle.CompileExecutable(
                ProfileId,
                ProfileVersion,
                IcId,
                WorkflowId,
                requestedMapCapacity,
                $"The built-in V2 {ProfileLabel} for {IcId} did not produce an executable composition.",
                selectedInputSlotIds);
    }

    private static TopologySelection CreateSummaryTopology()
    {
        return new TopologySelection(
            chipCount: 1,
            label: "1 IC",
            source: TopologySelectionSource.Requested,
            sourceId: "summary-default");
    }
}

internal sealed record GeneralMergeV2CandidateRegistration(
    string IcId,
    string FamilyId,
    string ProfileId,
    string ProfileVersion,
    BuiltInV2Bundle Bundle);

internal sealed class GeneralReplaceV2Registration
{
    internal const string ReferenceAddressSpaceId = "reference-image";
    private readonly Lazy<SavedRuleV2GeneralReplaceExactParent> _exactParent;

    internal GeneralReplaceV2Registration(
        string icId,
        string profileId,
        string profileVersion,
        BuiltInV2Bundle bundle)
    {
        IcId = icId;
        ProfileId = profileId;
        ProfileVersion = profileVersion;
        Bundle = bundle;
        _exactParent = new(() => Bundle.GetGeneralReplaceExactParent(ProfileId));
    }

    internal string IcId { get; }

    internal string ProfileId { get; }

    internal string ProfileVersion { get; }

    internal BuiltInV2Bundle Bundle { get; }

    internal string BundleContentHash => Bundle.ContentHash;

    internal string ReferenceSlotId => ExactParent.Admission.InputPolicies
        .Single(static policy => StringComparer.Ordinal.Equals(policy.Role, "reference"))
        .SlotId;

    private string SourceSlotId => ExactParent.Admission.InputPolicies
        .Single(static policy => StringComparer.Ordinal.Equals(policy.Role, "source"))
        .SlotId;

    internal string DefaultOutputFileName =>
        $"nt{IcId[2..].ToLowerInvariant()}-general-replace.bin";

    internal SavedRuleV2GeneralReplaceExactParent ExactParent => _exactParent.Value;

    internal SavedRuleV2GeneralReplaceAdmissionContext SavedRuleAdmissionContext =>
        ExactParent.Admission;

    internal IReadOnlyList<FirmwareImageMap> GetMapVariants(
        out IcNumberInputMode? inputMode,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return Bundle.GetMapVariants(
            ProfileId,
            ProfileVersion,
            IcId,
            ExperienceIds.GeneralReplace,
            out inputMode,
            out issues);
    }

    internal V2CompositionPlanCompileResult Compile(
        long referenceLength,
        IReadOnlyList<AddressSpace> sourceSpaces,
        IReadOnlyList<ExplicitMapping> mappings)
    {
        V2RuntimeReferenceReplaceInputBinding[] bindings =
        [
            new(ReferenceAddressSpaceId, ReferenceSlotId, referenceLength),
            .. sourceSpaces.Select(source =>
                new V2RuntimeReferenceReplaceInputBinding(
                    source.AddressSpaceId,
                    SourceSlotId,
                    source.Length)),
        ];
        return Bundle.CompileRuntimeReferenceReplace(
            ProfileId,
            ProfileVersion,
            IcId,
            ExperienceIds.GeneralReplace,
            requestedTopology: null,
            new V2RuntimeReferenceReplaceCompileRequest(bindings, mappings));
    }

    internal MetadataPlanDefinition CreateMetadataPlan(
        CompiledComposition composition)
    {
        return Bundle.CreateMetadataPlan(
            ProfileId,
            ProfileVersion,
            composition);
    }
}
