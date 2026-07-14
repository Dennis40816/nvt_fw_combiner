using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Profiles-owned compiler entry point for one exact trusted V2 profile selection.</summary>
internal static class TrustedV2CompositionCompiler
{
    private const string SelectionUnresolved = "profile.v2.compile.selection-unresolved";
    private const string ProfileExperienceMismatch = "profile.v2.compile.profile-experience-mismatch";
    private const string MapSelectionInvalid = "profile.v2.compile.map-selection-invalid";
    private const string MapCapacityRequired = "profile.v2.compile.map-capacity-required";
    private const string MapCapacityUnavailable = "profile.v2.compile.map-capacity-unavailable";
    private const string PreparationNotAdmitted = "profile.v2.compile.preparation-not-admitted";

    /// <summary>Compiles one trusted logical-output General Merge request without resolving a physical image map.</summary>
    internal static V2CompositionPlanCompileResult CompileLogicalOutput(
        TrustedProfileBundleCatalog catalog,
        string profileId,
        string profileVersion,
        string memberId,
        V2LogicalOutputCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(catalog);
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

        TrustedProfileBundleCatalog.ProfileSelectionResult selectionResult = catalog.SelectProfile(profileId, profileVersion);
        if (selectionResult.Selection is not { } selection)
        {
            return Failed(
                selectionResult.Issues,
                "profile.v2.logical.selection-unresolved",
                "The selected trusted logical-output profile could not be resolved from its catalog.");
        }

        V2LogicalOutputPreparationResult preparation = V2LogicalOutputPreparationService.Prepare(
            catalog,
            selection,
            memberId);
        return preparation.IsAdmitted
            ? V2CompositionPlanCompiler.CompileLogicalOutput(preparation, request)
            : Failed(
                preparation.Issues,
                "profile.v2.logical.preparation-not-admitted",
                "The selected trusted V2 profile was not admitted for logical-output lowering.");
    }

    /// <summary>Compiles one trusted map-bound General Replace request through the shared resolved-map preparation path.</summary>
    internal static V2CompositionPlanCompileResult CompileRuntimeReferenceReplace(
        TrustedProfileBundleCatalog catalog,
        string profileId,
        string profileVersion,
        string memberId,
        V2RuntimeReferenceReplaceCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryResolveMapCandidates(
                catalog,
                profileId,
                profileVersion,
                memberId,
                ExperienceIds.GeneralReplace,
                out TrustedProfileBundleCatalog.ProfileSelection? selection,
                out FirmwareImageMap[] mapCandidates,
                out IReadOnlyList<CompositionIssue> resolutionIssues))
        {
            return V2CompositionPlanCompileResult.Failed(resolutionIssues);
        }

        if (!catalog.TryResolveSelection(selection!, out TrustedCompositionProfileCatalogEntry? profileEntry) ||
            !V2CompositionPlanCompiler.TryGetRuntimeReferenceReplaceReferenceSlotId(
                profileEntry.Profile,
                out string referenceSlotId))
        {
            return Failed(
                [],
                "profile.v2.runtime-reference-replace.profile-shape-invalid",
                "Runtime reference-replace compilation requires the closed reference-image profile shape.");
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

        mapCandidates =
        [
            .. mapCandidates.Where(map => map.CapacityBytes == referenceBindings[0].ExactLengthBytes),
        ];
        if (mapCandidates.Length != 1)
        {
            return Failed(
                [],
                MapSelectionInvalid,
                "The selected trusted V2 profile must identify exactly one canonical image map for the requested runtime reference-replace capacity.");
        }

        V2CompositionPreparationResult preparation = V2CompositionPreparationService.Prepare(
            catalog,
            new V2CompositionPreparationRequest(
                selection!,
                new FirmwareMapResolutionInputs(
                    memberId,
                    ExperienceIds.GeneralReplace,
                    mapCandidates[0].CapacityBytes,
                    requestedTopology: null,
                    [])));
        return preparation.IsAdmitted
            ? V2CompositionPlanCompiler.CompileRuntimeReferenceReplace(preparation, request)
            : Failed(
                preparation.Issues,
                PreparationNotAdmitted,
                "The selected trusted V2 profile was not admitted to its canonical image map.");
    }

    internal static V2CompositionPlanCompileResult Compile(
        TrustedProfileBundleCatalog catalog,
        string profileId,
        string profileVersion,
        string memberId,
        string modeId,
        long? requestedMapCapacity = null)
    {
        if (!TryResolveMapCandidates(
                catalog,
                profileId,
                profileVersion,
                memberId,
                modeId,
                out TrustedProfileBundleCatalog.ProfileSelection? selection,
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

        if (mapCandidates.Length != 1)
        {
            return Failed(
                [],
                requestedMapCapacity is null ? MapCapacityRequired : MapSelectionInvalid,
                requestedMapCapacity is null
                    ? "The selected trusted V2 profile requires one exact map capacity."
                    : "The selected trusted V2 profile must identify exactly one canonical image map for the requested capacity.");
        }

        V2CompositionPreparationResult preparation = V2CompositionPreparationService.Prepare(
            catalog,
            new V2CompositionPreparationRequest(
                selection!,
                new FirmwareMapResolutionInputs(
                    memberId,
                    modeId,
                    mapCandidates[0].CapacityBytes,
                    requestedTopology: null,
                    [])));
        return preparation.IsAdmitted
            ? V2CompositionPlanCompiler.Compile(preparation)
            : Failed(
                preparation.Issues,
                PreparationNotAdmitted,
                "The selected trusted V2 profile was not admitted to its canonical image map.");
    }

    /// <summary>Returns the trusted profile's eligible canonical map capacities without selecting one.</summary>
    internal static IReadOnlyList<long> GetMapCapacities(
        TrustedProfileBundleCatalog catalog,
        string profileId,
        string profileVersion,
        string memberId,
        string modeId,
        out IReadOnlyList<CompositionIssue> issues)
    {
        if (!TryResolveMapCandidates(
                catalog,
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

    private static bool TryResolveMapCandidates(
        TrustedProfileBundleCatalog catalog,
        string profileId,
        string profileVersion,
        string memberId,
        string modeId,
        out TrustedProfileBundleCatalog.ProfileSelection? selection,
        out FirmwareImageMap[] mapCandidates,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modeId);

        selection = null;
        mapCandidates = [];
        issues = [];
        TrustedProfileBundleCatalog.ProfileSelectionResult selectionResult = catalog.SelectProfile(
            profileId,
            profileVersion);
        if (selectionResult.Selection is not { } selected ||
            !catalog.TryResolveSelection(selected, out TrustedCompositionProfileCatalogEntry? profileEntry))
        {
            issues = selectionResult.Issues.Count == 0
                ? [new CompositionIssue(
                    SelectionUnresolved,
                    "The selected trusted V2 profile could not be resolved from its catalog.")]
                : [.. selectionResult.Issues];
            return false;
        }

        if (!StringComparer.Ordinal.Equals(profileEntry.Profile.Experience.ExperienceId, modeId))
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
            .. profileEntry.Family.Family.ImageMaps.Where(map =>
                profileEntry.Profile.MapBinding.MapIds.Contains(map.MapId, StringComparer.Ordinal) &&
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

        selection = selected;
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
