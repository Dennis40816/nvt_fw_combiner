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
    internal static PreparedGeneralMergeDefaults? PrepareContextRefresh(
        CapabilitySelectorPublication publication,
        ShellPage page,
        string retainedIc,
        string retainedNumber,
        MergePresentationViewModel merge,
        ReplacePresentationViewModel replace,
        string? currentGeneralDefaultsIc,
        string currentGeneralLength,
        string currentGeneralFillByte,
        Func<string, (string Length, string FillByte)> resolveGeneralDefaults)
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
            return null;
        }

        string mode = owner == WorkflowInspectionOwner.Merge
            ? merge.ResolveAuthorableModeForCatalogReconciliation(
                workflowId => publication.IsWorkflowAuthorable(icId, workflowId))
            : replace.ResolveAuthorableModeForCatalogReconciliation(
                workflowId => publication.IsWorkflowAuthorable(icId, workflowId));
        if (string.IsNullOrWhiteSpace(mode))
        {
            return null;
        }

        string number = WorkflowSelectorProjection.Number(
            publication,
            icId,
            retainedNumber,
            StringComparer.Ordinal.Equals(mode, ExperienceIds.AbMerge));
        if (owner == WorkflowInspectionOwner.Replace)
        {
            replace.ValidateContextRefresh(icId, number, mode);
            return null;
        }

        string? length = null;
        string? fillByte = null;
        PreparedGeneralMergeDefaults? prepared = null;
        if (StringComparer.Ordinal.Equals(mode, ExperienceIds.GeneralMerge))
        {
            if (string.Equals(currentGeneralDefaultsIc, icId, StringComparison.Ordinal))
            {
                length = currentGeneralLength;
                fillByte = currentGeneralFillByte;
            }
            else
            {
                (length, fillByte) = resolveGeneralDefaults(icId);
                prepared = new(icId, length, fillByte);
            }
        }
        merge.ValidateContextRefresh(icId, number, mode, publication, length, fillByte);
        return prepared;
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

internal sealed record PreparedGeneralMergeDefaults(
    string IcId,
    string Length,
    string FillByte);

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
