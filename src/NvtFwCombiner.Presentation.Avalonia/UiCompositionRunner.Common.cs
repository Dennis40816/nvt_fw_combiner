using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public static partial class UiCompositionRunner
{
    /// <summary>Projects one accepted canonical memory layout into display-only rows.</summary>
    public static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetMemoryDisplay(
        PresentationCompositionServices services,
        ActiveSessionSnapshot acceptedSession,
        ShellTextResources? text = null,
        GeneralAuthoringAdmissionResult? admission = null,
        IReadOnlyList<CtrlRamRegion>? ctrlRamRegions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(acceptedSession);
        ResolvedCapability capability = acceptedSession.ExactCapability ??
            throw new InvalidOperationException(
                "Memory projection requires an exact compiled capability.");
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(
            capability,
            acceptedSession,
            capability.CompiledComposition,
            ctrlRamRegions);
        IReadOnlyList<MemoryLayoutConflict> conflicts = admission is null
            ? []
            : MemoryLayoutProjector.ProjectAdmissionConflicts(admission, layout.Capacity);
        return (
            FormatMemoryRange(new ByteRange(0, layout.Capacity)),
            [
                .. layout.AfterSegments.Select(segment => ToMemoryMapRow(layout, segment)),
                .. conflicts.Select(ToMemoryMapRow),
            ],
            [
                .. layout.AfterSegments.Select(segment => ToMemoryCoverageSegment(layout, segment, text)),
                .. conflicts.Select(conflict => ToMemoryCoverageSegment(layout, conflict, text)),
            ]);
    }

    /// <summary>Projects a typed pending state when no exact authoring publication exists.</summary>
    public static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetPendingMemoryDisplay(
        string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        return (
            "Memory layout pending",
            [new MemoryMapRowViewModel("Pending", "No output", "Select", "Pending input", detail)],
            [new MemoryCoverageSegmentViewModel(
                "Pending",
                "Pending input",
                detail,
                ResolveCoverageFill("Pending input"),
                300d)]);
    }

    private static MemoryMapRowViewModel ToMemoryMapRow(
        MemoryLayoutSnapshot layout,
        MemoryLayoutSegment segment)
    {
        MemoryLayoutSegment before = layout.BeforeSegments.Single(candidate =>
            candidate.Range.Contains(segment.Range));
        return new MemoryMapRowViewModel(
            FormatMemoryRange(segment.Range),
            MemorySourceLabel(before),
            MemoryActionLabel(segment),
            MemorySourceLabel(segment),
            MemoryDetail(layout, segment));
    }

    private static MemoryMapRowViewModel ToMemoryMapRow(MemoryLayoutConflict conflict)
    {
        return new MemoryMapRowViewModel(
            FormatMemoryRange(conflict.Range),
            "Output",
            "Blocked",
            "Overlap error",
            MemoryConflictDetail(conflict));
    }

    private static MemoryCoverageSegmentViewModel ToMemoryCoverageSegment(
        MemoryLayoutSnapshot layout,
        MemoryLayoutSegment segment,
        ShellTextResources? text)
    {
        string sourceLabel = MemorySourceLabel(segment);
        bool changed = segment.Disposition is
            MemoryWorkflowDisposition.WillWrite or
            MemoryWorkflowDisposition.WillReplace;
        return new MemoryCoverageSegmentViewModel(
            FormatMemoryRange(segment.Range),
            sourceLabel,
            MemoryDetail(layout, segment),
            ResolveCoverageFill(sourceLabel),
            300d * segment.Range.Length / layout.Capacity,
            changed,
            segment.Disposition == MemoryWorkflowDisposition.Kept ||
                segment.SourceSpaceId == CompositionAddressSpaceIds.ReferenceBase,
            segment.RegionId,
            preservationDetails: segment.PreservationDetails,
            text: text,
            regionGroup: segment.RegionGroup);
    }

    private static MemoryCoverageSegmentViewModel ToMemoryCoverageSegment(
        MemoryLayoutSnapshot layout,
        MemoryLayoutConflict conflict,
        ShellTextResources? text)
    {
        return new MemoryCoverageSegmentViewModel(
            FormatMemoryRange(conflict.Range),
            "Overlap error",
            MemoryConflictDetail(conflict),
            ResolveCoverageFill("Overlap error"),
            300d * conflict.Range.Length / layout.Capacity,
            isChanged: true,
            text: text);
    }

    private static string MemoryActionLabel(MemoryLayoutSegment segment)
    {
        return segment.SourceSpaceId == CompositionAddressSpaceIds.ReferenceBase &&
            segment.Disposition == MemoryWorkflowDisposition.WillReplace
                ? "Restore"
                : segment.SourceSpaceId == CompositionAddressSpaceIds.TpBWork
                    ? "Transform + Overlay"
                    : segment.Disposition switch
                    {
                        MemoryWorkflowDisposition.WillWrite =>
                            segment.ProcessorEffect == MemoryProcessorEffect.DeclaredWrite
                                ? "Postbuild"
                                : "Copy",
                        MemoryWorkflowDisposition.WillReplace =>
                            segment.ProcessorEffect == MemoryProcessorEffect.DeclaredWrite
                                ? "Replace + CRC"
                                : "Replace",
                        MemoryWorkflowDisposition.Kept => "Preserve",
                        MemoryWorkflowDisposition.Blank or MemoryWorkflowDisposition.Resolved => "Initialize",
                        MemoryWorkflowDisposition.DpAbBase => "Copy",
                        MemoryWorkflowDisposition.TpaOverlay => "Overlay",
                        MemoryWorkflowDisposition.TpbOverlay => "Transform + Overlay",
                        _ => "Project",
                    };
    }

    private static string MemorySourceLabel(MemoryLayoutSegment segment)
    {
        return segment.SourceSpaceId is { } sourceSpaceId
            ? DynamicCtrlRamReplacementIds.TryFormatDisplayLabel(sourceSpaceId, out _)
                ? DynamicCtrlRamReplacementIds.FormatRegionDisplayLabel(
                    segment.RegionId)
                : AddressSpaceLabel(sourceSpaceId)
            : segment.Disposition switch
            {
                MemoryWorkflowDisposition.Blank => "Reserved",
                MemoryWorkflowDisposition.Kept => "Base flash",
                MemoryWorkflowDisposition.Resolved or
                MemoryWorkflowDisposition.WillWrite or
                MemoryWorkflowDisposition.WillReplace or
                MemoryWorkflowDisposition.DpAbBase or
                MemoryWorkflowDisposition.TpaOverlay or
                MemoryWorkflowDisposition.TpbOverlay => segment.ContentRole switch
                {
                    MemoryContentRole.Dp => "DP BIN",
                    MemoryContentRole.Tp => "TP BIN",
                    MemoryContentRole.TpBackup => "TPB",
                    MemoryContentRole.Ldc => "LDC BIN",
                    MemoryContentRole.CtrlRam => "CtrlRAM BIN",
                    MemoryContentRole.Reserved => "Reserved",
                    MemoryContentRole.General => "Output",
                    _ => "Output",
                },
                _ => "Output",
            };
    }

    private static string MemoryDetail(
        MemoryLayoutSnapshot layout,
        MemoryLayoutSegment segment)
    {
        string initialization = layout.BlankFillByte is { } fillByte
            ? $"Blank fill 0x{fillByte:X2}. "
            : string.Empty;
        string operations = segment.ContributingOperations.Count == 0
            ? "No compiled operation writes this range."
            : $"Compiled operations: {string.Join(", ", segment.ContributingOperations.Select(static operation =>
                $"{operation.OperationId} (Sequence {operation.Sequence}; Reason: {operation.Reason})"))}.";
        return $"{segment.RegionId}. {initialization}{operations}";
    }

    private static string MemoryConflictDetail(MemoryLayoutConflict conflict)
    {
        string mappings = conflict.MappingIds.Count == 0
            ? "No mapping ids were reported."
            : $"Mappings: {string.Join(", ", conflict.MappingIds)}.";
        return $"{conflict.Message} {mappings}";
    }

    private static string AddressSpaceLabel(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            CompositionAddressSpaceIds.DpInput => "DP BIN",
            CompositionAddressSpaceIds.TpInput => "TP BIN",
            CompositionAddressSpaceIds.LdcInput => "LDC BIN",
            CompositionAddressSpaceIds.ReferenceBase => "Base flash",
            CompositionAddressSpaceIds.DpReplacement => "Changed DP BIN",
            CompositionAddressSpaceIds.LdcReplacement => "Changed LDC BIN",
            CompositionAddressSpaceIds.DpAbInput => "DP AB",
            CompositionAddressSpaceIds.TpAInput => "TPA",
            CompositionAddressSpaceIds.TpBInput or CompositionAddressSpaceIds.TpBWork => "TPB",
            _ => addressSpaceId,
        };
    }

    private static string FormatMemoryRange(ByteRange range)
    {
        return FormattableString.Invariant(
            $"0x{range.Start:X5}-0x{range.EndExclusive - 1:X5} (len 0x{range.Length:X})");
    }

    private static string ResolveCoverageFill(string sourceLabel)
    {
        return sourceLabel switch
        {
            "DP BIN" or "Changed DP BIN" or "DP_AB BIN" or "DP AB" => "#2563EB",
            "TP BIN" or "TPA BIN" or "TPA" or "A bank work" or
                "CtrlRAM BIN" or "Changed CtrlRAM BIN" => "#16A34A",
            "TPB work buffer" or "TPB" or "B bank work" or "Postbuild AB work" => "#7C3AED",
            "LDC BIN" or "Changed LDC BIN" => "#F97316",
            "Source BIN" => "#0D9488",
            "Restored TP" or "Preserved customer info" or "Preserve" => "#64748B",
            "Overlap error" => "#DC2626",
            string label when label.Contains("NF CtrlRAM", StringComparison.OrdinalIgnoreCase) => "#DC2626",
            string label when label.Contains("Normal CtrlRAM", StringComparison.OrdinalIgnoreCase) => "#0891B2",
            string label when label.Contains("MP CtrlRAM", StringComparison.OrdinalIgnoreCase) => "#7C3AED",
            string label when label.Contains("VN CtrlRAM", StringComparison.OrdinalIgnoreCase) => "#DB2777",
            string label when label.Contains("DIFF", StringComparison.OrdinalIgnoreCase) ||
                              label.Contains("DLM", StringComparison.OrdinalIgnoreCase) => "#D97706",
            string label when label.Contains("Vector", StringComparison.OrdinalIgnoreCase) => "#0D9488",
            _ => "#CBD5E1",
        };
    }

    private static string ToRange(long start, long length)
    {
        return FormattableString.Invariant($"0x{start:X5}-0x{start + length - 1:X5}");
    }

    private static string ToLength(long length)
    {
        return FormattableString.Invariant($"len 0x{length:X}");
    }
}
