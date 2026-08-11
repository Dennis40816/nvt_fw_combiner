namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Creates the production-backed UI view model.</summary>
public static class ShellViewModelFactory
{
    /// <summary>Creates the main window view model.</summary>
    /// <param name="hostServices">Explicit Application and platform dependencies.</param>
    /// <param name="language">Requested shell text language.</param>
    /// <returns>A populated main window view model.</returns>
    public static MainWindowViewModel Create(
        PresentationHostServices hostServices,
        ShellLanguage language = ShellLanguage.English)
    {
        ArgumentNullException.ThrowIfNull(hostServices);
        return new MainWindowViewModel(
            ApplicationVersionProvider.ShellLabel,
            ApplicationVersionProvider.InformationalVersion,
            language,
            hostServices);
    }
}
