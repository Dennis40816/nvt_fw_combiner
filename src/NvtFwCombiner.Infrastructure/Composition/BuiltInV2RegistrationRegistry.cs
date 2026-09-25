
using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Infrastructure.Composition;

internal static class BuiltInV2RegistrationRegistry
{
    internal static ReadOnlyCollection<BuiltInV2Registration> StandardMerge { get; } =
        CreateRegistrations(ExperienceIds.StandardMerge);

    internal static ReadOnlyDictionary<string, BuiltInV2Registration> StandardMergeByIc { get; } =
        new(StandardMerge.ToDictionary(static registration => registration.IcId, StringComparer.Ordinal));

    internal static ReadOnlyCollection<BuiltInV2Registration> AbMerge { get; } =
        CreateRegistrations(ExperienceIds.AbMerge);

    // Legacy unique-IC readers fail closed on multiple map-sets, without poisoning registry initialization.
    internal static ReadOnlyDictionary<string, BuiltInV2Registration> AbMergeByIc =>
        new(AbMerge.ToDictionary(static registration => registration.IcId, StringComparer.Ordinal));

    internal static BuiltInV2Registration? FindAbMergeRegistration(string icId, string mapVariantSetId)
    {
        return FindAbMergeRegistration(AbMerge, icId, mapVariantSetId);
    }

    internal static BuiltInV2Registration? FindAbMergeRegistration(
        IEnumerable<BuiltInV2Registration> registrations, string icId, string mapVariantSetId)
    {
        return registrations.SingleOrDefault(registration =>
            StringComparer.Ordinal.Equals(registration.IcId, icId) &&
            StringComparer.Ordinal.Equals(registration.SelectionGroupMapVariantSetId, mapVariantSetId));
    }

    internal static BuiltInV2Registration? FindUniqueAbMergeRegistration(string icId)
    {
        return AbMerge.SingleOrDefault(registration =>
            StringComparer.Ordinal.Equals(registration.IcId, icId));
    }

    internal static ReadOnlyDictionary<string, GeneralMergeV2CandidateRegistration> GeneralMergeByIc { get; } =
        new(SelectRegistrations(ExperienceIds.GeneralMerge)
            .Select(static item => new GeneralMergeV2CandidateRegistration(
                item.Registration.IcId,
                item.Registration.FamilyId!,
                item.Registration.ProfileId,
                item.Registration.ProfileVersion,
                BuiltInV2BundleRegistry.All[item.Bundle.BundleDirectory]))
            .ToDictionary(static registration => registration.IcId, StringComparer.Ordinal));

    internal static ReadOnlyDictionary<string, GeneralReplaceV2Registration> GeneralReplaceByIc { get; } =
        new(SelectRegistrations(ExperienceIds.GeneralReplace)
            .Select(static item => new GeneralReplaceV2Registration(
                item.Registration.IcId,
                item.Registration.ProfileId,
                item.Registration.ProfileVersion,
                BuiltInV2BundleRegistry.All[item.Bundle.BundleDirectory]))
            .ToDictionary(static registration => registration.IcId, StringComparer.Ordinal));

