using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string FirmwareConfigBackupBlockId = "fw-config-backup";

    private static bool TryCreateCtrlRamFirmwareVersionWritePlan(
        FirmwareConfigMetadata backupMetadata,
        LegacyCombinerPostbuildCommandPlan commandPlan,
        WorkbenchCtrlRamFirmwareVersionEdit edit,
        long baseLength,
        out FirmwareConfigVersionWritePlan? writePlan,
        out CompositionIssue? issue)
    {
        ArgumentNullException.ThrowIfNull(commandPlan);
        ArgumentNullException.ThrowIfNull(edit);

        writePlan = null;
        issue = null;
        if (!backupMetadata.IsFirmwareVersionBarValid)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamFirmwareVersionSourceInvalid,
                "TP FW version editing requires a valid FW/bar pair in the canonical NVT Backup.",
                WorkbenchSlotIds.ReplaceBase);
            return false;
        }

        ByteRange requiredBackupRange = new(
            backupMetadata.FirmwareConfigStart,
            FirmwareConfigLayout.RequiredLength);
        LegacyCombinerBlockArgument[] sourceBlocks =
        [
            .. commandPlan.Commands
                .SelectMany(command => command.Blocks)
                .Where(block =>
                    block.SourceKind == LegacyCombinerBlockSourceKind.FirmwareImage &&
                    StringComparer.Ordinal.Equals(block.BlockId, FirmwareConfigBackupBlockId) &&
                    block.FirmwareRange.Start == requiredBackupRange.Start &&
                    block.FirmwareRange.Contains(requiredBackupRange))
                .GroupBy(block => (block.SourceOffset, block.FirmwareRange))
                .Select(group => group.First())
                .OrderBy(block => block.SourceOffset)
                .ThenBy(block => block.FirmwareRange.Start),
        ];
        if (sourceBlocks.Length != 1)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamFirmwareVersionPropagationUnavailable,
                "TP FW version editing requires exactly one legacy Combiner FWConfig source block that propagates to the canonical NVT Backup.",
                "postbuild");
            return false;
        }

        LegacyCombinerBlockArgument sourceBlock = sourceBlocks[0];
        if (sourceBlock.SourceOffset + FirmwareConfigLayout.RequiredLength > baseLength)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamFirmwareVersionPropagationUnavailable,
                "The legacy Combiner FWConfig source block is outside the supplied base firmware image.",
                "postbuild");
            return false;
        }

        writePlan = FirmwareConfigVersionWritePlan
            .CreateForBackup(backupMetadata, edit.FirmwareVersion, edit.FirmwareSubVersion)
            .RebaseToCombinerSource(sourceBlock.SourceOffset);
        return true;
    }
}
