using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
    internal Task RefreshSelectedMergeFirmwareInspectionsAsync(
        string? applyVerifiedContextSlotId = null,
        CancellationToken cancellationToken = default)
    {
        return ActiveInspectionContext is { IsMerge: true } context
            ? RefreshSelectedFirmwareInspectionsAsync(
                context,
                InspectionSlots(context),
                applyVerifiedContextSlotId,
                cancellationToken)
            : Task.CompletedTask;
    }

    internal Task RefreshRetainedMergeFirmwareInspectionsIfStaleAsync()
    {
        return ActiveInspectionContext is { IsMerge: true } context &&
            RequiresCurrentSelectorReinspection(context)
                ? RefreshSelectedFirmwareInspectionsAsync(
                    context,
                    InspectionSlots(context),
                    applyVerifiedContextSlotId: null,
                    CancellationToken.None)
                : Task.CompletedTask;
    }

    internal Task RefreshSelectedReplaceFirmwareInspectionsAsync(
        string? applyVerifiedContextSlotId = null)
    {
        return RefreshSelectedReplaceFirmwareInspectionsAsync(
            applyVerifiedContextSlotId,
            CancellationToken.None);
    }

    private Task RefreshSelectedReplaceFirmwareInspectionsAsync(
        string? applyVerifiedContextSlotId,
        CancellationToken cancellationToken)
    {
        return ActiveInspectionContext is { IsReplace: true } context
            ? RefreshSelectedFirmwareInspectionsAsync(
                context,
                InspectionSlots(context),
                applyVerifiedContextSlotId,
                cancellationToken)
            : Task.CompletedTask;
    }

    private bool TryRefreshRetainedReplaceFirmwareInspectionsIfStale()
    {
        if (ActiveInspectionContext is not { IsReplace: true } context ||
            !RequiresCurrentSelectorReinspection(context))
        {
            return false;
        }

        _ = RefreshSelectedFirmwareInspectionsAsync(
            context,
            InspectionSlots(context),
            applyVerifiedContextSlotId: null,
            CancellationToken.None);
        return true;
    }

    private bool RequiresCurrentSelectorReinspection(WorkflowInspectionContext context)
    {
        return _selectorPublication is { } publication &&
            InspectionSlots(context)
                .Where(static slot => slot.HasFile)
                .Any(slot => RequiresCurrentSelectorReinspection(
                    context,
                    slot.CurrentInspectionProjection,
                    publication.ResolutionToken));
    }

    private static bool RequiresCurrentSelectorReinspection(
        WorkflowInspectionContext context,
        FirmwareInspectionSnapshot? projection,
        ResolutionToken currentToken)
    {
        if (projection is null)
        {
            return true;
        }

        ResolutionToken? token = projection.InputSlotStatus?.ResolutionToken ??
            projection.InputSlotCatalog?.ResolutionToken;
        return token is null
            ? !context.IsGeneralReplace
            : token != currentToken;
    }
}
