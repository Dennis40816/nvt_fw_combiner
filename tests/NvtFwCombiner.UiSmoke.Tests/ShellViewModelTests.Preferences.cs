using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies local shell preferences round-trip and invalid values keep fail-closed defaults.</summary>
    [Fact]
    public void ShellPreferenceFileStoreRoundTripsAndInvalidValuesFallBack()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-shell-preferences");
        string preferencesPath = workspace.PathFor(Path.Combine("state", "preferences.v1.json"));
        var preferences = new ShellPreferenceSnapshot("Dark", "Traditional Chinese", true);

        ShellPreferenceFileStore.Save(preferencesPath, preferences);

        ShellPreferenceSnapshot loaded = ShellPreferenceFileStore.Load(preferencesPath);
        Assert.Equal(preferences, loaded);

        var updatedPreferences = new ShellPreferenceSnapshot("Light", "English");
        ShellPreferenceFileStore.Save(preferencesPath, updatedPreferences);
        Assert.Equal(updatedPreferences, ShellPreferenceFileStore.Load(preferencesPath));

        MainWindowViewModel restoredViewModel = ShellViewModelFactory.Create();
        restoredViewModel.LoadShellPreferences(loaded);

        Assert.Equal("Dark", restoredViewModel.SelectedTheme);
        Assert.Equal("Traditional Chinese", restoredViewModel.SelectedLanguage);
        Assert.True(restoredViewModel.IsReducedMotionEnabled);
        Assert.True(restoredViewModel.CompositionProgress.IsReducedMotionEnabled);
        Assert.False(restoredViewModel.CompositionProgress.ShouldAnimateActiveStep);
        Assert.Equal("設定", restoredViewModel.SettingsPreview.Title);
        Assert.Equal(preferences, restoredViewModel.ExportShellPreferences());

        File.WriteAllText(preferencesPath, "{not valid json");

        Assert.Equal(ShellPreferenceSnapshot.Default, ShellPreferenceFileStore.Load(preferencesPath));

        MainWindowViewModel defaultViewModel = ShellViewModelFactory.Create();
        defaultViewModel.LoadShellPreferences(new ShellPreferenceSnapshot("Blue", "Klingon"));

        Assert.Equal("System", defaultViewModel.SelectedTheme);
        Assert.Equal("English", defaultViewModel.SelectedLanguage);
        Assert.False(defaultViewModel.IsReducedMotionEnabled);
    }
}
