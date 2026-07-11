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
        var preferences = new ShellPreferenceSnapshot("Dark", "Warn only", "Traditional Chinese");

        ShellPreferenceFileStore.Save(preferencesPath, preferences);

        ShellPreferenceSnapshot loaded = ShellPreferenceFileStore.Load(preferencesPath);
        Assert.Equal(preferences, loaded);

        MainWindowViewModel restoredViewModel = ShellViewModelFactory.Create();
        restoredViewModel.LoadShellPreferences(loaded);

        Assert.Equal("Dark", restoredViewModel.SelectedTheme);
        Assert.Equal("Warn only", restoredViewModel.SelectedStrictness);
        Assert.Equal("Traditional Chinese", restoredViewModel.SelectedLanguage);
        Assert.Equal("設定", restoredViewModel.SettingsPreview.Title);
        Assert.Equal("繁體中文介面已套用並會在啟動時還原。", restoredViewModel.LanguagePreferenceStatus);
        Assert.Equal(preferences, restoredViewModel.ExportShellPreferences());

        File.WriteAllText(preferencesPath, "{not valid json");

        Assert.Equal(ShellPreferenceSnapshot.Default, ShellPreferenceFileStore.Load(preferencesPath));

        MainWindowViewModel defaultViewModel = ShellViewModelFactory.Create();
        defaultViewModel.LoadShellPreferences(new ShellPreferenceSnapshot("Blue", "Relaxed", "Klingon"));

        Assert.Equal("System", defaultViewModel.SelectedTheme);
        Assert.Equal("Strict", defaultViewModel.SelectedStrictness);
        Assert.Equal("English", defaultViewModel.SelectedLanguage);
    }
}
