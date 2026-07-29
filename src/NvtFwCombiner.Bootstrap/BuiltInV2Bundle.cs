using System.Collections.Frozen;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Bundles;
using NvtFwCombiner.Profiles;
using NvtFwCombiner.Profiles.FirmwareFamilies;
using NvtFwCombiner.Profiles.V2;
using V2CompositionProfileDefinition = NvtFwCombiner.Profiles.V2.CompositionProfileDefinition;

namespace NvtFwCombiner.Bootstrap;

internal static class BuiltInV2BundleRegistry
{
    internal static FrozenDictionary<string, BuiltInV2Bundle> All { get; } =
        new (string Directory, string ContentHash)[]
        {
            ("nt51917-nt51927-general-merge-logical-candidate", "3dd5c0adb73b7ee5b0c0762e79ab8ddfa800696a0646138d15fba8984d84d2eb"),
            ("nt51917-ctrlram-replace-alias-candidate", "8992dbc5483054c5dc16e545444b1f94446c698c68b1abe7946efdb4d4ffb26b"),
            ("nt51919-nt51929-nt51932-ab-merge", "e7ad9ad8b4df72275ab25a617c76ffae20b84ba431880753b41b8c4fa0ef3f0a"),
            ("nt51919-nt51929-nt51932-general-merge-logical-candidate", "5659a4095a6fce9ab3f46f9415759f7aeba321adfddb891e52871b2d6acff4f8"),
            ("nt51923-nt51926-general-merge-logical-candidate", "26f12851f81d55bb88a0a0e18ab4f10f451747369e797efbc69fdbf05cdf5a96"),
            ("nt51923-ctrlram-replace-candidate", "8c1318f9e83a658028b1e0a07b2c38a28bcdeb6031d3a393d6b4912c2cdba14f"),
            ("nt51923-dp-replace", "9496b7d6296e06fac81f4ca73a49ac1d4154ef9edc4dcf078fe433efa046081e"),
            ("nt51923-standard-merge", "6bac75eb386ff08c3fa6970e54b3c1dca35722ddaeaf52b67068a127c4e85a96"),
            ("nt51926-ctrlram-replace-candidate", "25d5adc9697eacedcf238835da197b0359c41f8cc6d82110c181496038469529"),
            ("nt51927-ctrlram-replace-candidate", "d0c8a8775a35a01b52b8d8f32a93af0ac798067e2577d2420ab0dd65dd815d0f"),
            ("nt51927-dp-replace", "e4d93b721cf275341775614d6eaf46b6aad06e6eb46b3674770371e0b8d8a313"),
            ("nt51927-standard-merge", "631bf40e6f5f6aee14be7a5b834243def7c6a37cdb88f49e0d854471d5de6015"),
            ("nt51928-ctrlram-replace-candidate", "bba0e65221aff3ebbd4b06f83f38295b6e315eff0741fe68952e5844ae64c634"),
            ("nt51928-general-merge-logical-candidate", "a774a7622aedfac94fc045b56e7fe04902359ebe59747acd5486ed336b6d5da2"),
            ("nt51928-dp-replace", "19fa3f39c8341052a0614bcdf4e21638fe76f1a9c471888ae7586b21937c77c9"),
            ("nt51928-standard-merge", "63cc636b72a63e6bd34c7e45769bb21342b7885120f483ff70432e35350eadbb"),
            ("nt51929-ctrlram-replace-candidate", "ea9cf1fe05a1462ddff67ece4a037757375100b67d91da3eb1eac1dd0417a4a5"),
            ("nt51929-dp-replace", "169b9334a57328504fbe463c96dda1e8d749109896ae8d0143088b747b0ab596"),
            ("nt51929-standard-merge", "14a3b2808a5377af39b683fe44f60f152e9c7f4a15c18e5c9e264ad6ea2b0827"),
            ("nt51932-ctrlram-replace-candidate", "9a2c69c1b4bc4b5c047b9534c12f3e03b6be5492c9aa26eb626c9a657d101daf"),
            ("nt51950-ab-merge", "abdd907710be94470937f4f6ee9c250e9ec1f90c4cbd1d10134584ef15878206"),
            ("nt51950-ctrlram-replace-candidate", "d3f745c68d948e7e3a3a07d5717de2114742f881444076d93d2232343f98049e"),
            ("nt51951-ctrlram-replace-candidate", "f48429f505f71fbe7c258780dc1ef848c1d9a402d79906c1e24b3a1097192728"),
            ("nt51950-nt51951-general-merge-logical-candidate", "387e70efd2bfb4591852f700b5f0b1b3763b0fb7c5edd2cefa10d998b73b29b6"),
            ("nt51950-nt51951-standard-merge", "714bd7460c15da708be3a297ca8681bba6986262e5fbbc2589b3a8fab15779a1"),
        }.ToFrozenDictionary(
            static bundle => bundle.Directory,
            static bundle => new BuiltInV2Bundle(bundle.Directory, bundle.ContentHash),
            StringComparer.Ordinal);
}

