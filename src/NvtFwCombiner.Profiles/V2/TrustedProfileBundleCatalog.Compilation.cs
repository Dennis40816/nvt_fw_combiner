using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Compiler entry points owned by the exact trusted catalog snapshot they select from.</summary>
internal sealed partial class TrustedProfileBundleCatalog
{
    private const string SelectionUnresolved = "profile.v2.compile.selection-unresolved";
    private const string ProfileExperienceMismatch = "profile.v2.compile.profile-experience-mismatch";
    private const string MapSelectionInvalid = "profile.v2.compile.map-selection-invalid";
    private const string MapCapacityRequired = "profile.v2.compile.map-capacity-required";
    private const string MapCapacityUnavailable = "profile.v2.compile.map-capacity-unavailable";
    private const string TopologyNotDeclared = "profile.v2.compile.topology-not-declared";
    private const string PreparationNotAdmitted = "profile.v2.compile.preparation-not-admitted";
    private const string RuntimeReferenceResolutionArtifactInvalid =
        "profile.v2.runtime-reference-replace.resolution-artifact-invalid";

    /// <summary>Compiles one trusted logical-output General Merge request without resolving a physical image map.</summary>
    internal V2CompositionPlanCompileResult CompileLogicalOutput(
        string profileId,
        string profileVersion,
        string memberId,
        V2LogicalOutputCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(profileId) ||
            string.IsNullOrWhiteSpace(profileVersion) ||
            string.IsNullOrWhiteSpace(memberId))
        {
            return Failed(
                [],
                "profile.v2.logical.selection-invalid",
                "Logical-output compilation requires profile identity and member selections.");
        }

