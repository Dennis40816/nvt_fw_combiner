using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static partial class CompositionPlanningAdapter
{
    private const string FirmwareConfigBackupBlockId = "fw-config-backup";

    private static bool TryCreateCtrlRamFirmwareVersionWritePlan(
        FirmwareConfigMetadata backupMetadata,
        LegacyCombinerPostbuildProfile postbuildProfile,
        LegacyCombinerPostbuildCommandPlan commandPlan,
        WorkbenchCtrlRamFirmwareVersionEdit edit,
        ReadOnlySpan<byte> baseBytes,
        out FirmwareConfigVersionWritePlan? writePlan,
        out CompositionIssue? issue)
    {
        ArgumentNullException.ThrowIfNull(postbuildProfile);
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
            backupMetadata.StructureStart,
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
        long? sourceStart = postbuildProfile.FirmwareConfigWriteRoute switch
        {
            LegacyCombinerFirmwareConfigWriteRoute.CommandSourceToCanonicalBackup
                when sourceBlocks.Length == 1 => sourceBlocks[0].SourceOffset,
            LegacyCombinerFirmwareConfigWriteRoute.PrimaryToCanonicalBackup
                when sourceBlocks.Length == 0 && TryResolveImplicitFirmwareConfigSource(
                    postbuildProfile,
                    baseBytes,
                    backupMetadata,
                    out long implicitSourceStart) => implicitSourceStart,
            LegacyCombinerFirmwareConfigWriteRoute.CommandSourceToCanonicalBackup => null,
            LegacyCombinerFirmwareConfigWriteRoute.PrimaryToCanonicalBackup => null,
            LegacyCombinerFirmwareConfigWriteRoute.Unavailable => null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(postbuildProfile),
                postbuildProfile.FirmwareConfigWriteRoute,
                "Unsupported FWConfig write route."),
        };
        if (sourceStart is null)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamFirmwareVersionPropagationUnavailable,
                "TP FW version editing requires one reviewed legacy Combiner path from the modeled primary FWConfig to the canonical NVT Backup.",
                "postbuild");
            return false;
        }

        if (sourceStart.Value + FirmwareConfigLayout.RequiredLength > baseBytes.Length)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamFirmwareVersionPropagationUnavailable,
                "The legacy Combiner FWConfig source block is outside the supplied base firmware image.",
                "postbuild");
            return false;
        }

        writePlan = FirmwareConfigVersionWritePlan
            .CreateFromCanonicalBackup(backupMetadata, edit.FirmwareVersion, edit.FirmwareSubVersion)
            .RebaseToSourceStructure(sourceStart.Value);
        return true;
    }

    private static bool TryResolveImplicitFirmwareConfigSource(
        LegacyCombinerPostbuildProfile postbuildProfile,
        ReadOnlySpan<byte> baseBytes,
        FirmwareConfigMetadata backupMetadata,
        out long sourceStart)
    {
        sourceStart = 0;
        if (!BuiltInTpFlashMapCatalog.TryFind(postbuildProfile.IcId, out TpFlashMapProfile? flashMap) ||
            !FirmwareConfigMetadataReader.TryReadAtAbsoluteAddress(
                baseBytes,
                flashMap!.FirmwareConfigPrimaryStart,
                out FirmwareConfigMetadata primaryMetadata) ||
            primaryMetadata with { StructureStart = backupMetadata.StructureStart } != backupMetadata)
        {
            return false;
        }

        sourceStart = primaryMetadata.StructureStart;
        return true;
    }
}
