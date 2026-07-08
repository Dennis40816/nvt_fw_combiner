using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryGetPostbuildProfile(
        string icId,
        string basePath,
        out LegacyCombinerPostbuildProfile? postbuildProfile,
        out CompositionIssue? issue)
    {
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = LegacyCombinerPostbuildCatalog.GetProfiles(icId);
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
        if (profiles.Count > 1 &&
            !TryReadBaseCommonFwVersion(icId, basePath, out commonFwVersion))
        {
            postbuildProfile = null;
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamPostbuildCategoryUnknown,
                $"{icId} has multiple legacy Combiner postbuild categories, but the base BIN FWConfig Common FW version could not be read or failed FW/bar validation.",
                WorkbenchSlotIds.ReplaceBase);
            return false;
        }

        if (!LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
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

    private static string FormatPostbuildCommandBlock(LegacyCombinerPostbuildCommandPlan commandPlan)
    {
        string firmwarePath = Path.Combine("output", commandPlan.Profile.FirmwareFileName);
        const string binDirectory = "BIN";
        return string.Join(
            Environment.NewLine,
            commandPlan.Commands.Select(command =>
                $"Combiner.exe {string.Join(' ', LegacyCombinerPostbuildCommandLineBuilder.CreateArguments(command, firmwarePath, binDirectory))}"));
    }
}
