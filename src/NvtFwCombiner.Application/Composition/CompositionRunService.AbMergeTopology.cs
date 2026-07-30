using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

public sealed partial class CompositionRunService
{
    private const string TpFirmwareConfigInvalid = "AB_TP_FIRMWARE_CONFIG_BACKUP_INVALID";
    private const string TpTopologyMismatch = "AB_TP_TOPOLOGY_MISMATCH";
    private const string TpTopologySelectionMismatch = "AB_TP_TOPOLOGY_SELECTION_MISMATCH";

    private static void ValidateAbMergeTopologyMetadata(
        CompositionRunRequest request,
        Dictionary<string, byte[]> inputBytes,
        List<CompositionIssue> issues)
    {
        if (!request.CompiledComposition.IsV2AbFunctionOpenCandidate ||
            request.AbMergeTopologySelection is not { } selected ||
            !TryGetAcceptedTpSourceView(request, inputBytes, CompositionAddressSpaceIds.TpAInput, out ReadOnlySpan<byte> tpA) ||
            !TryGetAcceptedTpSourceView(request, inputBytes, CompositionAddressSpaceIds.TpBInput, out ReadOnlySpan<byte> tpB))
        {
            return;
        }

        if (!TryReadFirmwareConfig(tpA, out FirmwareConfigMetadata tpAMetadata))
        {
            issues.Add(new CompositionIssue(
                TpFirmwareConfigInvalid,
                "TPA has no valid canonical NVT FWConfig Backup.",
                CompositionAddressSpaceIds.TpAInput));
        }

        if (!TryReadFirmwareConfig(tpB, out FirmwareConfigMetadata tpBMetadata))
        {
            issues.Add(new CompositionIssue(
                TpFirmwareConfigInvalid,
                "TPB has no valid canonical NVT FWConfig Backup.",
                CompositionAddressSpaceIds.TpBInput));
        }

        if (issues.Any(static issue => StringComparer.Ordinal.Equals(issue.Code, TpFirmwareConfigInvalid)))
        {
            return;
        }

        if (tpAMetadata.ChipNumber == 0 || tpBMetadata.ChipNumber == 0)
        {
            FirmwareConfigMetadata zeroMetadata = tpAMetadata.ChipNumber == 0
                ? tpAMetadata
                : tpBMetadata;
            issues.Add(FirmwareConfigChipCountDiagnostics.CreateZeroIssue(
                zeroMetadata,
                FirmwareConfigChipCountRequirement.RequiredPositive,
                "ab-topology",
                "AB Code uses TPA and TPB IC Count to validate the selected topology.")!);
            return;
        }

        bool tpASingle = tpAMetadata.ChipNumber == 1;
        bool tpBSingle = tpBMetadata.ChipNumber == 1;
        if (tpASingle != tpBSingle)
        {
            issues.Add(new CompositionIssue(
                TpTopologyMismatch,
                $"TPA declares {FormatTopology(tpAMetadata.ChipNumber)} but TPB declares {FormatTopology(tpBMetadata.ChipNumber)}; AB Merge requires matching TP topology.",
                CompositionAddressSpaceIds.TpBInput));
            return;
        }

        if (selected.ChipCount == 1 != tpASingle)
        {
            issues.Add(new CompositionIssue(
                TpTopologySelectionMismatch,
                $"The selected topology '{selected.Label}' does not match TPA/TPB FWConfig Backup topology {FormatTopology(tpAMetadata.ChipNumber)}.",
                "ab-topology"));
        }
    }

    private static bool TryGetAcceptedTpSourceView(
        CompositionRunRequest request,
        Dictionary<string, byte[]> inputBytes,
        string addressSpaceId,
        out ReadOnlySpan<byte> prefix)
    {
        prefix = default;
        if (!inputBytes.TryGetValue(addressSpaceId, out byte[]? bytes))
        {
            return false;
        }

        CompiledInputArtifactInspectionResult inspection =
            CompiledInputArtifactInspectionService.Inspect(
                request.CompiledComposition,
                addressSpaceId,
                bytes);
        if (inspection.AcceptedSnapshotRange is not { Start: 0 } accepted ||
            accepted.Length > int.MaxValue)
        {
            return false;
        }

        prefix = bytes.AsSpan(0, checked((int)accepted.Length));
        return true;
    }

    private static bool TryReadFirmwareConfig(
        ReadOnlySpan<byte> prefix,
        out FirmwareConfigMetadata metadata)
    {
        return FirmwareConfigMetadataReader.TryReadBackup(prefix, out metadata) &&
            metadata.IsFirmwareVersionBarValid;
    }

    private static string FormatTopology(byte chipCount)
    {
        return chipCount == 1
            ? "1 IC"
            : $"Cascade ({chipCount} IC)";
    }
}
