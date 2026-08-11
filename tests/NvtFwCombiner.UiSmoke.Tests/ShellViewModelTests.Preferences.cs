using System.Text.Json;
using System.Text;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>The factory language initializes the matching persisted selector without a second relocalization pass.</summary>
    [Fact]
    public void ShellFactoryInitialLanguageAndSelectorStayAligned()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel(ShellLanguage.ChineseTraditional);

        Assert.Equal("Traditional Chinese", viewModel.SelectedLanguage);
        Assert.Equal("設定", viewModel.SettingsPreview.Title);

        viewModel.SelectedLanguage = "English";

        Assert.Equal("Settings", viewModel.SettingsPreview.Title);
    }

    /// <summary>Verifies local shell preferences round-trip and invalid values keep fail-closed defaults.</summary>
    [Fact]
    public void ShellPreferenceFileStoreRoundTripsAndInvalidValuesFallBack()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-shell-preferences");
        string preferencesPath = workspace.PathFor(Path.Combine("state", "preferences.v1.json"));
        var preferences = new ShellPreferenceSnapshot("Dark", "Traditional Chinese", true);

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
            Assert.True(entry.GetProperty("IsReducedMotionEnabled").GetBoolean());
            Assert.Equal(4, entry.EnumerateObject().Count());
        }

        ShellPreferenceSnapshot loaded = ShellPreferenceFileStore.Load(preferencesPath);
        Assert.Equal(preferences, loaded);

        var updatedPreferences = new ShellPreferenceSnapshot("Light", "English");
        ShellPreferenceFileStore.Save(preferencesPath, updatedPreferences);
        Assert.Equal(updatedPreferences, ShellPreferenceFileStore.Load(preferencesPath));

        MainWindowViewModel restoredViewModel = PresentationTestHost.CreateViewModel();
        restoredViewModel.LoadShellPreferences(loaded);

        Assert.Equal("Dark", restoredViewModel.SelectedTheme);
        Assert.Equal("Traditional Chinese", restoredViewModel.SelectedLanguage);
        Assert.True(restoredViewModel.IsReducedMotionEnabled);
        Assert.True(restoredViewModel.RunSession.CompositionProgress.IsReducedMotionEnabled);
        Assert.False(restoredViewModel.RunSession.CompositionProgress.ShouldAnimateActiveStep);
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

        MainWindowViewModel defaultViewModel = PresentationTestHost.CreateViewModel();
        defaultViewModel.LoadShellPreferences(new ShellPreferenceSnapshot("Blue", "Klingon"));

        Assert.Equal("System", defaultViewModel.SelectedTheme);
        Assert.Equal("English", defaultViewModel.SelectedLanguage);
        Assert.False(defaultViewModel.IsReducedMotionEnabled);
    }

    /// <summary>Bounds the synchronous startup preference read before constructing the localized shell.</summary>
    [Fact]
    public void ShellPreferenceFileStoreRejectsValidJsonAboveStartupLimit()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-shell-preferences-size-limit");
        string preferencesPath = workspace.PathFor(Path.Combine("state", "preferences.v1.json"));
        var preferences = new ShellPreferenceSnapshot("Dark", "Traditional Chinese", true);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        ShellPreferenceFileStore.Save(preferencesPath, preferences);
        string json = File.ReadAllText(preferencesPath);
        int paddingLength = checked((int)(
            ShellPreferenceFileStore.MaximumPreferencesFileBytes - utf8.GetByteCount(json)));
        Assert.True(paddingLength > 0);
        File.WriteAllText(preferencesPath, json + new string(' ', paddingLength), utf8);

        Assert.Equal(ShellPreferenceFileStore.MaximumPreferencesFileBytes, new FileInfo(preferencesPath).Length);
        Assert.Equal(preferences, ShellPreferenceFileStore.Load(preferencesPath));

        File.AppendAllText(preferencesPath, " ", utf8);

        Assert.Equal(ShellPreferenceFileStore.MaximumPreferencesFileBytes + 1, new FileInfo(preferencesPath).Length);
        Assert.Equal(ShellPreferenceSnapshot.Default, ShellPreferenceFileStore.Load(preferencesPath));
    }

    /// <summary>Local preferences written with supported BOM encodings remain readable.</summary>
    [Fact]
    public void ShellPreferenceFileStoreLoadsBomDocuments()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-shell-preferences-utf16");
        const string json = /*lang=json,strict*/ """
            {
              "SchemaVersion": 1,
              "Preferences": {
                "Theme": "Dark",
                "Strictness": "Strict",
                "Language": "Traditional Chinese",
                "IsReducedMotionEnabled": true
              }
            }
            """;
        Encoding[] encodings =
        [
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            Encoding.Unicode,
            Encoding.BigEndianUnicode,
            new UTF32Encoding(bigEndian: false, byteOrderMark: true),
            new UTF32Encoding(bigEndian: true, byteOrderMark: true),
        ];
        var expected = new ShellPreferenceSnapshot("Dark", "Traditional Chinese", true);

        for (int index = 0; index < encodings.Length; index++)
        {
            string preferencesPath = workspace.PathFor(Path.Combine("state", $"preferences-{index}.v1.json"));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(preferencesPath)!);
            File.WriteAllText(preferencesPath, json, encodings[index]);

            Assert.Equal(expected, ShellPreferenceFileStore.Load(preferencesPath));
        }
    }

    /// <summary>A valid preference document with an unsupported schema keeps fail-closed defaults.</summary>
    [Fact]
    public void ShellPreferenceFileStoreRejectsUnsupportedSchema()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-shell-preferences-schema");
        string preferencesPath = workspace.PathFor(Path.Combine("state", "preferences.v1.json"));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(preferencesPath)!);
        const string json = /*lang=json,strict*/ """
            {
              "SchemaVersion": 2,
              "Preferences": {
                "Theme": "Dark",
                "Strictness": "Strict",
                "Language": "Traditional Chinese",
                "IsReducedMotionEnabled": true
              }
            }
            """;
        File.WriteAllText(preferencesPath, json, Encoding.Unicode);

        Assert.Equal(ShellPreferenceSnapshot.Default, ShellPreferenceFileStore.Load(preferencesPath));
    }

    /// <summary>Async preference persistence keeps the previous file when cancelled and atomically publishes the latest snapshot.</summary>
    [Fact]
    public async Task ShellPreferenceFileStoreAsyncSavePreservesLastCompleteSnapshot()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-shell-preferences-async");
        string preferencesPath = workspace.PathFor(Path.Combine("state", "preferences.v1.json"));
        var original = new ShellPreferenceSnapshot("Dark", "English");
        var cancelled = new ShellPreferenceSnapshot("Light", "Traditional Chinese", true);
        var latest = new ShellPreferenceSnapshot("System", "Traditional Chinese", true);
        ShellPreferenceFileStore.Save(preferencesPath, original);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await ShellPreferenceFileStore.SaveAsync(preferencesPath, cancelled, cancellation.Token);

        Assert.Equal(original, ShellPreferenceFileStore.Load(preferencesPath));

        await ShellPreferenceFileStore.SaveAsync(
            preferencesPath,
            latest,
            TestContext.Current.CancellationToken);

        Assert.Equal(latest, ShellPreferenceFileStore.Load(preferencesPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(preferencesPath)!, "*.tmp"));
    }
}
