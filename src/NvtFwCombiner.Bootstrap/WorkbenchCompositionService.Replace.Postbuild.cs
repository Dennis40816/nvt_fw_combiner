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
        string? commonFwVersion = null;
        bool hasCommonFwVersion = baseImage is null
            ? TryReadBaseCommonFwVersion(icId, basePath, out commonFwVersion)
            : TryReadFirmwareConfigBackupMetadata(icId, baseImage, out FirmwareConfigMetadata metadata) &&
                (commonFwVersion = metadata.CommonFwVersion) is not null;
        return TryResolvePostbuildProfile(
            icId,
            profiles,
            hasCommonFwVersion,
            commonFwVersion,
            out postbuildProfile,
            out issue);
    }

    private static bool TryResolvePostbuildProfile(
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
                WorkbenchIssueCodes.ReplaceCtrlRamPostbuildProfileMissing,
                $"No legacy Combiner postbuild profile is registered for {icId}.",
                "postbuild");
            return false;
        }

        if (profiles.Count > 1 && !hasCommonFwVersion)
        {
            postbuildProfile = null;
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamPostbuildCategoryUnknown,
                $"{icId} has multiple runtime postbuild profiles, but the base BIN FWConfig Common FW version could not be read.",
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
