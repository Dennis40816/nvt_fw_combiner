using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.MemoryLayout;

/// <summary>Source artifact attribution, independent of map content role and workflow effect.</summary>
public enum MemoryArtifactKind
{
    /// <summary>No recognized canonical artifact kind; retain the original identity.</summary>
    Other,
    /// <summary>DP input.</summary>
    Dp,
    /// <summary>DP replacement input.</summary>
    DpReplacement,
    /// <summary>TP input.</summary>
    Tp,
    /// <summary>LDC input.</summary>
    Ldc,
    /// <summary>LDC replacement input.</summary>
    LdcReplacement,
    /// <summary>Reference input.</summary>
    Reference,
    /// <summary>DP AB input.</summary>
    DpAb,
    /// <summary>TP A input.</summary>
    TpA,
    /// <summary>TP B input.</summary>
    TpB,
    /// <summary>Declared TP B transform workspace.</summary>
    TpBWork,
}

/// <summary>Planned display action; generic postprocessing does not assert a CRC algorithm.</summary>
public enum MemoryPlanActionKind
{
    /// <summary>Pending input.</summary>
    Browse,
    /// <summary>Blocked input.</summary>
    Blocked,
    /// <summary>Restore reference content.</summary>
    Restore,
    /// <summary>Transform and overlay.</summary>
    TransformAndOverlay,
    /// <summary>Declared postprocessing.</summary>
    Postbuild,
    /// <summary>Copy content.</summary>
    Copy,
    /// <summary>Replace with generic declared postprocessing; historical identifier.</summary>
    ReplaceAndCrc,
    /// <summary>Replace content.</summary>
    Replace,
    /// <summary>Preserve reference.</summary>
    Preserve,
    /// <summary>Initialize the output.</summary>
    Initialize,
    /// <summary>Overlay content.</summary>
    Overlay,
    /// <summary>Project geometry without a more specific action.</summary>
    Project,
}

/// <summary>Application-owned compact-detail facts, localized by consumers.</summary>
public enum MemoryPlanDetailKind
{
    /// <summary>Untouched explicit blank initialization.</summary>
    Initialization,
    /// <summary>No assigned input, including a source-less write.</summary>
    Unassigned,
    /// <summary>Reference content preserved.</summary>
    ReferenceKept,
    /// <summary>Reference content restored.</summary>
    ReferenceRestored,
    /// <summary>Protected customer information from DP.</summary>
    ProtectedCustomerInformationFromDp,
    /// <summary>Protected customer information from replacement DP.</summary>
    ProtectedCustomerInformationFromDpReplacement,
    /// <summary>Protected content with no more specific attribution.</summary>
    ReservedUnwritten,
    /// <summary>Explicit unmapped range.</summary>
    Unmapped,
    /// <summary>DP input copy.</summary>
    CopiedFromDp,
    /// <summary>TP input overlay.</summary>
    OverlaidFromTp,
    /// <summary>Other declared source and disposition.</summary>
    Source,
}

public static partial class MemoryLayoutProjector
{
    /// <summary>Translates canonical artifact identity once; unknown identities remain unknown.</summary>
    public static MemoryArtifactKind GetArtifactKind(string? addressSpaceId)
    {
        return addressSpaceId switch
        {
            CompositionAddressSpaceIds.DpInput => MemoryArtifactKind.Dp,
            CompositionAddressSpaceIds.DpReplacement or CompositionAddressSpaceIds.InitialCodeReplacement => MemoryArtifactKind.DpReplacement,
            CompositionAddressSpaceIds.TpInput => MemoryArtifactKind.Tp,
            CompositionAddressSpaceIds.LdcInput => MemoryArtifactKind.Ldc,
            CompositionAddressSpaceIds.LdcReplacement => MemoryArtifactKind.LdcReplacement,
            CompositionAddressSpaceIds.ReferenceBase => MemoryArtifactKind.Reference,
            CompositionAddressSpaceIds.DpAbInput => MemoryArtifactKind.DpAb,
            CompositionAddressSpaceIds.TpAInput => MemoryArtifactKind.TpA,
            CompositionAddressSpaceIds.TpBInput => MemoryArtifactKind.TpB,
            CompositionAddressSpaceIds.TpBWork => MemoryArtifactKind.TpBWork,
            _ => MemoryArtifactKind.Other,
        };
    }

    /// <summary>Publishes one display group for an existing logical coverage group.</summary>
    public static ReplaceRegionGroup GetDisplayGroup(IEnumerable<MemoryLayoutSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        MemoryLayoutSegment[] items = [.. segments];
        if (items.Length == 0) { throw new ArgumentException("A display group requires coverage.", nameof(segments)); }
        if (items.All(static item => !item.IsPlannedWrite && item.IsReferenceContent)) { return ReplaceRegionGroup.Base; }
        ReplaceRegionGroup[] selected = [.. items.Where(static item => item.IsPlannedWrite).Select(static item => item.RegionGroup).Distinct()];
        ReplaceRegionGroup[] groups = selected.Length > 0 ? selected : [.. items.Select(static item => item.RegionGroup).Distinct()];
        return groups.Length == 1 ? groups[0] : ReplaceRegionGroup.Common;
    }
}

