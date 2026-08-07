using System.Collections.Frozen;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles.FirmwareFamilies;
using NvtFwCombiner.Profiles.V2;
using V2CompositionProfileDefinition = NvtFwCombiner.Domain.Composition.CompositionProfileDefinition;

namespace NvtFwCombiner.Bootstrap;

internal static class BuiltInV2BundleRegistry
{
    internal static ProfileBundlePackageTrustIndex TrustIndex { get; } =
        ProfileBundlePackageTrustIndexLoader.Load(Path.Combine(
            AppContext.BaseDirectory,
            "profiles",
            "built-in",
            "package-trust-index.json"));

    internal static FrozenDictionary<string, BuiltInV2Bundle> All { get; } =
        TrustIndex.Bundles.ToFrozenDictionary(
            static bundle => bundle.BundleDirectory,
            bundle => new BuiltInV2Bundle(
                bundle.BundleDirectory,
                bundle.BundleVersion,
                bundle.ContentHash,
                TrustIndex.TrustAnchorBindingId),
            StringComparer.Ordinal);
}

internal sealed class BuiltInV2Bundle
{
    internal const string CompilationFailed = "profile.v2.builtin-compilation-failed";
    private readonly Lazy<TrustedProfileBundleCatalog> _catalog;
    private readonly string _bundleVersion;
    private readonly string _trustAnchorBindingId;

