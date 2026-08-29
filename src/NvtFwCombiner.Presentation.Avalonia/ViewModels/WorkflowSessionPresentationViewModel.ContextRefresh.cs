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
        RefreshContextStateCore(WorkflowInspectionOwner.Replace, resetRunResult: true,
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
                _stateBindings.ResetRunResult();
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
            _stateBindings.ResetRunResult();
        }
    }

}
