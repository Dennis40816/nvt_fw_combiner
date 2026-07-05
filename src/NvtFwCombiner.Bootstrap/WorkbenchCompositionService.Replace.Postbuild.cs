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
        out CompositionIssue? issue)
    {
        IReadOnlyList<LegacyCombinerPostbuildProfile> profiles = LegacyCombinerPostbuildCatalog.GetProfiles(icId);
        if (profiles.Count == 0)
        {
            postbuildProfile = null;
            issue = new CompositionIssue(
                "replace.ctrlram.postbuild-profile-missing",
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
                "replace.ctrlram.postbuild-category-unknown",
                $"{icId} has multiple legacy Combiner postbuild categories, but the base BIN FWConfig Common FW version could not be read or failed FW/bar validation.",
                "replace-base");
            return false;
        }

        if (!LegacyCombinerPostbuildCatalog.TrySelectProfileForCommonFwVersion(
                icId,
                commonFwVersion,
                out postbuildProfile,
                out string? profileIssue))
        {
            issue = new CompositionIssue(
                "replace.ctrlram.postbuild-category-unsupported",
                profileIssue ?? $"No legacy Combiner postbuild profile is registered for {icId}.",
                "postbuild");
            return false;
        }

        issue = null;
        return true;
    }

    private static bool TryReadBaseCommonFwVersion(
        string icId,
        string basePath,
        out string? commonFwVersion)
    {
        commonFwVersion = null;
        if (!TpFlashMapCatalog.TryGetFirmwareConfigStart(icId, out long firmwareConfigStart))
        {
            return false;
        }

        try
        {
            byte[] image = File.ReadAllBytes(basePath);
            if (!FirmwareConfigMetadataReader.TryRead(image, firmwareConfigStart, out FirmwareConfigMetadata metadata))
            {
                return false;
            }

            if (!metadata.IsFirmwareVersionBarValid)
            {
                return false;
            }

            commonFwVersion = metadata.CommonFwVersion;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
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
