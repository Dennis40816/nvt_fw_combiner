using System.Buffers.Binary;
using System.Globalization;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static class DiffDlmNfMaskPolicy
{
    private static readonly DiffDlmNfGeometry Nt51929Like = new(
        new ByteRange(0x2D100, 0x8C00),
        RecordStride: 0x1400,
        WritableDlmLength: 0xB90,
        PreservedNfLength: 0x870,
        MinimumIcCount: 2,
        MaximumIcCount: 8,
        DlmDiffSizeCodeOffset: 0x7120,
        DlmDiffStartOffset: 0x716C);

    private static readonly DiffDlmNfGeometry Nt51950Like = new(
        new ByteRange(0x33200, 0x1400),
        RecordStride: 0x1400,
        WritableDlmLength: 2320,
        PreservedNfLength: 2800,
        MinimumIcCount: 2,
        MaximumIcCount: 2,
        DlmDiffSizeCodeOffset: null,
        DlmDiffStartOffset: null);

    internal static bool TryResolve(
        string icId,
        LegacyCombinerPostbuildBranch branch,
        out DiffDlmNfGeometry? geometry)
    {
        geometry = branch == LegacyCombinerPostbuildBranch.Cascade
            ? IcSupportCatalog.NormalizeIcId(icId) switch
            {
                "NT51919" or "NT51929" or "NT51932" => Nt51929Like,
                "NT51950" or "NT51951" => Nt51950Like,
                _ => null,
            }
            : null;
        return geometry is not null;
    }

    internal static bool IsIndependentNfSource(string sourceFileName)
    {
        return string.Equals(sourceFileName, "NF_Ctrlram.bin", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsDiffDlmSource(string sourceFileName)
    {
        return string.Equals(sourceFileName, "DiffDLM.bin", StringComparison.Ordinal);
    }

    internal static bool TryResolveTopologyCount(
        DiffDlmNfGeometry geometry,
        IcNumberSelection selection,
        int? reportedChipCount,
        out int topologyCount,
        out CompositionIssue? issue)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(selection);

        string requestedToken = selection.Parts[^1].Trim();
        bool hasRequestedCount = int.TryParse(
            requestedToken,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out int requestedCount) && requestedCount > 0;
        bool hasReportedCount = reportedChipCount is > 0;
        if (hasRequestedCount &&
            hasReportedCount &&
            requestedCount != reportedChipCount!.Value)
        {
            topologyCount = 0;
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamIcNumberMismatch,
                $"Selected Number is {requestedCount} IC, but the base firmware FWConfig reports {reportedChipCount.Value} IC.",
                "number");
            return false;
        }

        if (!hasRequestedCount && !hasReportedCount)
        {
            topologyCount = 0;
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamIcNumberMismatch,
                "Preserve-active-DiffNF requires an exact IC Count from FWConfig or an exact numeric Number selection.",
                "number");
            return false;
        }

        topologyCount = hasReportedCount ? reportedChipCount!.Value : requestedCount;
        if (topologyCount < geometry.MinimumIcCount || topologyCount > geometry.MaximumIcCount)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamIcNumberUnsupported,
                $"Preserve-active-DiffNF supports {geometry.MinimumIcCount}–{geometry.MaximumIcCount} IC for this route, but {topologyCount} IC was resolved.",
                "number");
            topologyCount = 0;
            return false;
        }

        issue = null;
        return true;
    }

    internal static bool TryResolveActiveRange(
        DiffDlmNfGeometry geometry,
        int icCount,
        ReadOnlySpan<byte> reference,
        out ByteRange activeRange,
        out CompositionIssue? issue)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        activeRange = default;
        if (icCount < geometry.MinimumIcCount || icCount > geometry.MaximumIcCount)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamIcNumberUnsupported,
                $"Preserve-active-DiffNF supports {geometry.MinimumIcCount}–{geometry.MaximumIcCount} IC for this route, but the resolved base reports {icCount} IC.",
                "number");
            return false;
        }

        long start = geometry.MaximumFirmwareRange.Start;
        int stride = geometry.RecordStride;
        if (geometry.DlmDiffSizeCodeOffset is int sizeCodeOffset &&
            geometry.DlmDiffStartOffset is int startOffset)
        {
            if (sizeCodeOffset < 0 ||
                startOffset < 0 ||
                sizeCodeOffset + sizeof(ushort) > reference.Length ||
                startOffset + sizeof(uint) > reference.Length)
            {
                issue = Invalid("The base firmware is too short to resolve its DiffDLM header geometry.");
                return false;
            }

            stride = checked(BinaryPrimitives.ReadUInt16LittleEndian(
                reference.Slice(sizeCodeOffset, sizeof(ushort))) + 1);
            start = BinaryPrimitives.ReadUInt32LittleEndian(
                reference.Slice(startOffset, sizeof(uint)));
            if (stride != geometry.RecordStride)
            {
                issue = Invalid(
                    $"The base firmware reports DiffDLM stride 0x{stride:X}, but this hot-fix contract requires 0x{geometry.RecordStride:X}.");
                return false;
            }

            if (start != geometry.MaximumFirmwareRange.Start)
            {
                issue = Invalid(
                    $"The base firmware reports DiffDLM start 0x{start:X}, but this hot-fix contract requires 0x{geometry.MaximumFirmwareRange.Start:X}.");
                return false;
            }
        }

        long activeLength = checked((long)(icCount - 1) * stride);
        activeRange = new ByteRange(start, activeLength);
        if (!geometry.MaximumFirmwareRange.Contains(activeRange) ||
            activeRange.EndExclusive > reference.Length)
        {
            issue = Invalid(
                $"The active DiffDLM range {activeRange} escapes the approved maximum range {geometry.MaximumFirmwareRange}.");
            activeRange = default;
            return false;
        }

        issue = null;
        return true;
    }

    internal static bool TryValidateSelectedSource(
        DiffDlmNfGeometry geometry,
        ByteRange activeRange,
        ReadOnlySpan<byte> selectedPrefix,
        out CompositionIssue? issue)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        int activePrefixLength = checked((int)activeRange.Length);
        int activeRecordCount = activePrefixLength / geometry.RecordStride;
        if (activePrefixLength <= 0 ||
            activePrefixLength % geometry.RecordStride != 0 ||
            !geometry.MaximumFirmwareRange.Contains(activeRange))
        {
            issue = Invalid("The resolved active DiffDLM range is inconsistent with the approved record geometry.");
            return false;
        }

        if (selectedPrefix.Length < activePrefixLength)
        {
            issue = new CompositionIssue(
                WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmSourceInvalid,
                $"DiffDLM is too short for {activeRecordCount} active slave record(s): {selectedPrefix.Length} bytes supplied, at least {activePrefixLength} bytes required.",
                WorkbenchSlotIds.CreateReplaceCtrlRam("diff"));
            return false;
        }

        for (int recordIndex = 0; recordIndex < activeRecordCount; recordIndex++)
        {
            int recordOffset = checked(recordIndex * geometry.RecordStride);
            ReadOnlySpan<byte> selectedDlm = selectedPrefix.Slice(recordOffset, geometry.WritableDlmLength);
            if (IsUniform(selectedDlm))
            {
                issue = new CompositionIssue(
                    WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmSourceInvalid,
                    $"DiffDLM active record {recordIndex} has no usable DLM payload because all {geometry.WritableDlmLength} bytes are identical.",
                    WorkbenchSlotIds.CreateReplaceCtrlRam("diff"));
                return false;
            }
        }

        issue = null;
        return true;
    }

    private static CompositionIssue Invalid(string message)
    {
        return new CompositionIssue(
            WorkbenchIssueCodes.ReplaceCtrlRamDiffDlmSourceInvalid,
            message,
            WorkbenchSlotIds.ReplaceBase);
    }

    private static bool IsUniform(ReadOnlySpan<byte> bytes)
    {
        byte first = bytes[0];
        for (int index = 1; index < bytes.Length; index++)
        {
            if (bytes[index] != first)
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed record DiffDlmNfGeometry(
    ByteRange MaximumFirmwareRange,
    int RecordStride,
    int WritableDlmLength,
    int PreservedNfLength,
    int MinimumIcCount,
    int MaximumIcCount,
    int? DlmDiffSizeCodeOffset,
    int? DlmDiffStartOffset);
