namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Creates the production-backed UI view model.</summary>
public static class ShellViewModelFactory
{
    /// <summary>Creates the main window view model.</summary>
    /// <param name="language">Requested shell text language.</param>
    /// <returns>A populated main window view model.</returns>
    public static MainWindowViewModel Create(ShellLanguage language = ShellLanguage.English)
    {
        PresentationHostServices hostServices = DesktopCompositionRoot.Create(
            ApplicationVersionProvider.InformationalVersion);
        return Create(hostServices, language);
    }

    internal static MainWindowViewModel Create(
        PresentationHostServices hostServices,
        ShellLanguage language)
    {
        ArgumentNullException.ThrowIfNull(hostServices);
        return new MainWindowViewModel(
            ApplicationVersionProvider.WorkbenchLabel,
            ApplicationVersionProvider.InformationalVersion,
            language,
            hostServices);
    }
}
