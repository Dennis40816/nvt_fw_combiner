using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    internal static async Task ApplyStandardMergeLaunchAsync(
        MainWindowViewModel shell, StandardMergeLaunchRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ApplyStartupContext(shell, shell.BeginNormalMergeFromHomeCommand, request.IcId, request.Number);
        WorkflowSessionPresentationViewModel workflow = shell.WorkflowSession;
        foreach ((FirmwareSlotViewModel slot, string path) in new[]
        {
            (shell.Merge.MergeDpSlot, request.DpPath),
            (shell.Merge.MergeTpSlot, request.TpPath),
        })
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!slot.CanSelectFile)
            {
                throw new InvalidOperationException($"Standard Merge input '{slot.SlotId}' is unavailable.");
            }
            await workflow.SetSlotFileAsync(slot.SlotId, path, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (slot.IsInputInspectionBlocking || workflow.IsFirmwareIcMismatchModalOpen ||
                workflow.IsFirmwareNumberMismatchModalOpen)
            {
                return;
            }
        }
    }
}
