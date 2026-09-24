using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class WorkflowSessionPresentationViewModel
{
    internal void RefreshContextState(WorkflowInspectionOwner? owner = null, bool resetRunResult = false,
        bool preserveReplaceSlotFiles = false, CapabilitySelectorPublication? selectorPublication = null)
    {
        RefreshContextStateCore(owner, resetRunResult, preserveReplaceSlotFiles,
            acceptedReplaceMode: false, selectorPublication);
    }

    private void RefreshAcceptedReplaceModeContextState()
    {
        RefreshContextStateCore(WorkflowInspectionOwner.Replace, resetRunResult: false,
            preserveReplaceSlotFiles: false, acceptedReplaceMode: true, selectorPublication: null);
    }

    private void RefreshContextStateCore(WorkflowInspectionOwner? owner, bool resetRunResult,
        bool preserveReplaceSlotFiles, bool acceptedReplaceMode,
        CapabilitySelectorPublication? selectorPublication)
    {
        EnsureWorkflowLoaded(selectorPublication);
        if (!HasWorkflowAuthoringChoices || string.IsNullOrWhiteSpace(SelectedIc))
        {
            if (owner is null or WorkflowInspectionOwner.Merge)
            {
                _merge.RefreshContextState();
            }
            if (!acceptedReplaceMode && (owner is null or WorkflowInspectionOwner.Replace))
            {
                _replace.ClearUnavailableContextState();
            }
            _stateBindings.RefreshCommandState();
            PublishRefreshedSharedContext();
            if (resetRunResult)
            {
                ResetRunResults(owner);
            }
            return;
        }

        if (owner == WorkflowInspectionOwner.Merge ||
            (owner is null && ActiveWorkflowOwner == WorkflowInspectionOwner.Merge))
        {
            PrepareGeneralMergeDefaults(GetWorkflowPageIc(WorkflowInspectionOwner.Merge));
        }

        if (owner is null or WorkflowInspectionOwner.Merge)
        {
            _merge.RefreshContextState();
            _merge.ApplyFirmwareSlotText();
        }
        if (owner is null or WorkflowInspectionOwner.Replace)
        {
            if (acceptedReplaceMode)
            {
                _replace.PrepareAcceptedModeContextState(
                    preserveSlotFiles: preserveReplaceSlotFiles);
            }
            else
            {
                _replace.RefreshContextState(preserveSlotFiles: preserveReplaceSlotFiles);
            }
            _replace.ApplyFirmwareSlotText();
        }
        _refreshCommandAvailability();
        PublishRefreshedSharedContext();
        if (resetRunResult)
        {
            ResetRunResults(owner);
        }
    }

    private void ResetRunResults(WorkflowInspectionOwner? owner, string? mode = null, bool allModes = false)
    {
        if (owner is null or WorkflowInspectionOwner.Merge)
        {
            IEnumerable<string> modes = allModes
                ? WorkflowPageModeCatalog.ForPage(ShellPage.Merge)
                : [mode ?? _merge.SelectedMergeMode];
            foreach (string affectedMode in modes)
            {
                _stateBindings.ResetRunResult(_merge.CaptureRunContext(affectedMode));
            }
        }
        if (owner is null or WorkflowInspectionOwner.Replace)
        {
            IEnumerable<string> modes = allModes
                ? WorkflowPageModeCatalog.ForPage(ShellPage.Replace)
                : [mode ?? _replace.SelectedReplaceMode];
            foreach (string affectedMode in modes)
            {
                _stateBindings.ResetRunResult(_replace.CaptureRunContext(affectedMode));
            }
        }
    }

}