    internal BuiltInV2Bundle(
        string bundleDirectory,
        string bundleVersion,
        string contentHash,
        string trustAnchorBindingId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustAnchorBindingId);
        RelativeRoot = Path.Combine("profiles", "built-in", bundleDirectory);
        _bundleVersion = bundleVersion;
        ContentHash = contentHash;
        _trustAnchorBindingId = trustAnchorBindingId;
        _catalog = new Lazy<TrustedProfileBundleCatalog>(LoadCatalog);
    }

    internal string RelativeRoot { get; }

    internal string ContentHash { get; }

    /// <summary>Projects the exact trusted identity used by a General Merge Saved Rule v2 parent.</summary>
    internal SavedRuleV2ParentBinding GetGeneralMergeSavedRuleParentBinding(
        string profileId)
    {
        return GetSavedRuleParentBinding(
            GetProfile(profileId),
            WorkbenchGeneralMergeIds.LogicalOutputMapId);
    }

    /// <summary>
    /// Projects the exact trusted parent facts required to admit a complete
    /// General Merge Saved Rule v2 document before draft materialization.
    /// </summary>
    internal SavedRuleV2GeneralMergeAdmissionContext
        GetGeneralMergeSavedRuleAdmissionContext(string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        TrustedCompositionProfileCatalogEntry profileEntry =
            _catalog.Value.Profiles.Single(candidate =>
                StringComparer.Ordinal.Equals(
                    candidate.Profile.ProfileId,
                    profileId));
        V2CompositionProfileDefinition profile =
            profileEntry.Profile.Promotion.Stage ==
            CompiledProfilePromotionStage.ExecutableCandidate
                ? profileEntry.Profile
                : throw new InvalidDataException(
                    "General Merge Saved Rule admission requires the exact executable-candidate parent.");

        return new SavedRuleV2GeneralMergeAdmissionContext(
            GetGeneralMergeSavedRuleParentBinding(profileId),
            SavedRuleSchemaTokens.PromotionStageExecutableCandidate,
            [
                .. profile.InputSlots.Select(static slot =>
                    new SavedRuleV2ParentInputPolicy(
                        slot.SlotId,
                        slot.Role,
                        slot.Cardinality,
                        [.. slot.AcceptedExtensions])),
            ],
            [.. profile.Validations.Select(static validation => validation.RuleId)],
            [
                .. profile.ProcessorStages.Select(
                    static processor => processor.ProcessorStageId),
            ]);
    }

    /// <summary>Projects Saved Rule and runtime facts from one exact profile entry.</summary>
    internal SavedRuleV2GeneralReplaceExactParent
        GetGeneralReplaceExactParent(string profileId)
    {
        return SavedRuleV2GeneralReplaceExactParentResolver.Resolve(
            _catalog.Value,
            profileId);
    }

    private TrustedCompositionProfileCatalogEntry GetProfile(string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        return _catalog.Value.Profiles.Single(candidate =>
            StringComparer.Ordinal.Equals(
                candidate.Profile.ProfileId,
                profileId));
    }

    private SavedRuleV2ParentBinding GetSavedRuleParentBinding(
        TrustedCompositionProfileCatalogEntry profile,
        string mapId)
    {
        ProfileBundleIdentity bundle = _catalog.Value.BundleIdentity;
        return new SavedRuleV2ParentBinding(
            bundle.BundleId,
            bundle.BundleVersion,
            bundle.ContentHash,
            profile.Profile.ProfileId,
            profile.Profile.ProfileVersion,
            profile.Identity.ContentHash,
            profile.Family.Family.FamilyId,
            profile.Family.Family.FamilyVersion,
            profile.Family.Family.FamilyContentHash,
            mapId);
    }

    internal bool TryResolveMetadataDefinition(
        FirmwareMetadataStructureDefinitionReference reference,
        out FirmwareMetadataStructureDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(reference);
        TrustedFirmwareFamilyCatalogEntry? family = _catalog.Value.Families.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.Family.FamilyId, reference.FamilyId) &&
            StringComparer.Ordinal.Equals(candidate.Family.FamilyVersion, reference.FamilyVersion) &&
            StringComparer.Ordinal.Equals(
                candidate.Family.FamilyContentHash,
                reference.FamilyContentHash));
        FirmwareMetadataStructureDefinition[] matches = family?.Family.MetadataSets
            .SelectMany(static set => set.Structures)
            .Where(structure => StringComparer.Ordinal.Equals(
                structure.Definition.DefinitionId,
                reference.StructureId))
            .Select(static structure => structure.Definition)
            .ToArray() ?? [];
        definition = matches.Length > 0 &&
                     matches.All(candidate => ReferenceEquals(candidate, matches[0]))
            ? matches[0]
            : null;
        return definition is not null;
    }

    internal static string FormatCapacities(IEnumerable<long> capacities)
    {
        return string.Join(" / ", capacities.Select(static capacity => $"0x{capacity:X}"));
    }

    internal V2CompositionPlanCompileResult CompileExecutable(
        string profileId,
        string profileVersion,
        string icId,
        string experienceId,
        long? requestedMapCapacity,
        string failureMessage,
        IReadOnlyCollection<string>? selectedInputSlotIds = null)
    {
        V2CompositionPlanCompileResult compilation = Compile(
            profileId,
            profileVersion,
            icId,
            experienceId,
            requestedMapCapacity,
            requestedTopology: null,
            resolutionArtifacts: [],
            selectedInputSlotIds);
        return compilation.CompiledComposition is { Eligibility: CompiledCompositionEligibility.V2RuntimeExecutable }
            ? compilation
            : V2CompositionPlanCompileResult.Failed(
            compilation.Issues.Count == 0
                ? [new CompositionIssue(CompilationFailed, failureMessage)]
                : compilation.Issues);
    }

    internal IReadOnlyList<string> GetInputSelectionGroupMemberSlotIds(
        string profileId,
        string profileVersion)
    {
        try
        {
            TrustedCompositionProfileCatalogEntry? entry = _catalog.Value.SelectProfile(
                profileId,
                profileVersion,
                out _);
            return entry is not null
                ? Array.AsReadOnly(
                [
                    .. entry.Profile.InputSelectionGroups
                        .SelectMany(static group => group.MemberSlotIds)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal),
                ])
                : [];
        }
        catch (Exception exception) when (IsBundleLoadFailure(exception))
        {
            return [];
        }
    }

    /// <summary>
    /// Compiles the narrowly admitted AB Code function-open route. It may run a
    /// profile whose only remaining blockers are direct-golden certification or
    /// firmware-owner review; every other candidate remains non-executable.
    /// </summary>
    internal V2CompositionPlanCompileResult CompileAbMergeFunctionOpen(
        string profileId,
        string profileVersion,
        string icId,
        long? requestedMapCapacity,
        TopologySelection? requestedTopology,
        string failureMessage)
    {
        V2CompositionPlanCompileResult compilation = Compile(
            profileId,
            profileVersion,
            icId,
            ExperienceIds.AbMerge,
            requestedMapCapacity,
            requestedTopology,
            []);
        return compilation.CompiledComposition is { } composition &&
               (composition.Eligibility == CompiledCompositionEligibility.V2RuntimeExecutable ||
                composition.IsV2AbFunctionOpenCandidate)
            ? compilation
            : V2CompositionPlanCompileResult.Failed(
            compilation.Issues.Count == 0
                ? [new CompositionIssue(CompilationFailed, failureMessage)]
                : compilation.Issues);
    }

    internal V2CompositionPlanCompileResult Compile(
        string profileId,
        string profileVersion,
        string icId,
        string experienceId,
        long? requestedMapCapacity,
        IReadOnlyList<FirmwareArtifactPayload> resolutionArtifacts)
    {
        return Compile(
            profileId,
            profileVersion,
            icId,
            experienceId,
            requestedMapCapacity,
            requestedTopology: null,
            resolutionArtifacts);
    }

    internal V2CompositionPlanCompileResult Compile(
        string profileId,
        string profileVersion,
        string icId,
        string experienceId,
        long? requestedMapCapacity,
        TopologySelection? requestedTopology,
        IReadOnlyList<FirmwareArtifactPayload> resolutionArtifacts,
        IReadOnlyCollection<string>? selectedInputSlotIds = null)
    {
        ArgumentNullException.ThrowIfNull(resolutionArtifacts);
        try
        {
            return _catalog.Value.Compile(
                profileId,
                profileVersion,
                icId,
                experienceId,
                requestedMapCapacity,
                requestedTopology,
                resolutionArtifacts,
                selectedInputSlotIds);
        }
        catch (Exception exception) when (IsBundleLoadFailure(exception))
        {
            return V2CompositionPlanCompileResult.Failed([CreateBundleLoadIssue(exception)]);
        }
    }

    internal V2CompositionPlanCompileResult CompileLogicalOutput(
        string profileId,
        string profileVersion,
        string memberId,
        V2LogicalOutputCompileRequest request)
    {
        try
        {
            return _catalog.Value.CompileLogicalOutput(
                profileId,
                profileVersion,
                memberId,
                request);
        }
        catch (Exception exception) when (IsBundleLoadFailure(exception))
        {
            return V2CompositionPlanCompileResult.Failed([CreateBundleLoadIssue(exception)]);
        }
    }

    internal V2CompositionPlanCompileResult CompileRuntimeReferenceReplace(
        string profileId,
        string profileVersion,
        string memberId,
        string experienceId,
        TopologySelection? requestedTopology,
        V2RuntimeReferenceReplaceCompileRequest request)
    {
        return CompileRuntimeReferenceReplace(
            profileId,
            profileVersion,
            memberId,
            experienceId,
            requestedTopology,
            [],
            request);
    }

    internal V2CompositionPlanCompileResult CompileRuntimeReferenceReplace(
        string profileId,
        string profileVersion,
        string memberId,
        string experienceId,
        TopologySelection? requestedTopology,
        IReadOnlyList<FirmwareArtifactPayload> resolutionArtifacts,
        V2RuntimeReferenceReplaceCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(resolutionArtifacts);
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return _catalog.Value.CompileRuntimeReferenceReplace(
                profileId,
                profileVersion,
                memberId,
                experienceId,
                requestedTopology,
                resolutionArtifacts,
                request);
        }
        catch (Exception exception) when (IsBundleLoadFailure(exception))
        {
            return V2CompositionPlanCompileResult.Failed([CreateBundleLoadIssue(exception)]);
        }
    }

    internal IReadOnlyList<long> GetMapCapacities(
        string profileId,
        string profileVersion,
        string icId,
        string experienceId,
        out IReadOnlyList<CompositionIssue> issues)
    {
        try
        {
            return _catalog.Value.GetMapCapacities(
                profileId,
                profileVersion,
                icId,
                experienceId,
                out issues);
        }
        catch (Exception exception) when (IsBundleLoadFailure(exception))
        {
            issues = [CreateBundleLoadIssue(exception)];
            return [];
        }
    }

    /// <summary>Reads exact canonical map references declared by one trusted profile.</summary>
    internal IReadOnlyList<FirmwareImageMap> GetMapVariants(
        string profileId,
        string profileVersion,
        string icId,
        string experienceId,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return GetMapVariants(
            profileId,
            profileVersion,
            icId,
            experienceId,
            out _,
            out issues);
    }

    /// <summary>Reads one profile's IC Count input mode and exact canonical map references.</summary>
    internal IReadOnlyList<FirmwareImageMap> GetMapVariants(
        string profileId,
        string profileVersion,
        string icId,
        string experienceId,
        out IcNumberInputMode? icNumberInputMode,
        out IReadOnlyList<CompositionIssue> issues)
    {
        try
        {
            return _catalog.Value.GetMapVariants(
                profileId,
                profileVersion,
                icId,
                experienceId,
                out icNumberInputMode,
                out issues);
        }
        catch (Exception exception) when (IsBundleLoadFailure(exception))
        {
            icNumberInputMode = null;
            issues = [CreateBundleLoadIssue(exception)];
            return [];
        }
    }

    /// <summary>
    /// Projects one trusted profile's metadata bindings into Application plan
    /// references without copying locators, fields, ranges, or formatter facts.
    /// </summary>
    internal MetadataPlanDefinition CreateMetadataPlan(
        string profileId,
        string profileVersion,
        CompiledComposition composition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        ArgumentNullException.ThrowIfNull(composition);
        if (!StringComparer.Ordinal.Equals(composition.V2Details.ProfileId, profileId) ||
            !StringComparer.Ordinal.Equals(
                composition.V2Details.ProfileVersion,
                profileVersion))
        {
            throw new InvalidDataException(
                "Metadata plans require the exact compiled trusted profile and resolved map.");
        }

        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap =
            composition.V2Details.Provenance.ResolvedMap;

        TrustedCompositionProfileCatalogEntry profileEntry =
            _catalog.Value.Profiles.Single(entry =>
                StringComparer.Ordinal.Equals(
                    entry.Profile.ProfileId,
                    profileId) &&
                StringComparer.Ordinal.Equals(
                    entry.Profile.ProfileVersion,
                    profileVersion));
        V2CompositionProfileDefinition profile =
            profileEntry.Profile;
        FirmwareFamilyResolutionDefinition family =
            profileEntry.Family.Family;
        MetadataPlanEntry[] entries =
        [
            .. profile.MetadataBindings.Select(binding =>
                CreateMetadataPlanEntry(
                    family,
                    resolvedMap,
                    profile,
                    binding)),
        ];
        return new MetadataPlanDefinition(
            entries,
            new MetadataPlanSourceIdentity(
                profileId,
                profileVersion,
                ContentHash));
    }

    /// <summary>Returns whether one trusted profile declares an exact metadata purpose.</summary>
    internal bool ProfileDeclaresMetadataPurpose(
        string profileId,
        string profileVersion,
        CompositionProfileMetadataPurpose purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        return _catalog.Value.Profiles
            .Single(entry =>
                StringComparer.Ordinal.Equals(
                    entry.Profile.ProfileId,
                    profileId) &&
                StringComparer.Ordinal.Equals(
                    entry.Profile.ProfileVersion,
                    profileVersion))
            .Profile.MetadataBindings.Any(binding =>
                binding.Purposes.Contains(purpose));
    }

    private static MetadataPlanEntry CreateMetadataPlanEntry(
        FirmwareFamilyResolutionDefinition family,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        V2CompositionProfileDefinition profile,
        CompositionProfileMetadataBinding binding)
    {
        if (!family.TryResolveStructure(
                resolvedMap.ImageMap.MapId,
                binding.StructureId,
                out FirmwareMetadataStructure? structure))
        {
            throw new InvalidDataException(
                $"Metadata binding '{binding.BindingId}' references a structure not selected by the compiled map.");
        }

        FirmwareMapFactBinding<FirmwareMetadataSet>[] metadataBindings =
        [
            .. resolvedMap.ImageMap.MetadataSetBindings.Where(
                candidate =>
                    StringComparer.Ordinal.Equals(
                        candidate.EffectiveKey.MemberId,
                        resolvedMap.MemberId) &&
                    candidate.Value.Structures.Any(
                        candidateStructure =>
                            ReferenceEquals(candidateStructure, structure))),
        ];
        if (metadataBindings.Length != 1)
        {
            throw new InvalidDataException(
                $"Metadata binding '{binding.BindingId}' does not resolve to exactly one canonical map fact.");
        }

        InputArtifactProfileSpace space = profile.Spaces
            .OfType<InputArtifactProfileSpace>()
            .Single(candidate => StringComparer.Ordinal.Equals(
                candidate.SpaceId,
                binding.SpaceId));
        return new MetadataPlanEntry(
            binding.BindingId,
            binding.SpaceId,
            space.SlotId,
            family,
            resolvedMap,
            metadataBindings[0],
            structure,
            binding.TargetReferences,
            binding.Purposes.Select(ToReferencePurpose),
            binding.EvidenceRefs);
    }

    private static MetadataReferencePurpose ToReferencePurpose(
        CompositionProfileMetadataPurpose purpose)
    {
        return purpose switch
        {
            CompositionProfileMetadataPurpose.MapResolution =>
                MetadataReferencePurpose.MapResolution,
            CompositionProfileMetadataPurpose.Validation =>
                MetadataReferencePurpose.Validation,
            CompositionProfileMetadataPurpose.OutputNaming =>
                MetadataReferencePurpose.OutputNaming,
            CompositionProfileMetadataPurpose.Display =>
                MetadataReferencePurpose.Display,
            CompositionProfileMetadataPurpose.Version =>
                MetadataReferencePurpose.Version,
            CompositionProfileMetadataPurpose.Inspection =>
                MetadataReferencePurpose.Inspection,
            CompositionProfileMetadataPurpose.Formatting =>
                MetadataReferencePurpose.Formatting,
            CompositionProfileMetadataPurpose.Copy =>
                MetadataReferencePurpose.Copy,
            CompositionProfileMetadataPurpose.Relocation =>
                MetadataReferencePurpose.Relocation,
            CompositionProfileMetadataPurpose.Integrity =>
                MetadataReferencePurpose.Integrity,
            CompositionProfileMetadataPurpose.Processor =>
                MetadataReferencePurpose.Processor,
            CompositionProfileMetadataPurpose.MemoryProjection =>
                MetadataReferencePurpose.MemoryProjection,
            CompositionProfileMetadataPurpose.ReportClassification =>
                MetadataReferencePurpose.ReportClassification,
            _ => throw new InvalidDataException(
                "Unknown trusted profile metadata purpose."),
        };
    }

    private static bool IsBundleLoadFailure(Exception exception)
    {
        return exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            ProfileBundleManifestNormalizationException or
            CompositionProfileNormalizationException or
            TrustedProfileBundleCatalogException;
    }

    private CompositionIssue CreateBundleLoadIssue(Exception exception)
    {
        return exception is TrustedProfileBundleCatalogException catalog
            ? new CompositionIssue(
                catalog.Code,
                $"The built-in V2 bundle '{RelativeRoot}' rejected catalog entry '{catalog.EntryId}' at '{catalog.EntryPath}' ({catalog.SemanticPath ?? "$"}): {catalog.Message}")
            : new CompositionIssue(
                "profile.v2.builtin-bundle-load-failed",
                $"The built-in V2 bundle '{RelativeRoot}' could not be loaded: {exception.Message}");
    }

    private TrustedProfileBundleCatalog LoadCatalog()
    {
        string bundleRoot = Path.Combine(AppContext.BaseDirectory, RelativeRoot);
        TrustedProfileBundle bundle = ProfileBundleLoader.Load(
            bundleRoot,
            "profile-bundle.json",
            new ProfileBundleTrustAnchor(ContentHash, _trustAnchorBindingId),
            new ProfileBundleLoadLimits(
                maximumManifestBytes: 16384,
                maximumJsonDepth: 32,
                new ProfileBundleEntrySnapshotLimits(16, 131072, 262144, 8)));
        return StringComparer.Ordinal.Equals(bundle.Manifest.BundleVersion, _bundleVersion)
            ? TrustedProfileBundleCatalogProjection.Create(
                bundle.CreateDocumentProjection(),
                BuiltInCanonicalMetadataDefinitionResolver.Instance)
            : throw new InvalidDataException(
                $"Bundle version '{bundle.Manifest.BundleVersion}' does not match the package trust index.");
    }
}