internal sealed class BuiltInV2Bundle
{
    internal const string CompilationFailed = "profile.v2.builtin-compilation-failed";
    private const string BundleLoadFailed = "profile.v2.builtin-bundle-load-failed";
    private const string TrustAnchorBindingId = "built-in-profile-bundle-v2";
    private readonly Lazy<TrustedProfileBundleCatalog> _catalog;

    internal BuiltInV2Bundle(string bundleDirectory, string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        RelativeRoot = Path.Combine("profiles", "built-in", bundleDirectory);
        ContentHash = contentHash;
        _catalog = new Lazy<TrustedProfileBundleCatalog>(LoadCatalog);
    }

    internal string RelativeRoot { get; }

    internal string ContentHash { get; }

    /// <summary>Projects the exact trusted identity used by a General Merge Saved Rule v2 parent.</summary>
    internal SavedRuleV2ParentBinding GetGeneralMergeSavedRuleParentBinding(
        string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        TrustedCompositionProfileCatalogEntry profile =
            _catalog.Value.Profiles.Single(candidate =>
                StringComparer.Ordinal.Equals(
                    candidate.Profile.ProfileId,
                    profileId));
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
            WorkbenchGeneralMergeIds.LogicalOutputMapId);
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
        string failureMessage)
    {
        V2CompositionPlanCompileResult compilation = Compile(
            profileId,
            profileVersion,
            icId,
            experienceId,
            requestedMapCapacity,
            []);
        return compilation.CompiledComposition is { Eligibility: CompiledCompositionEligibility.V2RuntimeExecutable }
            ? compilation
            : V2CompositionPlanCompileResult.Failed(
            compilation.Issues.Count == 0
                ? [new CompositionIssue(CompilationFailed, failureMessage)]
                : compilation.Issues);
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
            IcWorkflowIds.AbMerge,
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
        IReadOnlyList<FirmwareArtifactPayload> resolutionArtifacts)
    {
        ArgumentNullException.ThrowIfNull(resolutionArtifacts);
        try
        {
            return TrustedV2CompositionCompiler.Compile(
                _catalog.Value,
                profileId,
                profileVersion,
                icId,
                experienceId,
                requestedMapCapacity,
                requestedTopology,
                resolutionArtifacts);
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
            return TrustedV2CompositionCompiler.CompileLogicalOutput(
                _catalog.Value,
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
            return TrustedV2CompositionCompiler.CompileRuntimeReferenceReplace(
                _catalog.Value,
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
            return TrustedV2CompositionCompiler.GetMapCapacities(
                _catalog.Value,
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
            return TrustedV2CompositionCompiler.GetMapVariants(
                _catalog.Value,
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
        if (!StringComparer.Ordinal.Equals(composition.ProfileId, profileId) ||
            !StringComparer.Ordinal.Equals(
                composition.ProfileVersion,
                profileVersion) ||
            composition.V2Details?.Provenance.ResolvedMap is not { } resolvedMap)
        {
            throw new InvalidDataException(
                "Metadata plans require the exact compiled trusted profile and resolved map.");
        }

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
        return entries.Length == 0
            ? MetadataPlanDefinition.Empty
            : new MetadataPlanDefinition(entries);
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
        return new CompositionIssue(
            BundleLoadFailed,
            $"The built-in V2 bundle '{RelativeRoot}' could not be loaded: {exception.Message}");
    }

    private TrustedProfileBundleCatalog LoadCatalog()
    {
        string bundleRoot = Path.Combine(AppContext.BaseDirectory, RelativeRoot);
        TrustedProfileBundle bundle = ProfileBundleLoader.Load(
            bundleRoot,
            "profile-bundle.json",
            new ProfileBundleTrustAnchor(ContentHash, TrustAnchorBindingId),
            new ProfileBundleLoadLimits(
                maximumManifestBytes: 16384,
                maximumJsonDepth: 32,
                new ProfileBundleEntrySnapshotLimits(16, 131072, 262144, 8)));
        return TrustedProfileBundleCatalogProjection.Create(
            bundle.CreateDocumentProjection(),
            BuiltInCanonicalMetadataDefinitionResolver.Instance);
    }
}
