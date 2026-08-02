using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Compatibility helper for legacy synchronous assertions; selection still starts on the caller thread.</summary>
internal static class MainWindowViewModelTestExtensions
{
    internal static void SetSlotFile(this MainWindowViewModel viewModel, string slotId, string path)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        viewModel.WorkflowSession.SetSlotFileAsync(slotId, path, cancellationToken).GetAwaiter().GetResult();
    }
}
