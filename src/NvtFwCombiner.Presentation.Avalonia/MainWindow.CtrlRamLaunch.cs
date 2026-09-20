using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private bool _isStartupInputLoading;
    private bool _startupInputsAttempted;

    private async Task LoadStartupInputsAsync(MainWindowViewModel shell, CancellationToken cancellationToken)
    {
        if (!_launchOptions.HasStartupInputs || _startupInputsAttempted) { return; }
        _startupInputsAttempted = true;
        try
        {
            if (_launchOptions.CtrlRam is { } ctrlRam)
            {
                await ApplyCtrlRamLaunchAsync(shell, ctrlRam, cancellationToken);
            }
            else if (_launchOptions.AbMerge is { } abMerge)
            {
                await ApplyAbMergeLaunchAsync(shell, abMerge, cancellationToken);
            }
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
        ApplyStartupContext(shell, shell.BeginCtrlRamReplaceFromHomeCommand, request.IcId, request.Number);
        WorkflowSessionPresentationViewModel workflow = shell.WorkflowSession;

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

    private static void ApplyStartupContext(MainWindowViewModel shell, IRelayCommand begin, string icId, string number)
    {
        WorkflowSessionPresentationViewModel workflow = shell.WorkflowSession;
        if (!workflow.IsCanonicalCatalogReady || shell.HasSelectedFiles ||
            shell.SelectedPage != ShellPage.Home || !begin.CanExecute(null))
        {
            throw new InvalidOperationException("Input startup requires a ready, empty Home session.");
        }
        begin.Execute(null);
        try
        {
            WorkflowContextSetupViewModel setup = workflow.WorkflowContextSetup;
            if (!setup.IcChoices.Contains(icId, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"IC '{icId}' is unavailable. Choices: {string.Join(", ", setup.IcChoices)}.");
            }
            setup.SelectedIc = icId;
            if (!setup.NumberChoices.Any(choice => StringComparer.Ordinal.Equals(choice.Token, number)))
            {
                throw new InvalidOperationException($"IC Number '{number}' is unavailable for {icId}. Choices: {string.Join(", ", setup.NumberChoices.Select(choice => choice.Token))}.");
            }
            setup.SelectedNumber = number;
            if (!workflow.ConfirmWorkflowContextCommand.CanExecute(null))
            {
                throw new InvalidOperationException("Input startup context is not available.");
            }
            workflow.ConfirmWorkflowContextCommand.Execute(null);
        }
        finally
        {
            if (workflow.IsWorkflowContextModalOpen) { workflow.CancelWorkflowContextCommand.Execute(null); }
        }
    }
}
