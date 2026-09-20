using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    internal static async Task ApplyAbMergeLaunchAsync(
        MainWindowViewModel shell, AbMergeLaunchRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ApplyStartupContext(shell, shell.BeginAbMergeFromHomeCommand, request.IcId, request.Number);
        WorkflowSessionPresentationViewModel workflow = shell.WorkflowSession;
        (string AddressSpace, string Path)[] inputs =
        [
            (CompositionAddressSpaceIds.DpAbInput, request.DpPath),
            (CompositionAddressSpaceIds.TpAInput, request.TpAPath),
            (CompositionAddressSpaceIds.TpBInput, request.TpBPath),
        ];
        foreach ((string addressSpace, string path) in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!shell.Merge.AbMergeSlotsByAddressSpace.TryGetValue(addressSpace, out FirmwareSlotViewModel? slot) ||
                !slot.CanSelectFile)
            {
                throw new InvalidOperationException($"AB input '{addressSpace}' is unavailable.");
            }
            await workflow.SetSlotFileAsync(slot.SlotId, path, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            // Incomplete-pair precompilation readiness is not a terminal failure of this input.
            if (shell.Merge.AbMergeSlots.Any(input => input.CurrentInspectionProjection is { } inspection
                    ? inspection.InputSlotStatus is { ConfigurationBlocker: not null } or { IsTerminal: true, BlocksBuild: true }
                    : input.IsInputInspectionBlocking) ||
                workflow.IsFirmwareIcMismatchModalOpen || workflow.IsFirmwareNumberMismatchModalOpen)
            {
                return;
            }
        }
    }
}
