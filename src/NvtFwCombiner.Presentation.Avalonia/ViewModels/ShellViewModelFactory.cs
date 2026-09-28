namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal static class ShellViewModelFactory
{
    /// <param name="hostServices">Explicit Application and platform dependencies.</param>
    /// <param name="language">Requested shell text language.</param>
    /// <returns>A populated main window view model.</returns>
    public static MainWindowViewModel Create(
        PresentationHostServices hostServices,
        ShellLanguage language = ShellLanguage.English)
    {
        ArgumentNullException.ThrowIfNull(hostServices);
        var viewModel = new MainWindowViewModel(
            ApplicationVersionProvider.ShellLabel,
            ApplicationVersionProvider.InformationalVersion,
            language,
            hostServices);
        viewModel.Reports.LocalFiles = hostServices.LocalFiles;
        return viewModel;
    }
}
