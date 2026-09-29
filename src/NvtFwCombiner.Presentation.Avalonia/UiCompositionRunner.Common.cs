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
        return GetMemoryDisplay(services, acceptedSession, text, out _, admission, ctrlRamRegions);
    }

    internal static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetMemoryDisplay(
        PresentationCompositionServices services,
        ActiveSessionSnapshot acceptedSession,
        ShellTextResources text,
        out IReadOnlyList<MemoryCoverageSegmentViewModel> overview,
        GeneralAuthoringAdmissionResult? admission = null,
        IReadOnlyList<CtrlRamRegion>? ctrlRamRegions = null)
    {
        return GetMemoryDisplay(services, acceptedSession, text, out overview, out _, admission, ctrlRamRegions);
    }

    internal static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetMemoryDisplay(
        PresentationCompositionServices services,
        ActiveSessionSnapshot acceptedSession,
        ShellTextResources text,
        out IReadOnlyList<MemoryCoverageSegmentViewModel> overview,
        out MemoryLayoutSnapshot snapshot,
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
        snapshot = layout;
        overview = GetMemoryOverview(layout, text);
        IReadOnlyList<MemoryLayoutConflict> conflicts = admission is null
            ? []
            : MemoryLayoutProjector.ProjectAdmissionConflicts(admission, layout.Capacity);
        return (
            FormatMemoryRange(new ByteRange(0, layout.Capacity)),
            [
                .. layout.AfterSegments.Where(static segment => segment.IsPrimaryContent).Select(segment => ToMemoryMapRow(layout, segment, text)),
                .. conflicts.Select(conflict => ToMemoryMapRow(conflict, text)),
            ],
            [
                .. layout.AfterSegments.Select(segment => ToMemoryCoverageSegment(layout, segment, text)),
                .. conflicts.Select(conflict => ToMemoryCoverageSegment(layout, conflict, text)),
            ]);
    }

    internal static IReadOnlyList<MemoryCoverageSegmentViewModel> GetMemoryOverview(
        MemoryLayoutSnapshot layout, ShellTextResources text, MemoryLayoutBankLocator? bank = null)
    {
        if (bank is not null && !layout.Banks.Contains(bank))
        {
            throw new ArgumentException("The viewport must belong to the accepted memory snapshot.", nameof(bank));
        }
        ByteRange viewport = bank?.Range ?? new ByteRange(0, layout.Capacity);
        return [.. layout.SectionLocators.Where(section =>
            section.Range.Start < viewport.EndExclusive && viewport.Start < section.Range.EndExclusive).Select(section =>
        {
            long start = Math.Max(section.Range.Start, viewport.Start);
            var range = new ByteRange(start, Math.Min(section.Range.EndExclusive, viewport.EndExclusive) - start);
            string title = text.GetMemorySectionTitle(section.ContentRole);
            return new MemoryCoverageSegmentViewModel(
                FormatMemoryRange(range), title,
                section.IsImageContainer ? text.MemoryDpImageContextDetail :
                    section.IsImageOverlay ? text.MemoryTpOverlayContextDetail : text.MemorySectionContextDetail,
                section.ContentRole == MemoryContentRole.Tp ? MemoryCoverageFillRole.Tp :
                section.ContentRole == MemoryContentRole.Dp ? MemoryCoverageFillRole.Dp :
                MemoryCoverageFillRole.Neutral, range.Length,
                text: text, rangeStart: range.Start, rangeEndExclusive: range.EndExclusive,
                addressRangeLabel: FormatMemoryAddressRange(range),
                contentRole: section.ContentRole, displayTitle: title, addressSpaceId: section.AddressSpaceId,
                processingFacts: [.. section.Fields.Select(field => new MemoryRegionFact(field.CanonicalRegion.RegionId,
                    FormattableString.Invariant($"{field.AddressSpaceId} [0x{field.Range.Start:X},0x{field.Range.EndExclusive:X})")))]);
        })];
    }

    /// <summary>Projects a typed pending state when no exact authoring publication exists.</summary>
    internal static (
        string RangeLabel,
        IReadOnlyList<MemoryMapRowViewModel> Rows,
        IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments) GetPendingMemoryDisplay(
        ShellTextResources text,
        IEnumerable<FirmwareSlotViewModel> slots,
        MemoryPendingPrerequisite fallbackPrerequisite,
        ActiveSessionSnapshot? authoring = null, IReadOnlyList<string>? requiredAddressSpaces = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(slots);
        FirmwareSlotViewModel[] current = [.. slots];
        MemoryLayoutPendingDisplay projection = MemoryLayoutProjector.ProjectPending(authoring,
            current.Select(static slot => new MemoryLayoutPendingInput(slot.SlotId, slot.AddressSpaceId,
                !slot.DeclaredIsOptional, slot.FilePath, slot.InputIssueStatus, slot.CompiledSlotId,
                slot.InputAvailabilityIssue, slot.InputAuthoringIssues)), fallbackPrerequisite, requiredAddressSpaces);
        FirmwareSlotViewModel? pending = current.FirstOrDefault(slot => slot.SlotId == projection.SlotId);
        (string waitingLabel, string detail) = projection.Readiness == MemoryLayoutReadiness.Blocked
            ? text.GetBlockingInputText(projection, pending!.Title)
            : pending is null ? text.GetPendingInputText(projection.FallbackPrerequisite)
            : text.GetPendingInputText(projection.ArtifactKind, pending.Title);
        string unavailableLabel = text.NotAvailableLabel;
        return (
            waitingLabel,
            [new MemoryMapRowViewModel(
                unavailableLabel,
                new MemoryPlanSource(MemoryPlanSourceKind.NoOutput),
                projection.Action,
                new MemoryPlanSource(MemoryPlanSourceKind.Localized, waitingLabel),
                detail,
                text)],
            [new MemoryCoverageSegmentViewModel(
                unavailableLabel,
                waitingLabel,
                detail,
                projection.Severity == MemoryDiagnosticSeverity.Error ? MemoryCoverageFillRole.Conflict : MemoryCoverageFillRole.Neutral,
                300d,
                diagnosticSeverity: projection.Severity,
                text: text,
                addressRangeLabel: unavailableLabel,
                lengthLabel: string.Empty,
                compactDetail: detail,
                logicalCoverageGroupId: $"pending:{pending?.SlotId ?? fallbackPrerequisite.ToString()}")]);
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
            MemorySource(before, text),
            segment.Action,
            MemorySource(segment, text),
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
        bool initialized = segment.InitializationFillByte.HasValue;
        string sourceLabel = initialized
            ? $"0x{segment.InitializationFillByte:X2}"
            : text.GetMemoryPlanSourceLabel(segment.ContentSource is { } content && content.SourceSpaceId != segment.SourceSpaceId
                ? ArtifactSource(content.ArtifactKind, content.SourceSpaceId) : MemorySource(segment, text));
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
            usesBaseFirmwarePattern: segment.IsReferenceContent,
            regionId: segment.RegionId,
            sourceSlotId: segment.SourceSlotId,
            logicalSourceLabel: logicalSourceLabel,
            preservationDetails: segment.PreservationDetails,
            text: text,
            regionGroup: segment.RegionGroup,
            displayGroup: layout.AfterDisplayGroups[segment.LogicalCoverageGroupId],
            rangeStart: segment.Range.Start,
            rangeEndExclusive: segment.Range.EndExclusive,
            addressSpaceId: segment.AddressSpaceId,
            isPrimaryContent: segment.IsPrimaryContent,
            contentArtifactIdentity: segment.ContentSource?.ArtifactIdentity,
            addressRangeLabel: FormatMemoryAddressRange(segment.Range),
            lengthLabel: FormatMemoryLength(segment.Range),
            compactDetail: MemoryCompactDetail(segment, sourceLabel, text),
            logicalCoverageGroupId: segment.LogicalCoverageGroupId,
            contentRole: segment.ContentRole,
            ctrlRamRegionRole: segment.CtrlRamRegionRole,
            processingFacts: text.FormatMemoryLayoutTechnicalFacts(segment.RegionId, layout.BlankFillByte, segment.ContributingOperations),
            sourceFieldLabel: initialized ? text.MemoryInitializationLabel : text.MemorySourceLabel,
            displayTitle: segment.ContentRole == MemoryContentRole.CtrlRam
                ? text.FormatMemoryCtrlRamTitle(segment.CtrlRamRegionRole, segment.RegionGroup)
                : segment.SourceSpaceId is null || segment.ContentRole == MemoryContentRole.Reserved
                ? text.GetMemoryContentTitle(segment.ContentRole, segment.CtrlRamRegionRole)
                : null);
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
            compactDetail: detail,
            logicalCoverageGroupId: $"conflict:{conflict.ConflictId}");
    }

    private static MemoryPlanSource MemorySource(MemoryLayoutSegment segment, ShellTextResources text)
    {
        (bool isInitialization, string value, _) = text.GetMemoryUnassignedSource(segment.InitializationFillByte);
        return segment.IsReferenceContent
                ? new(MemoryPlanSourceKind.BaseFirmware)
                : segment.SourceSpaceId is { } sourceSpaceId
                    ? DynamicCtrlRamReplacementIds.TryFormatDisplayLabel(sourceSpaceId, out string sourceLabel)
                        ? new(
                            MemoryPlanSourceKind.Technical,
                            segment.ContentRole == MemoryContentRole.CtrlRam
                                ? DynamicCtrlRamReplacementIds.FormatRegionDisplayLabel(segment.BankRegion?.LocalRegion.RegionId ?? segment.RegionId)
                                : sourceLabel)
                        : ArtifactSource(segment.SourceKind, sourceSpaceId)
                    : new(MemoryPlanSourceKind.Localized, isInitialization
                        ? $"{text.MemoryInitializationLabel}: {value}"
                        : value);
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
        string compactSourceLabel = segment.SourceKind switch
        {
            MemoryArtifactKind.DpReplacement => "DP BIN",
            MemoryArtifactKind.LdcReplacement => "LDC BIN",
            MemoryArtifactKind.Other or MemoryArtifactKind.Dp or MemoryArtifactKind.Tp or MemoryArtifactKind.Ldc or
                MemoryArtifactKind.Reference or MemoryArtifactKind.DpAb or MemoryArtifactKind.TpA or MemoryArtifactKind.TpB or MemoryArtifactKind.TpBWork => sourceLabel,
            _ => throw new ArgumentOutOfRangeException(nameof(segment)),
        };
        return segment.DetailKind switch
        {
            MemoryPlanDetailKind.Initialization or MemoryPlanDetailKind.Unassigned => text.GetMemoryUnassignedSource(segment.InitializationFillByte).Detail,
            MemoryPlanDetailKind.ReferenceKept => text.GetOutputLayoutBaseDetail(false),
            MemoryPlanDetailKind.ReferenceRestored => text.GetOutputLayoutBaseDetail(true),
            MemoryPlanDetailKind.Source => text.FormatOutputLayoutSourceDetail(compactSourceLabel, segment.Disposition),
            MemoryPlanDetailKind.ProtectedCustomerInformationFromDp or MemoryPlanDetailKind.ProtectedCustomerInformationFromDpReplacement or
                MemoryPlanDetailKind.ReservedUnwritten or MemoryPlanDetailKind.Unmapped or MemoryPlanDetailKind.CopiedFromDp or
                MemoryPlanDetailKind.OverlaidFromTp => text.GetMemoryPlanDetail(segment.DetailKind),
            _ => throw new ArgumentOutOfRangeException(nameof(segment)),
        };
    }

    private static MemoryPlanSource ArtifactSource(MemoryArtifactKind kind, string identity)
    {
        return kind switch
        {
            MemoryArtifactKind.Dp => new(MemoryPlanSourceKind.DpBin),
            MemoryArtifactKind.DpReplacement => new(MemoryPlanSourceKind.DpReplacementBin),
            MemoryArtifactKind.Tp => new(MemoryPlanSourceKind.TpBin),
            MemoryArtifactKind.Ldc => new(MemoryPlanSourceKind.LdcBin),
            MemoryArtifactKind.LdcReplacement => new(MemoryPlanSourceKind.LdcReplacementBin),
            MemoryArtifactKind.Reference => new(MemoryPlanSourceKind.BaseFirmware),
            MemoryArtifactKind.DpAb => new(MemoryPlanSourceKind.DpAb),
            MemoryArtifactKind.TpA => new(MemoryPlanSourceKind.Tpa),
            MemoryArtifactKind.TpB or MemoryArtifactKind.TpBWork => new(MemoryPlanSourceKind.Tpb),
            MemoryArtifactKind.Other => new(MemoryPlanSourceKind.Technical, identity),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
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

        if (segment.IsReferenceContent)
        {
            return MemoryCoverageFillRole.Kept;
        }

        if (segment.ContentRole is MemoryContentRole.CustomerInformation or MemoryContentRole.Reserved)
        {
            return MemoryCoverageFillRole.Neutral;
        }

        MemoryContentRole role = segment.ContentAttribution switch
        {
            MemoryArtifactKind.Dp or MemoryArtifactKind.DpReplacement or MemoryArtifactKind.DpAb => MemoryContentRole.Dp,
            MemoryArtifactKind.Tp or MemoryArtifactKind.TpA => MemoryContentRole.Tp,
            MemoryArtifactKind.TpB or MemoryArtifactKind.TpBWork => MemoryContentRole.TpBackup,
            MemoryArtifactKind.Ldc or MemoryArtifactKind.LdcReplacement => MemoryContentRole.Ldc,
            MemoryArtifactKind.Other or MemoryArtifactKind.Reference => segment.ContentRole,
            _ => throw new ArgumentOutOfRangeException(nameof(segment)),
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

    private static string ToRange(long start, long length)
    {
        return FormattableString.Invariant($"0x{start:X5}-0x{start + length - 1:X5}");
    }

    private static string ToLength(long length)
    {
        return FormattableString.Invariant($"len 0x{length:X}");
    }
}
