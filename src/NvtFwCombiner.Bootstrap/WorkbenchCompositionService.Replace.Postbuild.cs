using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryGetPostbuildProfile(
        string icId,
        string basePath,
        out LegacyCombinerPostbuildProfile? postbuildProfile,
        out CompositionIssue? issue,
        byte[]? baseImage = null)
    {
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = GetPostbuildProfiles(icId);
        if (profiles.Count == 0)
        {
            postbuildProfile = null;
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamPostbuildProfileMissing,
                $"No legacy Combiner postbuild profile is registered for {icId}.",
                "postbuild");
            return false;
        }

        string? commonFwVersion = null;
        if (profiles.Any(static profile => profile.CommonFwVersionRule is not null) &&
            !(baseImage is null
                ? TryReadBaseCommonFwVersion(icId, basePath, out commonFwVersion)
                : TryReadFirmwareConfigBackupMetadata(icId, baseImage, out FirmwareConfigMetadata metadata) && metadata.IsFirmwareVersionBarValid &&
                    (commonFwVersion = metadata.CommonFwVersion) is not null))
        {
            postbuildProfile = null;
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamPostbuildCategoryUnknown,
                $"{icId} has multiple legacy Combiner postbuild categories, but the base BIN FWConfig Common FW version could not be read or failed FW/bar validation.",
                WorkbenchSlotIds.ReplaceBase);
            return false;
        }

        if (!TrySelectPostbuildProfileByCommonFwVersion(
                icId,
                commonFwVersion,
                out postbuildProfile,
                out string? profileIssue))
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamPostbuildCategoryUnsupported,
                profileIssue ?? $"No legacy Combiner postbuild profile is registered for {icId}.",
                "postbuild");
            return false;
        }

        issue = null;
        return true;
    }
}
