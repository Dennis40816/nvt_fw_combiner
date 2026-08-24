using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;

namespace NvtFwCombiner.Infrastructure.Composition;

internal static class BuiltInPostbuildProfileResolver
{
    internal static bool TryGetPostbuildProfile(
        ICompositionCapabilityExperience projection,
        string icId,
        string basePath,
        out LegacyCombinerPostbuildProfile? postbuildProfile,
        out CompositionIssue? issue,
        byte[]? baseImage = null)
    {
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles =
            BuiltInPostbuildProfileCatalog.GetProfiles(
                IcIdentifier.Normalize(icId));
        string? commonFwVersion = null;
        bool hasCommonFwVersion = baseImage is null
            ? BuiltInFirmwareInspection.TryReadBaseCommonFwVersion(projection, icId, basePath, out commonFwVersion)
            : BuiltInFirmwareInspection.TryReadFirmwareConfigBackupMetadata(projection, icId, baseImage, out FirmwareConfigMetadata metadata) &&
                (commonFwVersion = metadata.CommonFwVersion) is not null;
        return TryResolvePostbuildProfile(
            icId,
            profiles,
            hasCommonFwVersion,
            commonFwVersion,
            out postbuildProfile,
            out issue);
    }

    internal static bool TryResolvePostbuildProfile(
        string icId,
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles,
        bool hasCommonFwVersion,
        string? commonFwVersion,
        out LegacyCombinerPostbuildProfile? postbuildProfile,
        out CompositionIssue? issue)
    {
        if (profiles.Count == 0)
        {
            postbuildProfile = null;
            issue = new CompositionIssue(
                CompositionPlanningIssueCodes.ReplaceCtrlRamPostbuildProfileMissing,
                $"No legacy Combiner postbuild profile is registered for {icId}.",
                "postbuild");
            return false;
        }

        if (profiles.Count > 1 && !hasCommonFwVersion)
        {
            postbuildProfile = null;
            issue = new CompositionIssue(
                CompositionPlanningIssueCodes.ReplaceCtrlRamPostbuildCategoryUnknown,
                $"{icId} has multiple runtime postbuild profiles, but the base BIN FWConfig Common FW version could not be read.",
                CompositionSlotIds.ReplaceBase);
            return false;
        }

        if (!BuiltInPostbuildProfileCatalog.TrySelectProfileForCommonFwVersion(
                IcIdentifier.Normalize(icId),
                commonFwVersion,
                out postbuildProfile,
                out string? profileIssue))
        {
            issue = new CompositionIssue(
                CompositionPlanningIssueCodes.ReplaceCtrlRamPostbuildCategoryUnsupported,
                profileIssue ?? $"No legacy Combiner postbuild profile is registered for {icId}.",
                "postbuild");
            return false;
        }

        issue = null;
        return true;
    }
}
