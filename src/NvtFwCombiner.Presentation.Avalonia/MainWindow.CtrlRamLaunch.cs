using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private bool _isStartupInputLoading;
    private bool _startupInputsAttempted;

    private async Task LoadStartupInputsAsync(MainWindowViewModel shell, CancellationToken cancellationToken)
    {
        if (_launchOptions.CtrlRam is not { } request || _startupInputsAttempted) { return; }
        _startupInputsAttempted = true;
        try
        {
            await ApplyCtrlRamLaunchAsync(shell, request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Closing the window owns cancellation; never resume remaining selections.
        }
        catch (Exception exception)
        {
            shell.Reports.LoadReportError("Startup inputs", exception.Message);
        }
        finally
        {
            _isStartupInputLoading = false;
            ApplyShellInteractionState(shell);
        }
    }

    internal static async Task ApplyCtrlRamLaunchAsync(
        MainWindowViewModel shell, CtrlRamLaunchRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!shell.WorkflowSession.IsCanonicalCatalogReady || shell.HasSelectedFiles ||
            shell.SelectedPage != ShellPage.Home || !shell.BeginCtrlRamReplaceFromHomeCommand.CanExecute(null))
        {
            throw new InvalidOperationException("CtrlRAM startup requires a ready, empty Home session.");
        }
        WorkflowSessionPresentationViewModel workflow = shell.WorkflowSession;
        shell.BeginCtrlRamReplaceFromHomeCommand.Execute(null);
        try
        {
            WorkflowContextSetupViewModel setup = workflow.WorkflowContextSetup;
            if (!setup.IcChoices.Contains(request.IcId, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"CtrlRAM IC '{request.IcId}' is unavailable. Choices: {string.Join(", ", setup.IcChoices)}.");
            }
            setup.SelectedIc = request.IcId;
            if (!setup.NumberChoices.Any(choice => StringComparer.Ordinal.Equals(choice.Token, request.Number)))
            {
                throw new InvalidOperationException($"IC Number '{request.Number}' is unavailable for {request.IcId}. Choices: {string.Join(", ", setup.NumberChoices.Select(choice => choice.Token))}.");
            }
            setup.SelectedNumber = request.Number;
            workflow.ConfirmWorkflowContextCommand.Execute(null);
        }
        finally
        {
            if (workflow.IsWorkflowContextModalOpen) { workflow.CancelWorkflowContextCommand.Execute(null); }
        }

        await workflow.SetSlotFileAsync(shell.Replace.ReplaceBaseSlot.SlotId, request.BasePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (NeedsInputAttention(shell.Replace.ReplaceBaseSlot)) { return; }
        // Slot availability comes from the inspected Base, not a second IC-to-region table.
        FirmwareSlotViewModel[] ctrlRamSlots = [.. shell.Replace.ReplaceSlots.Where(
            slot => slot.SlotKind == FirmwareSlotKind.CtrlRam && slot.CanSelectFile)];
        foreach (CtrlRamLaunchInput input in request.Inputs)
        {
            if (!ctrlRamSlots.Any(slot => slot.SlotId == input.SlotId))
            {
                throw new InvalidOperationException($"CtrlRAM slot '{input.SlotId}' is unavailable. Choices: {string.Join(", ", ctrlRamSlots.Select(slot => slot.SlotId))}.");
            }
        }
        foreach (CtrlRamLaunchInput input in request.Inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await workflow.SetSlotFileAsync(input.SlotId, input.Path, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            FirmwareSlotViewModel slot = shell.Replace.ReplaceSlots.Single(slot => slot.SlotId == input.SlotId);
            if (NeedsInputAttention(slot)) { return; }
        }

        bool NeedsInputAttention(FirmwareSlotViewModel slot)
        {
            return slot.IsSemanticStateError || workflow.IsFirmwareIcMismatchModalOpen ||
                workflow.IsFirmwareNumberMismatchModalOpen;
        }
    }
}
