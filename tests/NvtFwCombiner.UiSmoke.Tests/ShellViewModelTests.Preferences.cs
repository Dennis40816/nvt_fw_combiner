using System.Text.Json;
using System.Text;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
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

    /// <summary>Startup overlaps one preference read with host construction and applies that exact first-frame snapshot.</summary>
    [Fact]
    public async Task DesktopStartupUsesOneOverlappedPreferenceSnapshot()
    {
        PresentationHostServices services = PresentationTestHost.CreateServices("startup-test");
        var preferenceLoaderStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowPreferenceLoader = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var preferenceCompletion = new TaskCompletionSource<ShellPreferenceSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int preferenceLoads = 0;
        int hostConstructions = 0;

        Task<(PresentationHostServices HostServices, Task<ShellPreferenceSnapshot> ShellPreferences)> preparation =
            Task.Run(() =>
                DesktopApplication.PrepareStartup(
                    () =>
                    {
                        hostConstructions++;
                        return services;
                    },
                    () =>
                    {
                        preferenceLoads++;
                        _ = preferenceLoaderStarted.TrySetResult();
                        allowPreferenceLoader.Task.GetAwaiter().GetResult();
                        return preferenceCompletion.Task;
                    },
                    StartupTraceSession.Disabled));

        try
        {
            await preferenceLoaderStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            (PresentationHostServices preparedHost, Task<ShellPreferenceSnapshot> preparedPreferences) =
                await preparation.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);

            Assert.Same(services, preparedHost);
            Assert.False(preparedPreferences.IsCompleted);
            Assert.Equal(1, preferenceLoads);
            Assert.Equal(1, hostConstructions);

            _ = allowPreferenceLoader.TrySetResult();
            var preferences = new ShellPreferenceSnapshot(
                "Dark",
                "Traditional Chinese",
                IsReducedMotionEnabled: true);
            _ = preferenceCompletion.TrySetResult(preferences);
            Assert.Same(preferences, await preparedPreferences);

            MainWindowViewModel viewModel = MainWindow.CreateStartupViewModel(
                preparedHost,
                await preparedPreferences);
            Assert.Equal("Dark", viewModel.SelectedTheme);
            Assert.Equal("Traditional Chinese", viewModel.SelectedLanguage);
            Assert.True(viewModel.IsReducedMotionEnabled);
            Assert.Equal("設定", viewModel.SettingsPreview.Title);
        }
        finally
        {
            _ = allowPreferenceLoader.TrySetResult();
        }
    }

    /// <summary>Verifies local shell preferences round-trip and invalid values keep fail-closed defaults.</summary>
    [Fact]
    public async Task ShellPreferenceFileStoreRoundTripsAndInvalidValuesFallBack()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-shell-preferences");
        string preferencesPath = workspace.PathFor(Path.Combine("state", "preferences.v1.json"));
        var preferences = new ShellPreferenceSnapshot("Dark", "Traditional Chinese", true);

        ShellPreferenceSnapshot missingPreferences = LoadPreferences(preferencesPath);
        Assert.Equal(ShellPreferenceSnapshot.Default, missingPreferences);
        Assert.Equal("Light", missingPreferences.Theme);

        SavePreferences(preferencesPath, preferences);

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

        ShellPreferenceSnapshot loaded = LoadPreferences(preferencesPath);
        Assert.Equal(preferences, loaded);
        Assert.Equal(preferences, await ShellPreferenceFileStore.LoadAsync(TestHost.LocalFiles, preferencesPath));

        var updatedPreferences = new ShellPreferenceSnapshot("Light", "English");
        SavePreferences(preferencesPath, updatedPreferences);
        Assert.Equal(updatedPreferences, LoadPreferences(preferencesPath));

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

        Assert.Equal(ShellPreferenceSnapshot.Default, LoadPreferences(preferencesPath));

        File.WriteAllText(preferencesPath, /*lang=json,strict*/ """{"SchemaVersion":1,"Preferences":null}""");

        Assert.Equal(ShellPreferenceSnapshot.Default, LoadPreferences(preferencesPath));

        File.WriteAllText(preferencesPath, /*lang=json,strict*/ """{"SchemaVersion":1}""");

        Assert.Equal(ShellPreferenceSnapshot.Default, LoadPreferences(preferencesPath));

        File.WriteAllText(
            preferencesPath,
            /*lang=json,strict*/ """{"SchemaVersion":2,"Preferences":{"Theme":"Dark","Strictness":"Strict","Language":"Traditional Chinese"}}""");

        Assert.Equal(ShellPreferenceSnapshot.Default, LoadPreferences(preferencesPath));

        MainWindowViewModel defaultViewModel = PresentationTestHost.CreateViewModel();

        Assert.Equal("Light", defaultViewModel.SelectedTheme);

        defaultViewModel.LoadShellPreferences(new ShellPreferenceSnapshot("Blue", "Klingon"));

        Assert.Equal("Light", defaultViewModel.SelectedTheme);
        Assert.Equal("English", defaultViewModel.SelectedLanguage);
        Assert.False(defaultViewModel.IsReducedMotionEnabled);

        defaultViewModel.LoadShellPreferences(new ShellPreferenceSnapshot("System", "English"));

        Assert.Equal("System", defaultViewModel.SelectedTheme);
    }

    /// <summary>Bounds the startup preference read before constructing the localized shell.</summary>
    [Fact]
    public void ShellPreferenceFileStoreRejectsValidJsonAboveStartupLimit()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-shell-preferences-size-limit");
        string preferencesPath = workspace.PathFor(Path.Combine("state", "preferences.v1.json"));
        var preferences = new ShellPreferenceSnapshot("Dark", "Traditional Chinese", true);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        SavePreferences(preferencesPath, preferences);
        string json = File.ReadAllText(preferencesPath);
        int paddingLength = checked((int)(
            ShellPreferenceFileStore.MaximumPreferencesFileBytes - utf8.GetByteCount(json)));
        Assert.True(paddingLength > 0);
        File.WriteAllText(preferencesPath, json + new string(' ', paddingLength), utf8);

        Assert.Equal(ShellPreferenceFileStore.MaximumPreferencesFileBytes, new FileInfo(preferencesPath).Length);
        Assert.Equal(preferences, LoadPreferences(preferencesPath));

        File.AppendAllText(preferencesPath, " ", utf8);

        Assert.Equal(ShellPreferenceFileStore.MaximumPreferencesFileBytes + 1, new FileInfo(preferencesPath).Length);
        Assert.Equal(ShellPreferenceSnapshot.Default, LoadPreferences(preferencesPath));
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

            Assert.Equal(expected, LoadPreferences(preferencesPath));
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

        Assert.Equal(ShellPreferenceSnapshot.Default, LoadPreferences(preferencesPath));
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
        SavePreferences(preferencesPath, original);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await ShellPreferenceFileStore.SaveAsync(TestHost.LocalFiles, preferencesPath, cancelled, cancellation.Token);

        Assert.Equal(original, LoadPreferences(preferencesPath));

        await ShellPreferenceFileStore.SaveAsync(
            TestHost.LocalFiles,
            preferencesPath,
            latest,
            TestContext.Current.CancellationToken);

        Assert.Equal(latest, LoadPreferences(preferencesPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(preferencesPath)!, "*.tmp"));
    }

    private ShellPreferenceSnapshot LoadPreferences(string path)
    {
        return ShellPreferenceFileStore.LoadAsync(TestHost.LocalFiles, path).GetAwaiter().GetResult();
    }

    private void SavePreferences(string path, ShellPreferenceSnapshot preferences)
    {
        ShellPreferenceFileStore.SaveAsync(TestHost.LocalFiles, path, preferences, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}
