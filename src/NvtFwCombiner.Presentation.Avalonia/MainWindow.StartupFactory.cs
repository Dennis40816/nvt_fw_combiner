using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    internal static MainWindowViewModel CreateStartupViewModel(
        PresentationHostServices hostServices,
        ShellPreferenceSnapshot startupPreferences)
    {
        ArgumentNullException.ThrowIfNull(hostServices);
        ArgumentNullException.ThrowIfNull(startupPreferences);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(
            hostServices,
            ShellTextResources.LanguageFromPreference(startupPreferences.Language));
        viewModel.LoadShellPreferences(startupPreferences);
        return viewModel;
    }
}