        TrustedCompositionProfileCatalogEntry? profileEntry = SelectProfile(
            profileId,
            profileVersion,
            out IReadOnlyList<CompositionIssue> selectionIssues);
        return profileEntry is null
            ? Failed(
                selectionIssues,
                "profile.v2.logical.selection-unresolved",
                "The selected trusted logical-output profile could not be resolved from its catalog.")
            : V2CompositionPlanCompiler.CompileLogicalOutput(
                BundleIdentity,
                profileEntry,
                memberId,
                request);
    }

    /// <summary>Compiles one trusted runtime reference-replace request with optional immutable map-resolution evidence.</summary>
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
        if (!TryResolveMapCandidates(
                profileId,
                profileVersion,
                memberId,
                experienceId,
                out TrustedCompositionProfileCatalogEntry? profileEntry,
                out FirmwareImageMap[] mapCandidates,
                out IReadOnlyList<CompositionIssue> resolutionIssues))
        {
            return V2CompositionPlanCompileResult.Failed(resolutionIssues);
        }

        if (!V2CompositionPlanCompiler.TryGetRuntimeReferenceReplaceSelectionShape(
                profileEntry.Profile,
                out string referenceSlotId,
                out bool allowsTopologyDisambiguation))
        {
            return Failed(
                [],
                "profile.v2.runtime-reference-replace.profile-shape-invalid",
                "Runtime reference-replace compilation requires the closed reference-image profile shape.");
        }

        if (requestedTopology is not null && !allowsTopologyDisambiguation)
        {
            return Failed(
                [],
                "profile.v2.runtime-reference-replace.topology-not-admitted",
                "Only a topology-disambiguating runtime reference-replace profile can use an explicit topology selection.");
        }

        V2RuntimeReferenceReplaceInputBinding[] referenceBindings =
        [
            .. request.Bindings.Where(binding =>
                binding is not null &&
                StringComparer.Ordinal.Equals(binding.SlotId, referenceSlotId)),
        ];
        if (referenceBindings.Length != 1 || referenceBindings[0].ExactLengthBytes <= 0)
        {
            return Failed(
                [],
                "profile.v2.runtime-reference-replace.reference-length-invalid",
                "Runtime reference-replace compilation requires exactly one positive-length reference-image binding to select its canonical map.");
        }

        FirmwareArtifactPayload[] artifactSnapshots = [.. resolutionArtifacts];
        bool artifactsAreValid = allowsTopologyDisambiguation
            ? artifactSnapshots.Length <= 1 &&
              (artifactSnapshots.Length == 0 ||
               (StringComparer.Ordinal.Equals(
                    artifactSnapshots[0].ArtifactId,
                    referenceBindings[0].BindingId) &&
                artifactSnapshots[0].LengthBytes == referenceBindings[0].ExactLengthBytes))
            : artifactSnapshots.Length == 0;
        if (!artifactsAreValid)
        {
            return Failed(
                [],
                RuntimeReferenceResolutionArtifactInvalid,
                "Only a topology-disambiguating runtime reference-replace profile may supply one immutable map-resolution artifact matching the reference binding identity and length.");
        }

        mapCandidates =
        [
            .. mapCandidates.Where(map => map.CapacityBytes == referenceBindings[0].ExactLengthBytes),
        ];
        if (mapCandidates.Length == 0 ||
            ((!allowsTopologyDisambiguation || requestedTopology is null) && mapCandidates.Length != 1))
        {
            return Failed(
                [],
                MapSelectionInvalid,
                "The selected trusted V2 profile must identify a canonical image map for the requested runtime reference-replace capacity and topology.");
        }

        bool isAdmitted = V2CompositionPlanCompiler.TryCompileRuntimeReferenceReplaceAdmitted(
            this,
            profileEntry,
            new FirmwareMapResolutionInputs(
                memberId,
                experienceId,
                referenceBindings[0].ExactLengthBytes,
                requestedTopology,
                artifactSnapshots),
            request,
            out V2CompositionPlanCompileResult? compilation,
            out IReadOnlyList<CompositionIssue> preparationIssues);
        return isAdmitted
            ? compilation!
            : Failed(
                preparationIssues,
                PreparationNotAdmitted,
                "The selected trusted V2 profile was not admitted to its canonical image map.");
    }

    /// <summary>Compiles one trusted map-bound profile with an optional typed topology selection.</summary>
    internal V2CompositionPlanCompileResult Compile(
        string profileId,
        string profileVersion,
        string memberId,
        string modeId,
        long? requestedMapCapacity,
        TopologySelection? requestedTopology,
        IReadOnlyList<FirmwareArtifactPayload> resolutionArtifacts,
        IReadOnlyCollection<string>? selectedInputSlotIds = null)
    {
        ArgumentNullException.ThrowIfNull(resolutionArtifacts);
        if (!TryResolveMapCandidates(
                profileId,
                profileVersion,
                memberId,
                modeId,
                out TrustedCompositionProfileCatalogEntry? selectedProfile,
                out FirmwareImageMap[] mapCandidates,
                out IReadOnlyList<CompositionIssue> resolutionIssues))
        {
            return V2CompositionPlanCompileResult.Failed(resolutionIssues);
        }

        if (requestedMapCapacity is { } capacity)
        {
            mapCandidates =
            [
                .. mapCandidates.Where(map => map.CapacityBytes == capacity),
            ];
            if (mapCandidates.Length == 0)
            {
                return Failed(
                    [],
                    MapCapacityUnavailable,
                    "The selected trusted V2 profile has no canonical image map for the requested capacity.");
            }
        }

        if (requestedTopology is not null)
        {
            if (mapCandidates.All(static map =>
                    map.Applicability.TopologyRequirement.Kind == TopologyRequirementKind.None))
            {
                return Failed(
                    [],
                    TopologyNotDeclared,
                    "The selected trusted V2 map does not declare a topology selection.");
            }

            mapCandidates =
            [
                .. mapCandidates.Where(map =>
                    map.Applicability.TopologyRequirement.Kind != TopologyRequirementKind.None &&
                    map.Applicability.TopologyRequirement.Matches(requestedTopology)),
            ];
        }

        if (mapCandidates.Length > 1 &&
            requestedMapCapacity is null &&
            selectedProfile.Profile.InputSelectionGroups.Count != 0)
        {
            mapCandidates =
            [
                .. mapCandidates
                    .Where(map => MapSupportsSelectedOptionalSlots(
                        selectedProfile.Profile,
                        map,
                        selectedInputSlotIds ?? []))
                    .OrderBy(static map => map.CapacityBytes)
                    .ThenBy(static map => map.MapId, StringComparer.Ordinal)
                    .Take(1),
            ];
        }

        if (mapCandidates.Length != 1)
        {
            return Failed(
                [],
                requestedMapCapacity is null ? MapCapacityRequired : MapSelectionInvalid,
                requestedMapCapacity is null
                    ? "The selected trusted V2 profile requires one exact map capacity."
                    : "The selected trusted V2 profile must identify exactly one canonical image map for the requested capacity.");
        }

        bool isAdmitted = V2CompositionPlanCompiler.TryCompileAdmitted(
            this,
            selectedProfile,
            new FirmwareMapResolutionInputs(
                memberId,
                modeId,
                mapCandidates[0].CapacityBytes,
                requestedTopology,
                resolutionArtifacts),
            selectedInputSlotIds,
            out V2CompositionPlanCompileResult? compilation,
            out IReadOnlyList<CompositionIssue> preparationIssues);
        return isAdmitted
            ? compilation!
            : Failed(
                preparationIssues,
                PreparationNotAdmitted,
                "The selected trusted V2 profile was not admitted to its canonical image map.");
    }

    private static bool MapSupportsSelectedOptionalSlots(
        CompositionProfileDefinition profile,
        FirmwareImageMap map,
        IReadOnlyCollection<string> selectedInputSlotIds)
    {
        var regions = map.Regions.Select(static region => region.RegionId).ToHashSet(StringComparer.Ordinal);
        var selected = selectedInputSlotIds.ToHashSet(StringComparer.Ordinal);
        foreach (InputSelectionGroupDefinition group in profile.InputSelectionGroups)
        {
            foreach (string slotId in group.MemberSlotIds.Where(selected.Contains))
            {
                string[] inputSpaceIds =
                [
                    .. profile.Spaces
                        .OfType<InputArtifactProfileSpace>()
                        .Where(space => StringComparer.Ordinal.Equals(space.SlotId, slotId))
                        .Select(static space => space.SpaceId),
                ];
                if (profile.Views
                    .Where(view => inputSpaceIds.Contains(view.SpaceId, StringComparer.Ordinal))
                    .Select(TryGetRegionId)
                    .Where(static regionId => regionId is not null)
                    .Any(regionId => !regions.Contains(regionId!)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static string? TryGetRegionId(CompositionProfileView view)
    {
        return view.Selector switch
        {
            MapRegionViewSelector region => region.RegionId,
            MapRegionSliceViewSelector slice => slice.RegionId,
            _ => null,
        };
    }

    /// <summary>Returns the trusted profile's eligible canonical map capacities without selecting one.</summary>
    internal IReadOnlyList<long> GetMapCapacities(
        string profileId,
        string profileVersion,
        string memberId,
        string modeId,
        out IReadOnlyList<CompositionIssue> issues)
    {
        if (!TryResolveMapCandidates(
                profileId,
                profileVersion,
                memberId,
                modeId,
                out _,
                out FirmwareImageMap[] mapCandidates,
                out issues))
        {
            return [];
        }

        issues = [];
        return Array.AsReadOnly(
        [
            .. mapCandidates
                .Select(static map => map.CapacityBytes)
                .Distinct()
                .Order(),
        ]);
    }

    /// <summary>Returns the profile IC Count input mode with its exact eligible map references.</summary>
    internal IReadOnlyList<FirmwareImageMap> GetMapVariants(
        string profileId,
        string profileVersion,
        string memberId,
        string modeId,
        out IcNumberInputMode? icNumberInputMode,
        out IReadOnlyList<CompositionIssue> issues)
    {
        if (!TryResolveMapCandidates(
                profileId,
                profileVersion,
                memberId,
                modeId,
                out TrustedCompositionProfileCatalogEntry? profileEntry,
                out FirmwareImageMap[] mapCandidates,
                out issues))
        {
            icNumberInputMode = null;
            return [];
        }

        icNumberInputMode = profileEntry.Profile.IcNumberInputMode;
        issues = [];
        return Array.AsReadOnly(
        [
            .. mapCandidates.OrderBy(static map => map.MapId, StringComparer.Ordinal),
        ]);
    }

    private bool TryResolveMapCandidates(
        string profileId,
        string profileVersion,
        string memberId,
        string modeId,
        [NotNullWhen(true)] out TrustedCompositionProfileCatalogEntry? profileEntry,
        out FirmwareImageMap[] mapCandidates,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modeId);

        profileEntry = null;
        mapCandidates = [];
        issues = [];
        TrustedCompositionProfileCatalogEntry? selected = SelectProfile(
            profileId,
            profileVersion,
            out IReadOnlyList<CompositionIssue> selectionIssues);
        if (selected is null)
        {
            issues = selectionIssues.Count == 0
                ? [new CompositionIssue(
                    SelectionUnresolved,
                    "The selected trusted V2 profile could not be resolved from its catalog.")]
                : [.. selectionIssues];
            return false;
        }

        if (!StringComparer.Ordinal.Equals(selected.Profile.ExperienceId, modeId))
        {
            issues =
            [
                new CompositionIssue(
                    ProfileExperienceMismatch,
                    "The requested mode does not match the selected trusted V2 profile experience."),
            ];
            return false;
        }

        mapCandidates =
        [
            .. selected.Family.Family.ImageMaps.Where(map =>
                selected.Profile.MapBinding.MapIds.Contains(map.MapId, StringComparer.Ordinal) &&
                map.Applicability.MemberIds.Contains(memberId, StringComparer.Ordinal) &&
                map.Applicability.ModeIds.Contains(modeId, StringComparer.Ordinal)),
        ];
        if (mapCandidates.Length == 0)
        {
            issues =
            [
                new CompositionIssue(
                    MapSelectionInvalid,
                    "The selected trusted V2 profile does not identify a canonical image map for the requested member and mode."),
            ];
            return false;
        }

        profileEntry = selected;
        return true;
    }

    private static V2CompositionPlanCompileResult Failed(
        IReadOnlyList<CompositionIssue> issues,
        string fallbackCode,
        string fallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(issues);
        return V2CompositionPlanCompileResult.Failed(issues.Count == 0
            ? [new CompositionIssue(fallbackCode, fallbackMessage)]
            : issues);
    }
}