    private static ReadOnlyCollection<BuiltInV2Registration> CreateRegistrations(string workflowId)
    {
        return Array.AsReadOnly(
        [
            .. SelectRegistrations(workflowId)
                .Select(item => new BuiltInV2Registration(
                    item.Registration.IcId,
                    item.Registration.ProfileId,
                    item.Registration.ProfileVersion,
                    item.Registration.MapVariantSetId,
                    BuiltInV2BundleRegistry.All[item.Bundle.BundleDirectory],
                    CompositionKind.Merge,
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
        WorkflowId = workflowId ?? ExperienceIds.StandardMerge;
        bool isKnownWorkflow = WorkflowId is ExperienceIds.StandardMerge or ExperienceIds.AbMerge;
        bool kindMatchesWorkflow = compositionKind == CompositionKind.Merge;
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

    internal SourceEnvelopeProfileBinding? SourceEnvelopeBinding => IsStandardMerge
        ? _bundle.GetSourceEnvelopeBinding(ProfileId, ProfileVersion)
        : null;

    internal TrustedMapBoundProfileDeclaration GetMapBoundDeclaration(string mapId)
    {
        return _bundle.GetMapBoundDeclaration(ProfileId, ProfileVersion, IcId, WorkflowId, mapId);
    }

    /// <summary>Projects the exact trusted family of this registration without synthesizing a compilation.</summary>
    internal FirmwareFamilyResolutionDefinition GetFirmwareFamily()
    {
        return _bundle.GetFirmwareFamily(ProfileId, ProfileVersion);
    }

    internal bool TryGetAbAuthoringDefinition(out CanonicalAbAuthoringDefinition? definition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return _bundle.TryGetAbAuthoringDefinition(ProfileId, ProfileVersion, out definition, out issues);
    }

    internal bool HasReportClassificationMetadata =>
        _bundle.ProfileDeclaresMetadataPurpose(
            ProfileId,
            ProfileVersion,
            CompositionProfileMetadataPurpose.ReportClassification);

    private bool IsStandardMerge => WorkflowId == ExperienceIds.StandardMerge;

    private bool IsAbMerge => WorkflowId == ExperienceIds.AbMerge;

    private string ProfileLabel => WorkflowId switch
    {
        ExperienceIds.StandardMerge => "Standard Merge profile",
        ExperienceIds.AbMerge => "AB Merge profile",
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

    /// <summary>Compiles one declared exact map and retains its canonical metadata references.</summary>
    internal MetadataPlanDefinition CreateExactMapMetadataPlan(string mapId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        if (SourceEnvelopeBinding is not null)
        {
            return _bundle.CreateDeclaredMetadataPlan(
                ProfileId, ProfileVersion, IcId, WorkflowId, mapId);
        }

        IReadOnlyList<FirmwareImageMap> maps = GetMapVariants(
            out _,
            out IReadOnlyList<CompositionIssue> mapIssues);
        FirmwareImageMap selectedMap = mapIssues.Count == 0
            ? maps.SingleOrDefault(map => StringComparer.Ordinal.Equals(
                    map.MapId,
                    mapId)) ??
                throw new InvalidDataException(
                    $"No trusted {WorkflowId} map '{mapId}' is registered for {IcId}.")
            : throw new InvalidDataException(
                $"Trusted {WorkflowId} maps for {IcId} were rejected: " +
                string.Join(", ", mapIssues.Select(static issue => issue.Code)));

        IReadOnlyCollection<string>[] selections = InputSelectionGroupMemberSlotIds.Count == 0
            ? [[]]
            : [[], InputSelectionGroupMemberSlotIds];
        var matches = new List<CompiledComposition>();
        var issueCodes = new List<string>();
        foreach (IReadOnlyCollection<string> selection in selections)
        {
            TryCompile(
                selectedMap.CapacityBytes,
                selection,
                out CompiledComposition? composition,
                out IReadOnlyList<CompositionIssue> compileIssues);
            issueCodes.AddRange(compileIssues.Select(static issue => issue.Code));
            if (compileIssues.Count == 0 &&
                composition?.V2Details.Provenance.ResolvedMap.ImageMap is { } compiledMap &&
                StringComparer.Ordinal.Equals(compiledMap.MapId, selectedMap.MapId))
            {
                matches.Add(composition);
            }
        }

        return matches.Count == 1
            ? CreateMetadataPlan(matches[0])
            : throw new InvalidDataException(
                $"Compiler matched {matches.Count} candidates for exact {WorkflowId} map " +
                $"'{mapId}' on {IcId}: {string.Join(", ", issueCodes)}");
    }

    internal bool TryGetContainerPolicy(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out V2StandardMergeContainerPolicy? policy)
    {
        IReadOnlyList<long> capacities = GetMapCapacities(out IReadOnlyList<CompositionIssue> issues);
        FirmwareImageMap? map = SourceEnvelopeBinding is { } envelope
            ? GetMapBoundDeclaration(envelope.LayoutTemplateMapId).Map
            : _summaryCompilation.Value.CompiledComposition?.V2Details.Provenance.ResolvedMap.ImageMap;
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
        TryCompile(inputLength, requestedTopology, selectedInputSlotIds, [],
            out composition, out issues);
    }

    internal void TryCompile(
        long? inputLength,
        TopologySelection? requestedTopology,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        IReadOnlyList<FirmwareArtifactPayload> resolutionArtifacts,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(resolutionArtifacts);
        if (!TryAdmitCompileRequest(
                inputLength,
                requestedTopology,
                resolutionArtifacts,
                out long? requestedCapacity,
                out TopologySelection? effectiveTopology,
                out issues))
        {
            composition = null;
            return;
        }

        V2CompositionPlanCompileResult compilation = CompileExecutable(
            requestedCapacity,
            effectiveTopology,
            selectedInputSlotIds,
            resolutionArtifacts);
        composition = compilation.CompiledComposition;
        issues = compilation.Issues;
    }

    /// <summary>
    /// Compiles the plan that <c>TryCompile(inputLength, ...)</c> admits for each input length, in
    /// order and only as far as the caller enumerates. Within one call, input lengths admitted to
    /// the identical compiler request share its successful compilation; a request identical to this
    /// Standard registration's successful summary compilation reuses that existing result. Failures
    /// are never reused, and no other compilation outlives the call.
    /// </summary>
    internal IEnumerable<(long? InputLength, CompiledComposition? Composition, IReadOnlyList<CompositionIssue> Issues)>
        TryCompileEach(IEnumerable<long?> inputLengths)
    {
        ArgumentNullException.ThrowIfNull(inputLengths);
        var compiled = new List<(long? MapCapacity, V2CompositionPlanCompileResult Compilation)>();
        foreach (long? inputLength in inputLengths)
        {
            if (!TryAdmitCompileRequest(
                    inputLength,
                    requestedTopology: null,
                    [],
                    out long? requestedCapacity,
                    out TopologySelection? effectiveTopology,
                    out IReadOnlyList<CompositionIssue> issues))
            {
                yield return (inputLength, null, issues);
                continue;
            }

            // Only topology-free requests are keyed: with no topology, slot selection or resolution
            // artifact, the map capacity is the sole varying compiler input.
            int previous = effectiveTopology is null
                ? compiled.FindIndex(entry => entry.MapCapacity == requestedCapacity)
                : -1;
            V2CompositionPlanCompileResult compilation;
            if (previous >= 0)
            {
                compilation = compiled[previous].Compilation;
            }
            else
            {
                V2CompositionPlanCompileResult? summary =
                    effectiveTopology is null && IsStandardSummaryRequest(requestedCapacity)
                        ? _summaryCompilation.Value
                        : null;
                compilation = summary?.CompiledComposition is not null
                    ? summary
                    : CompileExecutable(requestedCapacity, effectiveTopology, null, []);
                if (effectiveTopology is null && compilation.CompiledComposition is not null)
                {
                    compiled.Add((requestedCapacity, compilation));
                }
            }

            yield return (inputLength, compilation.CompiledComposition, compilation.Issues);
        }
    }

    /// <summary>Admits one input length and derives the exact map capacity and topology requested from the compiler.</summary>
    private bool TryAdmitCompileRequest(
        long? inputLength,
        TopologySelection? requestedTopology,
        IReadOnlyList<FirmwareArtifactPayload> resolutionArtifacts,
        out long? requestedCapacity,
        out TopologySelection? effectiveTopology,
        out IReadOnlyList<CompositionIssue> issues)
    {
        requestedCapacity = null;
        effectiveTopology = requestedTopology;
        if (requestedTopology is not null && !IsAbMerge)
        {
            issues =
            [
                new CompositionIssue(
                    "profile.v2.builtin.topology-not-admitted",
                    "Only AB Merge built-in registrations admit an explicit topology selection."),
            ];
            return false;
        }

        IReadOnlyList<long> capacities = GetMapCapacities(out issues);
        if (issues.Count != 0)
        {
            return false;
        }

        SourceEnvelopeProfileBinding? capturedSourceEnvelope = IsAbMerge
            ? _bundle.GetSourceEnvelopeBinding(ProfileId, ProfileVersion)
            : SourceEnvelopeBinding;
        bool hasCapturedEnvelopeLength = capturedSourceEnvelope is { } envelope &&
            inputLength is > 0 &&
            resolutionArtifacts.Count(artifact =>
                StringComparer.Ordinal.Equals(artifact.ArtifactId, envelope.SourceSlotId) &&
                artifact.LengthBytes == inputLength.Value) == 1;
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
                    issues = [];
                    return false;
                }

                requestedCapacity = requestedTopology is null ? capacities[0] : null;
                effectiveTopology ??= CreateSummaryTopology();
            }
            else if (!capacities.Contains(inputLength.Value) &&
                !hasCapturedEnvelopeLength)
            {
                issues =
                [
                    new CompositionIssue(
                        IsStandardMerge
                            ? CompositionPlanningIssueCodes.StandardMergeDpLengthUnsupported
                            : CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                        $"Selected DP BIN length 0x{inputLength.Value:X} is unsupported; {IcId} {ProfileLabel} accepts DP input lengths {BuiltInV2Bundle.FormatCapacities(capacities)}."),
                ];
                return false;
            }
            else
            {
                requestedCapacity = inputLength;
            }
        }
        if (IsAbMerge && hasCapturedEnvelopeLength)
        {
            requestedCapacity = inputLength;
        }

        return true;
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
        if (SourceEnvelopeBinding is { } envelope)
        {
            TrustedMapBoundProfileDeclaration declaration = GetMapBoundDeclaration(
                envelope.LayoutTemplateMapId);
            CompositionProfileDefinition profile = declaration.ProfileEntry.Profile;
            string[] requiredSpaces =
            [
                .. profile.Spaces.OfType<InputArtifactProfileSpace>()
                    .Where(space => profile.InputSlots.Single(slot =>
                        StringComparer.Ordinal.Equals(slot.SlotId, space.SlotId)).Required)
                    .Select(static space => space.SpaceId)
                    .Order(StringComparer.Ordinal),
            ];
            return new CapabilityProfileSummary(
                ProfileId, IcId, CompositionKind, Array.AsReadOnly(requiredSpaces),
                profile.Output.FileNameTemplate, profile.IcNumberInputMode,
                CompileSucceeded: false, [], DeclarationReady: true);
        }

        V2CompositionPlanCompileResult compilation = _summaryCompilation.Value;
        return compilation.CompiledComposition is { } composition
            ? CapabilityProfileSummary.FromCompiled(composition)
            : new CapabilityProfileSummary(
                ProfileId,
                IcId,
                CompositionKind,
                [],
                IsStandardMerge
                    ? StandardMergeFallbackOutputFileName
                    : $"nt{IcId[2..].ToLowerInvariant()}-ab-merge.bin",
                null,
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
        if (IsAbMerge)
        {
            IReadOnlyList<FirmwareImageMap> maps = GetMapVariants(out _, out IReadOnlyList<CompositionIssue> mapIssues);
            if (mapIssues.Count != 0) { return V2CompositionPlanCompileResult.Failed(mapIssues); }
            FirmwareImageMap? representative = maps.OrderBy(static map => map.CapacityBytes)
                .ThenBy(static map => map.MapId, StringComparer.Ordinal).FirstOrDefault();
            return representative is null
                ? V2CompositionPlanCompileResult.Failed([new CompositionIssue(BuiltInV2Bundle.CompilationFailed,
                    $"The built-in V2 {ProfileLabel} for {IcId} has no declared maps.")])
                : CompileExecutable(representative.CapacityBytes, HeadlessRouteSelection.CreateTopologySelection(
                    representative.Applicability.TopologyRequirement, representative.MapId));
        }
        return TryAdmitStandardSummaryRequest(out long? mapCapacity, out IReadOnlyList<CompositionIssue> issues)
            ? CompileExecutable(mapCapacity)
            : V2CompositionPlanCompileResult.Failed(issues);
    }

    /// <summary>Admits the topology-free map capacity that the Standard summary compiles.</summary>
    private bool TryAdmitStandardSummaryRequest(
        out long? mapCapacity,
        out IReadOnlyList<CompositionIssue> issues)
    {
        IReadOnlyList<long> capacities = GetMapCapacities(out issues);
        mapCapacity = IsStandardMerge && capacities.Count > 1 ? capacities[0] : null;
        if (issues.Count == 0 && capacities.Count == 0)
        {
            issues =
            [
                new CompositionIssue(
                    BuiltInV2Bundle.CompilationFailed,
                    $"The built-in V2 {ProfileLabel} for {IcId} has no declared map capacities."),
            ];
        }

        return issues.Count == 0;
    }

    /// <summary>True when a topology-free request is exactly the one the Standard summary compiles.</summary>
    private bool IsStandardSummaryRequest(long? mapCapacity)
    {
        return IsStandardMerge &&
            TryAdmitStandardSummaryRequest(out long? summaryCapacity, out _) &&
            summaryCapacity == mapCapacity;
    }

    private V2CompositionPlanCompileResult CompileExecutable(
        long? requestedMapCapacity,
        TopologySelection? requestedTopology = null,
        IReadOnlyCollection<string>? selectedInputSlotIds = null,
        IReadOnlyList<FirmwareArtifactPayload>? resolutionArtifacts = null)
    {
        return IsAbMerge
            ? _bundle.CompileAbMergeFunctionOpen(
                ProfileId,
                ProfileVersion,
                IcId,
                requestedMapCapacity,
                requestedTopology,
                $"The built-in V2 {ProfileLabel} for {IcId} did not produce an executable composition.",
                selectedInputSlotIds,
                resolutionArtifacts)
            : _bundle.CompileExecutable(
                ProfileId,
                ProfileVersion,
                IcId,
                WorkflowId,
                requestedMapCapacity,
                $"The built-in V2 {ProfileLabel} for {IcId} did not produce an executable composition.",
                resolutionArtifacts ?? [],
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
        V2ExplicitMappingInputBinding[] bindings =
        [
            new(ReferenceAddressSpaceId, ReferenceSlotId, referenceLength),
            .. sourceSpaces.Select(source =>
                new V2ExplicitMappingInputBinding(
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
