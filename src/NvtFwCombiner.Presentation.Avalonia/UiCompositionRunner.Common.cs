using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

internal static partial class UiCompositionRunner
{
    /// <summary>Projects one accepted canonical memory layout into display-only rows.</summary>
    internal static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetMemoryDisplay(
        PresentationCompositionServices services,
        ActiveSessionSnapshot acceptedSession,
        ShellTextResources text,
        GeneralAuthoringAdmissionResult? admission = null,
        IReadOnlyList<CtrlRamRegion>? ctrlRamRegions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(acceptedSession);
        ArgumentNullException.ThrowIfNull(text);
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
                .. layout.AfterSegments.Select(segment => ToMemoryMapRow(layout, segment, text)),
                .. conflicts.Select(conflict => ToMemoryMapRow(conflict, text)),
            ],
            [
                .. layout.AfterSegments.Select(segment => ToMemoryCoverageSegment(layout, segment, text)),
                .. conflicts.Select(conflict => ToMemoryCoverageSegment(layout, conflict, text)),
            ]);
    }

    /// <summary>Projects a typed pending state when no exact authoring publication exists.</summary>
    internal static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetPendingMemoryDisplay(
        ShellTextResources text,
        IEnumerable<FirmwareSlotViewModel> slots,
        MemoryPendingPrerequisite fallbackPrerequisite)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(slots);
        FirmwareSlotViewModel? pending = slots.FirstOrDefault(static slot =>
                slot.AddressSpaceId == CompositionAddressSpaceIds.ReferenceBase && !slot.HasFile) ??
            slots.FirstOrDefault(static slot => slot.HasFile && slot.IsInputInspectionBlocking) ??
            slots.FirstOrDefault(static slot => !slot.IsOptional && !slot.HasFile);
        bool blocked = pending is { HasFile: true, IsInputInspectionBlocking: true };
        (string waitingLabel, string detail) = blocked
            ? text.GetBlockingInputText(
                pending!.AddressSpaceId,
                pending.Title,
                pending.InputInspectionStatus)
            : pending is null
                ? text.GetPendingInputText(fallbackPrerequisite)
                : text.GetPendingInputText(pending.AddressSpaceId, pending.Title);
        string unavailableLabel = text.NotAvailableLabel;
        return (
            waitingLabel,
            [new MemoryMapRowViewModel(
                unavailableLabel,
                new MemoryPlanSource(MemoryPlanSourceKind.NoOutput),
                blocked ? MemoryPlanActionKind.Blocked : MemoryPlanActionKind.Browse,
                new MemoryPlanSource(MemoryPlanSourceKind.Localized, waitingLabel),
                detail,
                text)],
            [new MemoryCoverageSegmentViewModel(
                unavailableLabel,
                waitingLabel,
                detail,
                blocked ? MemoryCoverageFillRole.Conflict : MemoryCoverageFillRole.Neutral,
                300d,
                diagnosticSeverity: blocked
                    ? MemoryDiagnosticSeverity.Error
                    : MemoryDiagnosticSeverity.Information,
                text: text,
                addressRangeLabel: unavailableLabel,
                lengthLabel: string.Empty,
                compactDetail: detail)]);
    }

    private static MemoryMapRowViewModel ToMemoryMapRow(
        MemoryLayoutSnapshot layout,
        MemoryLayoutSegment segment,
        ShellTextResources text)
    {
        MemoryLayoutSegment before = layout.BeforeSegments.Single(candidate =>
            candidate.Range.Contains(segment.Range));
        return new MemoryMapRowViewModel(
            FormatMemoryRange(segment.Range),
            MemorySource(before),
            MemoryAction(segment),
            MemorySource(segment),
            MemoryDetail(layout, segment, text),
            text);
    }

    private static MemoryMapRowViewModel ToMemoryMapRow(
        MemoryLayoutConflict conflict,
        ShellTextResources text)
    {
        return new MemoryMapRowViewModel(
            FormatMemoryRange(conflict.Range),
            new MemoryPlanSource(MemoryPlanSourceKind.Output),
            MemoryPlanActionKind.Blocked,
            new MemoryPlanSource(MemoryPlanSourceKind.OverlapError),
            text.FormatMemoryLayoutConflictDetail(conflict.MappingIds),
            text);
    }

    private static MemoryCoverageSegmentViewModel ToMemoryCoverageSegment(
        MemoryLayoutSnapshot layout,
        MemoryLayoutSegment segment,
        ShellTextResources text)
    {
        string sourceLabel = text.GetMemoryPlanSourceLabel(MemorySource(segment));
        string logicalSourceLabel = segment.ContentRole == MemoryContentRole.CtrlRam
            ? ShellTextResources.GetCtrlRamRegionTechnicalLabel(segment.CtrlRamRegionRole)
            : sourceLabel;
        return new MemoryCoverageSegmentViewModel(
            FormatMemoryRange(segment.Range),
            sourceLabel,
            MemoryDetail(layout, segment, text),
            ResolveCoverageFillRole(segment),
            300d * segment.Range.Length / layout.Capacity,
            disposition: segment.Disposition,
            observedChange: segment.ObservedChange,
            diagnosticSeverity: segment.DiagnosticSeverity,
            usesBaseFirmwarePattern: segment.Disposition == MemoryWorkflowDisposition.Kept ||
                segment.SourceSpaceId == CompositionAddressSpaceIds.ReferenceBase,
            regionId: segment.RegionId,
            sourceSlotId: segment.SourceSlotId,
            logicalSourceLabel: logicalSourceLabel,
            preservationDetails: segment.PreservationDetails,
            text: text,
            regionGroup: segment.RegionGroup,
            rangeStart: segment.Range.Start,
            rangeEndExclusive: segment.Range.EndExclusive,
            addressRangeLabel: FormatMemoryAddressRange(segment.Range),
            lengthLabel: FormatMemoryLength(segment.Range),
            compactDetail: MemoryCompactDetail(segment, sourceLabel, text));
    }

    private static MemoryCoverageSegmentViewModel ToMemoryCoverageSegment(
        MemoryLayoutSnapshot layout,
        MemoryLayoutConflict conflict,
        ShellTextResources text)
    {
        string detail = text.FormatMemoryLayoutConflictDetail(conflict.MappingIds);
        return new MemoryCoverageSegmentViewModel(
            FormatMemoryRange(conflict.Range),
            text.GetMemoryPlanSourceLabel(new MemoryPlanSource(MemoryPlanSourceKind.OverlapError)),
            detail,
            MemoryCoverageFillRole.Conflict,
            300d * conflict.Range.Length / layout.Capacity,
            diagnosticSeverity: MemoryDiagnosticSeverity.Error,
            text: text,
            addressRangeLabel: FormatMemoryAddressRange(conflict.Range),
            lengthLabel: FormatMemoryLength(conflict.Range),
            compactDetail: detail);
    }

    private static MemoryPlanActionKind MemoryAction(MemoryLayoutSegment segment)
    {
        return segment.SourceSpaceId == CompositionAddressSpaceIds.ReferenceBase &&
            segment.Disposition == MemoryWorkflowDisposition.WillReplace
                ? MemoryPlanActionKind.Restore
                : segment.SourceSpaceId == CompositionAddressSpaceIds.TpBWork
                    ? MemoryPlanActionKind.TransformAndOverlay
                    : segment.Disposition switch
                    {
                        MemoryWorkflowDisposition.WillWrite =>
                            segment.ProcessorEffect == MemoryProcessorEffect.DeclaredWrite
                                ? MemoryPlanActionKind.Postbuild
                                : MemoryPlanActionKind.Copy,
                        MemoryWorkflowDisposition.WillReplace =>
                            segment.ProcessorEffect == MemoryProcessorEffect.DeclaredWrite
                                ? MemoryPlanActionKind.ReplaceAndCrc
                                : MemoryPlanActionKind.Replace,
                        MemoryWorkflowDisposition.Kept => MemoryPlanActionKind.Preserve,
                        MemoryWorkflowDisposition.Blank or MemoryWorkflowDisposition.Resolved =>
                            MemoryPlanActionKind.Initialize,
                        MemoryWorkflowDisposition.DpAbBase => MemoryPlanActionKind.Copy,
                        MemoryWorkflowDisposition.TpaOverlay => MemoryPlanActionKind.Overlay,
                        MemoryWorkflowDisposition.TpbOverlay => MemoryPlanActionKind.TransformAndOverlay,
                        _ => MemoryPlanActionKind.Project,
                    };
    }

    private static MemoryPlanSource MemorySource(MemoryLayoutSegment segment)
    {
        return IsReferenceKept(segment) && segment.ContentRole == MemoryContentRole.CtrlRam
                ? new(
                    MemoryPlanSourceKind.Technical,
                    ShellTextResources.GetCtrlRamRegionTechnicalLabel(segment.CtrlRamRegionRole))
                : IsReferenceKept(segment)
                ? new(MemoryPlanSourceKind.BaseFirmware)
                : segment.ContentRole is
            MemoryContentRole.CustomerInformation or
            MemoryContentRole.Reserved
                ? new(MemoryPlanSourceKind.Reserved)
                : segment.ContentRole == MemoryContentRole.Unmapped &&
                    segment.SourceSpaceId is null
                    ? new(MemoryPlanSourceKind.Unmapped)
                : segment.SourceSpaceId is { } sourceSpaceId
                    ? DynamicCtrlRamReplacementIds.TryFormatDisplayLabel(sourceSpaceId, out _)
                        ? new(
                            MemoryPlanSourceKind.Technical,
                            DynamicCtrlRamReplacementIds.FormatRegionDisplayLabel(segment.RegionId))
                        : AddressSpaceSource(sourceSpaceId)
                    : segment.Disposition switch
                    {
                        MemoryWorkflowDisposition.Blank => new(MemoryPlanSourceKind.Reserved),
                        MemoryWorkflowDisposition.Kept => new(MemoryPlanSourceKind.BaseFirmware),
                        MemoryWorkflowDisposition.Resolved or
                        MemoryWorkflowDisposition.WillWrite or
                        MemoryWorkflowDisposition.WillReplace or
                        MemoryWorkflowDisposition.DpAbBase or
                        MemoryWorkflowDisposition.TpaOverlay or
                        MemoryWorkflowDisposition.TpbOverlay => segment.ContentRole switch
                        {
                            MemoryContentRole.Dp => new(MemoryPlanSourceKind.DpBin),
                            MemoryContentRole.Tp => new(MemoryPlanSourceKind.TpBin),
                            MemoryContentRole.TpBackup => new(MemoryPlanSourceKind.Tpb),
                            MemoryContentRole.Ldc => new(MemoryPlanSourceKind.LdcBin),
                            MemoryContentRole.CtrlRam => new(MemoryPlanSourceKind.CtrlRamBin),
                            MemoryContentRole.CustomerInformation or MemoryContentRole.Reserved =>
                                new(MemoryPlanSourceKind.Reserved),
                            MemoryContentRole.Unmapped => new(MemoryPlanSourceKind.Unmapped),
                            MemoryContentRole.General => new(MemoryPlanSourceKind.Output),
                            _ => new(MemoryPlanSourceKind.Output),
                        },
                        _ => new(MemoryPlanSourceKind.Output),
                    };
    }

    private static string MemoryDetail(
        MemoryLayoutSnapshot layout,
        MemoryLayoutSegment segment,
        ShellTextResources text)
    {
        return text.FormatMemoryLayoutTechnicalDetail(
            segment.RegionId,
            layout.BlankFillByte,
            segment.ContributingOperations);
    }

    private static string MemoryCompactDetail(
        MemoryLayoutSegment segment,
        string sourceLabel,
        ShellTextResources text)
    {
        string compactSourceLabel = segment.SourceSpaceId switch
        {
            CompositionAddressSpaceIds.DpReplacement => "DP BIN",
            CompositionAddressSpaceIds.LdcReplacement => "LDC BIN",
            _ => sourceLabel,
        };
        return IsReferenceKept(segment)
            ? text.GetOutputLayoutBaseDetail(
                segment.Disposition == MemoryWorkflowDisposition.WillReplace)
            : segment.ContentRole == MemoryContentRole.CustomerInformation
            ? text.GetMemoryPlanDetail(segment.SourceSpaceId switch
            {
                CompositionAddressSpaceIds.DpInput or CompositionAddressSpaceIds.DpAbInput =>
                    MemoryPlanDetailKind.ProtectedCustomerInformationFromDp,
                CompositionAddressSpaceIds.DpReplacement or
                    CompositionAddressSpaceIds.InitialCodeReplacement =>
                        MemoryPlanDetailKind.ProtectedCustomerInformationFromDpReplacement,
                _ => MemoryPlanDetailKind.ReservedUnwritten,
            })
            : segment.ContentRole == MemoryContentRole.Reserved
            ? text.GetMemoryPlanDetail(MemoryPlanDetailKind.ReservedUnwritten)
            : segment.ContentRole == MemoryContentRole.Unmapped && segment.SourceSpaceId is null
            ? text.GetMemoryPlanDetail(MemoryPlanDetailKind.Unmapped)
            : segment.SourceSpaceId switch
            {
                CompositionAddressSpaceIds.DpInput or CompositionAddressSpaceIds.DpAbInput =>
                    text.GetMemoryPlanDetail(MemoryPlanDetailKind.CopiedFromDp),
                CompositionAddressSpaceIds.TpInput or CompositionAddressSpaceIds.TpAInput =>
                    text.GetMemoryPlanDetail(MemoryPlanDetailKind.OverlaidFromTp),
                CompositionAddressSpaceIds.ReferenceBase => text.GetOutputLayoutBaseDetail(
                    segment.Disposition == MemoryWorkflowDisposition.WillReplace),
                _ => text.FormatOutputLayoutSourceDetail(compactSourceLabel, segment.Disposition),
            };
    }

    private static MemoryPlanSource AddressSpaceSource(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            CompositionAddressSpaceIds.DpInput => new(MemoryPlanSourceKind.DpBin),
            CompositionAddressSpaceIds.TpInput => new(MemoryPlanSourceKind.TpBin),
            CompositionAddressSpaceIds.LdcInput => new(MemoryPlanSourceKind.LdcBin),
            CompositionAddressSpaceIds.ReferenceBase => new(MemoryPlanSourceKind.BaseFirmware),
            CompositionAddressSpaceIds.DpReplacement or
                CompositionAddressSpaceIds.InitialCodeReplacement =>
                    new(MemoryPlanSourceKind.DpReplacementBin),
            CompositionAddressSpaceIds.LdcReplacement =>
                new(MemoryPlanSourceKind.LdcReplacementBin),
            CompositionAddressSpaceIds.DpAbInput => new(MemoryPlanSourceKind.DpAb),
            CompositionAddressSpaceIds.TpAInput => new(MemoryPlanSourceKind.Tpa),
            CompositionAddressSpaceIds.TpBInput or CompositionAddressSpaceIds.TpBWork =>
                new(MemoryPlanSourceKind.Tpb),
            _ => new(MemoryPlanSourceKind.Technical, addressSpaceId),
        };
    }

    private static string FormatMemoryRange(ByteRange range)
    {
        return FormattableString.Invariant(
            $"{FormatMemoryAddressRange(range)} ({FormatMemoryLength(range)})");
    }

    private static string FormatMemoryAddressRange(ByteRange range)
    {
        return FormattableString.Invariant(
            $"0x{range.Start:X5}-0x{range.EndExclusive - 1:X5}");
    }

    private static string FormatMemoryLength(ByteRange range)
    {
        return FormattableString.Invariant($"len 0x{range.Length:X}");
    }

    private static MemoryCoverageFillRole ResolveCoverageFillRole(MemoryLayoutSegment segment)
    {
        if (segment.ContentRole == MemoryContentRole.CtrlRam)
        {
            return ResolveCtrlRamCoverageFillRole(segment.CtrlRamRegionRole);
        }

        if (IsReferenceKept(segment))
        {
            return MemoryCoverageFillRole.Kept;
        }

        if (segment.ContentRole is MemoryContentRole.CustomerInformation or MemoryContentRole.Reserved)
        {
            return MemoryCoverageFillRole.Neutral;
        }

        MemoryContentRole role = segment.SourceSpaceId switch
        {
            CompositionAddressSpaceIds.DpInput or
            CompositionAddressSpaceIds.DpReplacement or
            CompositionAddressSpaceIds.DpAbInput or
            CompositionAddressSpaceIds.InitialCodeReplacement => MemoryContentRole.Dp,
            CompositionAddressSpaceIds.TpInput or
            CompositionAddressSpaceIds.TpAInput => MemoryContentRole.Tp,
            CompositionAddressSpaceIds.TpBInput or
            CompositionAddressSpaceIds.TpBWork => MemoryContentRole.TpBackup,
            CompositionAddressSpaceIds.LdcInput or
            CompositionAddressSpaceIds.LdcReplacement => MemoryContentRole.Ldc,
            _ => segment.ContentRole,
        };
        return role switch
        {
            MemoryContentRole.Dp => MemoryCoverageFillRole.Dp,
            MemoryContentRole.Tp => MemoryCoverageFillRole.Tp,
            MemoryContentRole.TpBackup => MemoryCoverageFillRole.TpBackup,
            MemoryContentRole.Ldc => MemoryCoverageFillRole.Ldc,
            MemoryContentRole.CtrlRam => ResolveCtrlRamCoverageFillRole(
                segment.CtrlRamRegionRole),
            MemoryContentRole.General => MemoryCoverageFillRole.Source,
            MemoryContentRole.CustomerInformation or
            MemoryContentRole.Reserved or
            MemoryContentRole.Unmapped => MemoryCoverageFillRole.Neutral,
            _ => MemoryCoverageFillRole.Neutral,
        };
    }

    internal static MemoryCoverageFillRole ResolveCtrlRamCoverageFillRole(
        CtrlRamRegionRole role)
    {
        return role switch
        {
            CtrlRamRegionRole.Nf => MemoryCoverageFillRole.CtrlRamNf,
            CtrlRamRegionRole.Normal => MemoryCoverageFillRole.CtrlRamNormal,
            CtrlRamRegionRole.Mp => MemoryCoverageFillRole.CtrlRamMp,
            CtrlRamRegionRole.Vn => MemoryCoverageFillRole.CtrlRamVn,
            CtrlRamRegionRole.Vector => MemoryCoverageFillRole.CtrlRamVector,
            CtrlRamRegionRole.DiffDlm => MemoryCoverageFillRole.DiffDlm,
            CtrlRamRegionRole.Other => MemoryCoverageFillRole.CtrlRam,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
    }

    private static bool IsReferenceKept(MemoryLayoutSegment segment)
    {
        return segment.Disposition == MemoryWorkflowDisposition.Kept ||
            segment.SourceSpaceId == CompositionAddressSpaceIds.ReferenceBase;
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
