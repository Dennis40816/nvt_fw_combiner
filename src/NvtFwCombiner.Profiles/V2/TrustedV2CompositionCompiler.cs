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

    internal static V2CompositionPlanCompileResult Compile(
        TrustedProfileBundleCatalog catalog,
        string profileId,
        string profileVersion,
        string memberId,
        string modeId,
        long? requestedMapCapacity = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modeId);

        TrustedProfileBundleCatalog.ProfileSelectionResult selectionResult = catalog.SelectProfile(
            profileId,
            profileVersion);
        if (selectionResult.Selection is not { } selection ||
            !catalog.TryResolveSelection(selection, out TrustedCompositionProfileCatalogEntry? profileEntry))
        {
            return Failed(
                selectionResult.Issues,
                SelectionUnresolved,
                "The selected trusted V2 profile could not be resolved from its catalog.");
        }

        if (!StringComparer.Ordinal.Equals(profileEntry.Profile.Experience.ExperienceId, modeId))
        {
            return Failed(
                [],
                ProfileExperienceMismatch,
                "The requested mode does not match the selected trusted V2 profile experience.");
        }

        FirmwareImageMap[] mapCandidates =
        [
            .. profileEntry.Family.Family.ImageMaps.Where(map =>
                profileEntry.Profile.MapBinding.MapIds.Contains(map.MapId, StringComparer.Ordinal) &&
                map.Applicability.MemberIds.Contains(memberId, StringComparer.Ordinal) &&
                map.Applicability.ModeIds.Contains(modeId, StringComparer.Ordinal)),
        ];
        if (mapCandidates.Length == 0)
        {
            return Failed(
                [],
                MapSelectionInvalid,
                "The selected trusted V2 profile does not identify a canonical image map for the requested member and mode.");
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
                selection,
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
