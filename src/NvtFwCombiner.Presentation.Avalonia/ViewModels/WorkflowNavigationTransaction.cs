using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed record WorkflowContextSelection(
    ShellPage Page,
    string Mode,
    bool ShowNumber,
    string IcId,
    string Number);

internal sealed record WorkflowContextTarget(
    ShellPage Page,
    string Mode,
    bool ShowNumber);

internal static class WorkflowNavigationTransaction
{
    internal static WorkflowPageCatalogReconciliation ReconcileCatalogPage(
        CapabilitySelectorPublication publication,
        ShellPage page,
        string previousIc,
        string previousNumber,
        MergePresentationViewModel merge,
        ReplacePresentationViewModel replace)
    {
        string icId = WorkflowSelectorProjection.ContextIc(publication, previousIc, page);
        bool modeChanged = page switch
        {
            ShellPage.Merge => merge.StageAuthorableModeForCatalogReconciliation(
                workflowId => !string.IsNullOrWhiteSpace(icId) &&
                    publication.IsWorkflowAuthorable(icId, workflowId)),
            ShellPage.Replace => replace.StageAuthorableModeForCatalogReconciliation(
                workflowId => !string.IsNullOrWhiteSpace(icId) &&
                    publication.IsWorkflowAuthorable(icId, workflowId)),
            ShellPage.Home or ShellPage.HexEditor => throw new ArgumentException(
                "Workflow page expected.",
                nameof(page)),
            _ => throw new InvalidOperationException("Unknown shell page."),
        };
        string number = WorkflowSelectorProjection.Number(
            publication,
            icId,
            previousNumber,
            page == ShellPage.Merge && merge.IsAbCodeMergeModeSelected);
        return new(
            icId,
            number,
            !string.Equals(previousIc, icId, StringComparison.Ordinal) ||
                !string.Equals(previousNumber, number, StringComparison.Ordinal) ||
                modeChanged,
            modeChanged);
    }

    internal static void PrepareContextRefresh(
        CapabilitySelectorPublication publication,
        ShellPage page,
        string retainedIc,
        string retainedNumber,
        MergePresentationViewModel merge,
        ReplacePresentationViewModel replace)
    {
        ArgumentNullException.ThrowIfNull(publication);
        WorkflowInspectionOwner owner = page switch
        {
            ShellPage.Merge => WorkflowInspectionOwner.Merge,
            ShellPage.Replace => WorkflowInspectionOwner.Replace,
            ShellPage.Home or ShellPage.HexEditor => throw new ArgumentException(
                "Workflow page expected.",
                nameof(page)),
            _ => throw new InvalidOperationException("Unknown shell page."),
        };
        string icId = WorkflowSelectorProjection.ContextIc(publication, retainedIc, page);
        if (string.IsNullOrWhiteSpace(icId))
        {
            return;
        }

        string mode = owner == WorkflowInspectionOwner.Merge
            ? merge.ResolveAuthorableModeForCatalogReconciliation(
                workflowId => publication.IsWorkflowAuthorable(icId, workflowId))
            : replace.ResolveAuthorableModeForCatalogReconciliation(
                workflowId => publication.IsWorkflowAuthorable(icId, workflowId));
        if (string.IsNullOrWhiteSpace(mode))
        {
            return;
        }

        string number = WorkflowSelectorProjection.Number(
            publication,
            icId,
            retainedNumber,
            StringComparer.Ordinal.Equals(mode, ExperienceIds.AbMerge));
        if (owner == WorkflowInspectionOwner.Replace)
        {
            replace.ValidateContextRefresh(icId, number, mode);
            return;
        }

        merge.ValidateContextRefresh(icId, number, mode, publication);
    }

    internal static void RecordConfirmedSelection(
        Action<SystemActivityDraft> record,
        WorkflowContextSelection selection,
        string previousIc,
        string previousNumber)
    {
        if (!string.Equals(previousIc, selection.IcId, StringComparison.Ordinal))
        {
            record(new(
                SystemActivityCodes.IcSelected,
                SystemActivityImportance.Debug,
                SystemActivityCategory.Workflow,
                SystemActivitySeverity.Information,
                selection.IcId));
        }
        if (selection.ShowNumber &&
            !string.Equals(previousNumber, selection.Number, StringComparison.Ordinal))
        {
            record(new(
                SystemActivityCodes.NumberSelected,
                SystemActivityImportance.Debug,
                SystemActivityCategory.Workflow,
                SystemActivitySeverity.Information,
                selection.Number,
                selection.IcId));
        }
    }
}

internal sealed record WorkflowPageCatalogReconciliation(
    string IcId,
    string Number,
    bool NeedsRefresh,
    bool ModeChanged);

internal readonly record struct PageActivationRollback(
    string SelectedIc,
    string SelectedNumber,
    bool WorkflowLoaded);

internal sealed record AcceptedFirmwareMismatchSelection(
    WorkflowInspectionContext Context,
    string SlotId,
    string Path);

internal sealed record WorkflowModeNavigationStage(
    ShellPage Page,
    string PreviousMode,
    bool PreviousNeedsRefresh,
    bool Changed)
{
    internal static WorkflowModeNavigationStage Create(
        ShellPage page,
        string mode,
        string icId,
        bool previousNeedsRefresh,
        MergePresentationViewModel merge,
        ReplacePresentationViewModel replace,
        Func<string, string, bool> isAuthorable)
    {
        string previousMode = page == ShellPage.Merge
            ? merge.SelectedMergeMode
            : replace.SelectedReplaceMode;
        bool changed = page switch
        {
            ShellPage.Merge => merge.StageModeForWorkflowNavigation(
                mode,
                isAuthorable(icId, mode)),
            ShellPage.Replace => replace.StageModeForWorkflowNavigation(
                mode,
                isAuthorable(icId, mode)),
            ShellPage.Home or ShellPage.HexEditor => throw new ArgumentException(
                "Workflow mode staging requires Merge or Replace ownership.",
                nameof(page)),
            _ => throw new InvalidOperationException("Unknown shell page."),
        };
        return new(page, previousMode, previousNeedsRefresh, changed);
    }

    internal void Restore(
        MergePresentationViewModel merge,
        ReplacePresentationViewModel replace)
    {
        if (Changed && Page == ShellPage.Merge)
        {
            merge.RestoreStagedWorkflowNavigationMode(PreviousMode);
        }
        else if (Changed)
        {
            replace.RestoreStagedWorkflowNavigationMode(PreviousMode);
        }
    }

    internal void Publish(
        MergePresentationViewModel merge,
        ReplacePresentationViewModel replace)
    {
        if (Changed && Page == ShellPage.Merge)
        {
            merge.CommitStagedWorkflowNavigationMode(PreviousMode);
        }
        else if (Changed)
        {
            replace.CommitStagedWorkflowNavigationMode(PreviousMode);
        }
    }
}
