using Avalonia.Controls;
using Avalonia.VisualTree;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

internal static class WorkbenchShellViewModelLocator
{
    public static MainWindowViewModel? Find(Control control)
    {
        return control.FindAncestorOfType<Window>()?.DataContext as MainWindowViewModel;
    }
}
