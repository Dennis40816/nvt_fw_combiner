using System.Text.Json;
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
        var preferences = new ShellPreferenceSnapshot("Dark", "Traditional Chinese");

        Assert.Equal(ShellPreferenceSnapshot.Default, ShellPreferenceFileStore.Load(preferencesPath));

        ShellPreferenceFileStore.Save(preferencesPath, preferences);

        using (var document = JsonDocument.Parse(File.ReadAllText(preferencesPath)))
        {
            JsonElement root = document.RootElement;
            Assert.Equal(1, root.GetProperty("SchemaVersion").GetInt32());
            Assert.Equal(2, root.EnumerateObject().Count());
            JsonElement entry = root.GetProperty("Preferences");
            Assert.Equal("Dark", entry.GetProperty("Theme").GetString());
            Assert.Equal("Strict", entry.GetProperty("Strictness").GetString());
            Assert.Equal("Traditional Chinese", entry.GetProperty("Language").GetString());
            Assert.Equal(3, entry.EnumerateObject().Count());
        }

        ShellPreferenceSnapshot loaded = ShellPreferenceFileStore.Load(preferencesPath);
        Assert.Equal(preferences, loaded);

        var updatedPreferences = new ShellPreferenceSnapshot("Light", "English");
        ShellPreferenceFileStore.Save(preferencesPath, updatedPreferences);
        Assert.Equal(updatedPreferences, ShellPreferenceFileStore.Load(preferencesPath));

        MainWindowViewModel restoredViewModel = ShellViewModelFactory.Create();
        restoredViewModel.LoadShellPreferences(loaded);

        Assert.Equal("Dark", restoredViewModel.SelectedTheme);
        Assert.Equal("Traditional Chinese", restoredViewModel.SelectedLanguage);
        Assert.Equal("設定", restoredViewModel.SettingsPreview.Title);
        Assert.Equal(preferences, restoredViewModel.ExportShellPreferences());

        File.WriteAllText(preferencesPath, "{not valid json");

        Assert.Equal(ShellPreferenceSnapshot.Default, ShellPreferenceFileStore.Load(preferencesPath));

        File.WriteAllText(preferencesPath, /*lang=json,strict*/ """{"SchemaVersion":1,"Preferences":null}""");

        Assert.Equal(ShellPreferenceSnapshot.Default, ShellPreferenceFileStore.Load(preferencesPath));

        File.WriteAllText(preferencesPath, /*lang=json,strict*/ """{"SchemaVersion":1}""");

        Assert.Equal(ShellPreferenceSnapshot.Default, ShellPreferenceFileStore.Load(preferencesPath));

        File.WriteAllText(
            preferencesPath,
            /*lang=json,strict*/ """{"SchemaVersion":2,"Preferences":{"Theme":"Dark","Strictness":"Strict","Language":"Traditional Chinese"}}""");

        Assert.Equal(ShellPreferenceSnapshot.Default, ShellPreferenceFileStore.Load(preferencesPath));

        MainWindowViewModel defaultViewModel = ShellViewModelFactory.Create();
        defaultViewModel.LoadShellPreferences(new ShellPreferenceSnapshot("Blue", "Klingon"));

        Assert.Equal("System", defaultViewModel.SelectedTheme);
        Assert.Equal("English", defaultViewModel.SelectedLanguage);
    }
}