public sealed partial class MemoryLayoutSegment
{
    /// <summary>Writer/source identity, distinct from accepted content attribution.</summary>
    public MemoryArtifactKind SourceKind => MemoryLayoutProjector.GetArtifactKind(SourceSpaceId);
    /// <summary>Accepted content attribution, or the declared writer source when no input is admitted.</summary>
    public MemoryArtifactKind ContentAttribution => ContentSource?.ArtifactKind ?? SourceKind;
    /// <summary>Preserved or explicitly restored reference content; does not imply no write.</summary>
    public bool IsReferenceContent => Disposition == MemoryWorkflowDisposition.Kept || SourceKind == MemoryArtifactKind.Reference;
    /// <summary>Whether the compiled plan declares a write, independent of input admission.</summary>
    public bool IsPlannedWrite => Disposition is MemoryWorkflowDisposition.WillWrite or MemoryWorkflowDisposition.WillReplace or
        MemoryWorkflowDisposition.DpAbBase or MemoryWorkflowDisposition.TpaOverlay or MemoryWorkflowDisposition.TpbOverlay;
    /// <summary>Explicit untouched initializer byte; null for unassigned, reference or source-less writes.</summary>
    public byte? InitializationFillByte { get; private init; }
    /// <summary>Typed action projected from the existing disposition and processor facts.</summary>
    public MemoryPlanActionKind Action => SourceKind == MemoryArtifactKind.Reference && Disposition == MemoryWorkflowDisposition.WillReplace
        ? MemoryPlanActionKind.Restore : SourceKind == MemoryArtifactKind.TpBWork ? MemoryPlanActionKind.TransformAndOverlay : Disposition switch
        {
            MemoryWorkflowDisposition.WillWrite => ProcessorEffect == MemoryProcessorEffect.DeclaredWrite ? MemoryPlanActionKind.Postbuild : MemoryPlanActionKind.Copy,
            MemoryWorkflowDisposition.WillReplace => ProcessorEffect == MemoryProcessorEffect.DeclaredWrite ? MemoryPlanActionKind.ReplaceAndCrc : MemoryPlanActionKind.Replace,
            MemoryWorkflowDisposition.Kept => MemoryPlanActionKind.Preserve,
            MemoryWorkflowDisposition.Blank or MemoryWorkflowDisposition.Resolved => MemoryPlanActionKind.Initialize,
            MemoryWorkflowDisposition.DpAbBase => MemoryPlanActionKind.Copy,
            MemoryWorkflowDisposition.TpaOverlay => MemoryPlanActionKind.Overlay,
            MemoryWorkflowDisposition.TpbOverlay => MemoryPlanActionKind.TransformAndOverlay,
            _ => MemoryPlanActionKind.Project,
        };
    /// <summary>Typed detail; source-less writes must not be described as initialization.</summary>
    public MemoryPlanDetailKind DetailKind => SourceSpaceId is null
        ? InitializationFillByte.HasValue ? MemoryPlanDetailKind.Initialization : MemoryPlanDetailKind.Unassigned
        : IsReferenceContent ? Disposition == MemoryWorkflowDisposition.WillReplace ? MemoryPlanDetailKind.ReferenceRestored : MemoryPlanDetailKind.ReferenceKept
        : ContentRole == MemoryContentRole.CustomerInformation ? SourceKind switch
        {
            MemoryArtifactKind.Dp or MemoryArtifactKind.DpAb => MemoryPlanDetailKind.ProtectedCustomerInformationFromDp,
            MemoryArtifactKind.DpReplacement => MemoryPlanDetailKind.ProtectedCustomerInformationFromDpReplacement,
            MemoryArtifactKind.Other or MemoryArtifactKind.Tp or MemoryArtifactKind.Ldc or MemoryArtifactKind.LdcReplacement or
                MemoryArtifactKind.Reference or MemoryArtifactKind.TpA or MemoryArtifactKind.TpB or MemoryArtifactKind.TpBWork => MemoryPlanDetailKind.ReservedUnwritten,
            _ => throw new InvalidOperationException("Unknown artifact kind."),
        } : SourceKind switch
        {
            MemoryArtifactKind.Dp or MemoryArtifactKind.DpAb => MemoryPlanDetailKind.CopiedFromDp,
            MemoryArtifactKind.Tp or MemoryArtifactKind.TpA => MemoryPlanDetailKind.OverlaidFromTp,
            MemoryArtifactKind.Other or MemoryArtifactKind.DpReplacement or MemoryArtifactKind.Ldc or MemoryArtifactKind.LdcReplacement or
                MemoryArtifactKind.Reference or MemoryArtifactKind.TpB or MemoryArtifactKind.TpBWork => MemoryPlanDetailKind.Source,
            _ => throw new InvalidOperationException("Unknown artifact kind."),
        };
}
