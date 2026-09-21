using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Application.InputInspection;
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

/// <summary>One owner for AB pair count equality and optional single/cascade selector admission.</summary>
internal static class AbMergeTopologyAdmission
{
    internal static AbMergeTopologyAdmissionResult Assess(
        ReadOnlySpan<byte> tpA, ReadOnlySpan<byte> tpB, TopologySelection? selected)
    {
        var issues = new List<CompositionIssue>();
        bool validA = TryReadFirmwareConfig(tpA, out FirmwareConfigMetadata metadataA);
        bool validB = TryReadFirmwareConfig(tpB, out FirmwareConfigMetadata metadataB);
        if (!validA)
        {
            issues.Add(new CompositionIssue(
                "AB_TP_FIRMWARE_CONFIG_BACKUP_INVALID",
                "TPA IC Count is unreadable: no valid canonical NVT FWConfig Backup.",
                CompositionAddressSpaceIds.TpAInput));
        }

        if (!validB)
        {
            issues.Add(new CompositionIssue(
                "AB_TP_FIRMWARE_CONFIG_BACKUP_INVALID",
                "TPB IC Count is unreadable: no valid canonical NVT FWConfig Backup.",
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
                $"{(metadataA.ChipNumber == 0 ? "TPA" : "TPB")} IC Count was read as 0; AB Code requires positive, identical TP counts.")!);
            return new(metadataA.ChipNumber, metadataB.ChipNumber, issues);
        }

        bool tpASingle = metadataA.ChipNumber == 1;
        if (metadataA.ChipNumber != metadataB.ChipNumber)
        {
            issues.Add(new CompositionIssue(
                "AB_TP_TOPOLOGY_MISMATCH",
                $"TPA declares {metadataA.ChipNumber} IC but TPB declares {metadataB.ChipNumber} IC; AB Merge requires identical TP IC Counts.",
                CompositionAddressSpaceIds.TpBInput));
        }
        else if (selected is not null && selected.ChipCount == 1 != tpASingle)
        {
            issues.Add(new CompositionIssue(
                "AB_TP_TOPOLOGY_SELECTION_MISMATCH",
                $"The selected topology '{selected.Label}' does not match TPA/TPB FWConfig Backup topology {FormatTopology(metadataA.ChipNumber)}.",
                "ab-topology"));
        }

        return new(metadataA.ChipNumber, metadataB.ChipNumber, issues);
    }

    internal static AbMergeTopologyAdmissionResult? AssessAcceptedPair(
        CompiledComposition composition, ReadOnlyMemory<byte> tpA, ReadOnlyMemory<byte> tpB, TopologySelection? selected)
    {
        return TryGetAcceptedTpSourceView(composition, tpA, CompositionAddressSpaceIds.TpAInput, out ReadOnlySpan<byte> a) &&
            TryGetAcceptedTpSourceView(composition, tpB, CompositionAddressSpaceIds.TpBInput, out ReadOnlySpan<byte> b)
            ? Assess(a, b, selected) : null;
    }

    internal static AbMergeTopologyAdmissionResult AssessCommonAcceptedPair(
        IReadOnlyList<CompiledComposition> candidates, ReadOnlyMemory<byte> tpA, ReadOnlyMemory<byte> tpB,
        TopologySelection? selected)
    {
        ByteRange? acceptedA = null;
        ByteRange? acceptedB = null;
        long? requiredA = null;
        long? requiredB = null;
        if (candidates.Count == 0) { return InvalidGeometry(); }
        foreach (CompiledComposition candidate in candidates)
        {
            if (!candidate.IsV2AbMergeRuntimeRoute ||
                !candidate.V2Details.InputContract.SpaceBindings.Any(static binding => binding.AddressSpaceId == CompositionAddressSpaceIds.TpAInput) ||
                !candidate.V2Details.InputContract.SpaceBindings.Any(static binding => binding.AddressSpaceId == CompositionAddressSpaceIds.TpBInput))
            {
                return InvalidGeometry();
            }
            CompiledInputArtifactInspectionResult a = CompiledInputArtifactInspectionService.Inspect(candidate, CompositionAddressSpaceIds.TpAInput, tpA);
            CompiledInputArtifactInspectionResult b = CompiledInputArtifactInspectionService.Inspect(candidate, CompositionAddressSpaceIds.TpBInput, tpB);
            if (a.BlocksBuild || b.BlocksBuild || a.AcceptedSnapshotRange is not { Start: 0 } aRange ||
                b.AcceptedSnapshotRange is not { Start: 0 } bRange ||
                (acceptedA is not null && (acceptedA != aRange || requiredA != a.RequiredEndExclusive)) ||
                (acceptedB is not null && (acceptedB != bRange || requiredB != b.RequiredEndExclusive)))
            {
                return InvalidGeometry();
            }
            acceptedA = aRange;
            acceptedB = bRange;
            requiredA = a.RequiredEndExclusive;
            requiredB = b.RequiredEndExclusive;
        }
        return Assess(tpA.Span[..checked((int)acceptedA!.Value.Length)], tpB.Span[..checked((int)acceptedB!.Value.Length)], selected);
    }

    private static AbMergeTopologyAdmissionResult InvalidGeometry()
    {
        return new(null, null, [new CompositionIssue("AB_TP_SOURCE_GEOMETRY_INVALID",
            "TP inputs must satisfy one common accepted source range across all current AB format candidates.")]);
    }

    private static bool TryGetAcceptedTpSourceView(
        CompiledComposition composition, ReadOnlyMemory<byte> bytes, string addressSpaceId, out ReadOnlySpan<byte> prefix)
    {
        prefix = default;
        CompiledInputArtifactInspectionResult inspection = CompiledInputArtifactInspectionService.Inspect(
            composition, addressSpaceId, bytes);
        if (inspection.AcceptedSnapshotRange is not { Start: 0 } accepted || accepted.Length > int.MaxValue)
        {
            return false;
        }
        prefix = bytes.Span[..checked((int)accepted.Length)];
        return true;
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
