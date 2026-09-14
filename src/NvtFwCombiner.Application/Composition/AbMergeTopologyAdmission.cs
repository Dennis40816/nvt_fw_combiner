using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Observed Backup counts and existing topology diagnostics, without retained input bytes.</summary>
internal sealed class AbMergeTopologyAdmissionResult(
    byte? tpAChipCount, byte? tpBChipCount, IEnumerable<CompositionIssue> issues)
{
    internal byte? TpAChipCount { get; } = tpAChipCount;
    internal byte? TpBChipCount { get; } = tpBChipCount;
    internal IReadOnlyList<CompositionIssue> Issues { get; } = Array.AsReadOnly(issues.ToArray());
    internal bool Succeeded => Issues.Count == 0;
}

/// <summary>Single owner of the existing AB single/cascade admission; exact counts are observations, not selectors.</summary>
internal static class AbMergeTopologyAdmission
{
    internal static AbMergeTopologyAdmissionResult Assess(
        ReadOnlySpan<byte> tpA, ReadOnlySpan<byte> tpB, TopologySelection selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        var issues = new List<CompositionIssue>();
        bool validA = TryReadFirmwareConfig(tpA, out FirmwareConfigMetadata metadataA);
        bool validB = TryReadFirmwareConfig(tpB, out FirmwareConfigMetadata metadataB);
        if (!validA)
        {
            issues.Add(new CompositionIssue(
                "AB_TP_FIRMWARE_CONFIG_BACKUP_INVALID",
                "TPA has no valid canonical NVT FWConfig Backup.",
                CompositionAddressSpaceIds.TpAInput));
        }

        if (!validB)
        {
            issues.Add(new CompositionIssue(
                "AB_TP_FIRMWARE_CONFIG_BACKUP_INVALID",
                "TPB has no valid canonical NVT FWConfig Backup.",
                CompositionAddressSpaceIds.TpBInput));
        }

        if (!validA || !validB)
        {
            return new(validA ? metadataA.ChipNumber : null, validB ? metadataB.ChipNumber : null, issues);
        }

        if (metadataA.ChipNumber == 0 || metadataB.ChipNumber == 0)
        {
            FirmwareConfigMetadata zeroMetadata = metadataA.ChipNumber == 0 ? metadataA : metadataB;
            issues.Add(FirmwareConfigChipCountDiagnostics.CreateZeroIssue(
                zeroMetadata,
                FirmwareConfigChipCountRequirement.RequiredPositive,
                "ab-topology",
                "AB Code uses TPA and TPB IC Count to validate the selected topology.")!);
            return new(metadataA.ChipNumber, metadataB.ChipNumber, issues);
        }

        bool tpASingle = metadataA.ChipNumber == 1;
        bool tpBSingle = metadataB.ChipNumber == 1;
        if (tpASingle != tpBSingle)
        {
            issues.Add(new CompositionIssue(
                "AB_TP_TOPOLOGY_MISMATCH",
                $"TPA declares {FormatTopology(metadataA.ChipNumber)} but TPB declares {FormatTopology(metadataB.ChipNumber)}; AB Merge requires matching TP topology.",
                CompositionAddressSpaceIds.TpBInput));
        }
        else if (selected.ChipCount == 1 != tpASingle)
        {
            issues.Add(new CompositionIssue(
                "AB_TP_TOPOLOGY_SELECTION_MISMATCH",
                $"The selected topology '{selected.Label}' does not match TPA/TPB FWConfig Backup topology {FormatTopology(metadataA.ChipNumber)}.",
                "ab-topology"));
        }

        return new(metadataA.ChipNumber, metadataB.ChipNumber, issues);
    }

    private static bool TryReadFirmwareConfig(ReadOnlySpan<byte> prefix, out FirmwareConfigMetadata metadata)
    {
        return FirmwareConfigMetadataReader.TryReadBackup(prefix, out metadata) && metadata.IsFirmwareVersionBarValid;
    }

    private static string FormatTopology(byte chipCount)
    {
        return chipCount == 1 ? "1 IC" : $"Cascade ({chipCount} IC)";
    }
}
