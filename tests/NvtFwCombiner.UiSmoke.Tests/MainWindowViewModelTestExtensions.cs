using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Compatibility helper for legacy synchronous assertions; selection still starts on the caller thread.</summary>
internal static class MainWindowViewModelTestExtensions
{
    internal static void OpenReplace(MainWindowViewModel viewModel, string mode)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.Replace.SelectedReplaceMode = mode;
    }

    internal static void SetSlotFile(this MainWindowViewModel viewModel, string slotId, string path)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        bool replaceSlot = string.Equals(
                viewModel.Replace.ReplaceBaseSlot.SlotId,
                slotId,
                StringComparison.Ordinal) ||
            viewModel.Replace.ReplaceSlots.Any(slot =>
                string.Equals(slot.SlotId, slotId, StringComparison.Ordinal)) ||
            viewModel.Replace.GeneralReplaceMappings.Any(mapping =>
                string.Equals(mapping.MappingId, slotId, StringComparison.Ordinal));
        if (replaceSlot)
        {
            OpenReplace(viewModel, viewModel.Replace.SelectedReplaceMode);
        }
        else
        {
            viewModel.ShowMergeCommand.Execute(null);
        }
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        viewModel.WorkflowSession.SetSlotFileAsync(slotId, path, cancellationToken).GetAwaiter().GetResult();
    }
}
