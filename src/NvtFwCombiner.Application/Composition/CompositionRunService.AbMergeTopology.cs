using NvtFwCombiner.Application.FlashMaps;
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
            !TryGetDeclaredTpPrefix(request, inputBytes, CompositionAddressSpaceIds.TpAInput, out ReadOnlySpan<byte> tpA) ||
            !TryGetDeclaredTpPrefix(request, inputBytes, CompositionAddressSpaceIds.TpBInput, out ReadOnlySpan<byte> tpB))
        {
            return;
        }

        if (!TryReadTopology(tpA, out FirmwareConfigMetadata tpAMetadata))
        {
            issues.Add(new CompositionIssue(
                TpFirmwareConfigInvalid,
                "TPA has no valid canonical NVT FWConfig Backup with a positive chip count.",
                CompositionAddressSpaceIds.TpAInput));
        }

        if (!TryReadTopology(tpB, out FirmwareConfigMetadata tpBMetadata))
        {
            issues.Add(new CompositionIssue(
                TpFirmwareConfigInvalid,
                "TPB has no valid canonical NVT FWConfig Backup with a positive chip count.",
                CompositionAddressSpaceIds.TpBInput));
        }

        if (issues.Any(static issue => StringComparer.Ordinal.Equals(issue.Code, TpFirmwareConfigInvalid)))
        {
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

        if (request.AbMergeTopologySelection is { } selected &&
            selected.ChipCount == 1 != tpASingle)
        {
            issues.Add(new CompositionIssue(
                TpTopologySelectionMismatch,
                $"The selected topology '{selected.Label}' does not match TPA/TPB FWConfig Backup topology {FormatTopology(tpAMetadata.ChipNumber)}.",
                "ab-topology"));
        }
    }

    private static bool TryGetDeclaredTpPrefix(
        CompositionRunRequest request,
        Dictionary<string, byte[]> inputBytes,
        string addressSpaceId,
        out ReadOnlySpan<byte> prefix)
    {
        prefix = default;
        if (!inputBytes.TryGetValue(addressSpaceId, out byte[]? bytes) ||
            request.CompiledComposition.V2Details is not { } details)
        {
            return false;
        }

        CompiledInputSpaceBinding? binding = details.InputContract.SpaceBindings.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.AddressSpaceId, addressSpaceId));
        CompiledInputSlotRequirement? slot = binding is null
            ? null
            : details.InputContract.Slots.SingleOrDefault(candidate =>
                StringComparer.Ordinal.Equals(candidate.SlotId, binding.SlotId));
        if (slot?.LengthRequirement is not CompiledDeclaredPrefixWithWarningInputLengthRequirement declaredPrefix ||
            bytes.LongLength < declaredPrefix.RequiredEndExclusive ||
            declaredPrefix.RequiredEndExclusive > int.MaxValue)
        {
            return false;
        }

        prefix = bytes.AsSpan(0, checked((int)declaredPrefix.RequiredEndExclusive));
        return true;
    }

    private static bool TryReadTopology(
        ReadOnlySpan<byte> prefix,
        out FirmwareConfigMetadata metadata)
    {
        return FirmwareConfigMetadataReader.TryReadBackup(prefix, out metadata) &&
            metadata.IsFirmwareVersionBarValid &&
            metadata.ChipNumber > 0;
    }

    private static string FormatTopology(byte chipCount)
    {
        return chipCount == 1
            ? "1 IC"
            : $"Cascade ({chipCount} IC)";
    }
}
