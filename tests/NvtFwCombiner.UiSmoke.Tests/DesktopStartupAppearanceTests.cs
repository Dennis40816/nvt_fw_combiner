using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Nvt.Core.RuntimeQuery;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Temporary appearance through the Core startup and runtime command definitions.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class DesktopStartupAppearanceTests
{
    /// <summary>Help lists only the appearance commands that this slice actually releases.</summary>
    [Fact]
    public void HelpListsReleasedAppearanceCommands()
    {
        UiLaunchOptions options = UiLaunchOptions.Parse(["--help"]);
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, DesktopApplication.CompletePublicRequest(options, output, error));
        string help = output.ToString();
        Assert.Contains("--theme", help, StringComparison.Ordinal);
        Assert.Contains("--language", help, StringComparison.Ordinal);
        Assert.Contains("--motion", help, StringComparison.Ordinal);
        Assert.DoesNotContain("--window-size", help, StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    /// <summary>Every declared appearance value passes strict validation before composition.</summary>
    [Theory]
    [InlineData("theme", "light")]
    [InlineData("theme", "dark")]
    [InlineData("theme", "system")]
    [InlineData("language", "en")]
    [InlineData("language", "zh-Hant")]
    [InlineData("motion", "full")]
    [InlineData("motion", "reduced")]
    [InlineData("theme", "LIGHT")]
    [InlineData("theme", "DaRk")]
    [InlineData("theme", "SYSTEM")]
    [InlineData("language", "EN")]
    [InlineData("language", "ZH-HANT")]
    [InlineData("motion", "FULL")]
    [InlineData("motion", "ReDuCeD")]
    public void ReleasedAppearanceValuesAreAcceptedForSavedReportCapture(string command, string value)
    {
        using var workspace = TempWorkspace.Create("startup-appearance-parse");
        UiLaunchOptions options = UiLaunchOptions.Parse(
            [$"--{command}={value}", "--report", workspace.PathFor("report.json"), "--open-report",
                "--capture", workspace.PathFor("out.png")], new LocalFileStore());
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Null(DesktopApplication.CompletePublicRequest(options, output, error));
        Assert.Empty(options.Issues);
        RuntimeQueryStartupCall call = Assert.Single(options.StartupCommands);
        Assert.Equal(command, call.Command.Name);
        Assert.Equal(value, call.Args!["value"]);
        Assert.Equal(RuntimeQueryCommandRisk.ChangesState, call.Command.Risk);
        Assert.Equal(RuntimeQueryStartupPhase.AfterStartup, call.Phase);
    }

    /// <summary>All grammar and value failures return 64 in strict requests and remain interactive otherwise.</summary>
    [Theory]
    [InlineData("theme", "unknown")]
    [InlineData("language", "zh-Hans")]
    [InlineData("motion", "automatic")]
    public void InvalidAppearanceRequestsUseStrictOrRecoverableCompletion(string command, string invalid)
    {
        string valid = command switch
        {
            "theme" => "light",
            "language" => "en",
            "motion" => "full",
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };
        string[][] requests = [[$"--{command}={invalid}"], [$"--{command}"], [$"--{command}="],
            [$"--{command}", " "], [$"--{command}={invalid}", $"--{command}={invalid}"],
            [$"--{command}={valid}", $"--{command}", valid.ToUpperInvariant()]];
        using var workspace = TempWorkspace.Create("startup-appearance-refused");
        foreach (string[] request in requests)
        {
            UiLaunchOptions ordinary = UiLaunchOptions.Parse(request);
            Assert.NotEmpty(ordinary.Issues);
            using var output = new StringWriter();
            using var error = new StringWriter();
            Assert.Null(DesktopApplication.CompletePublicRequest(ordinary, output, error));
            UiLaunchOptions capture = UiLaunchOptions.Parse([.. request, "--report", workspace.PathFor("report.json"),
                "--open-report", "--capture", workspace.PathFor("out.png")], new LocalFileStore());
            Assert.Equal(64, DesktopApplication.CompletePublicRequest(capture, output, error));
            Assert.Contains($"--{command}", error.ToString(), StringComparison.Ordinal);
            Assert.False(File.Exists(workspace.PathFor("out.png")));
            UiLaunchOptions help = UiLaunchOptions.Parse([.. request, "--help"]);
            Assert.Equal(64, DesktopApplication.CompletePublicRequest(help, output, error));
            int hostConstructions = 0;
            InvalidOperationException reached = Assert.Throws<InvalidOperationException>(() => DesktopApplication.Run(
                () => { hostConstructions++; throw new InvalidOperationException("Interactive host reached."); },
                new LocalFileStore(), workspace.Root, request));
            Assert.Equal("Interactive host reached.", reached.Message);
            Assert.Equal(1, hostConstructions);
        }
    }

    /// <summary>A missing value never consumes a positional token beyond a legacy UI option.</summary>
    [Fact]
    public void UiParsingPreservesCoreValueBoundariesAndIdenticalLegacyValues()
    {
        UiLaunchOptions valid = UiLaunchOptions.Parse(["--theme", "light", "--report", "light", "--open-report"]);
        Assert.Empty(valid.Issues);
        Assert.Equal("light", valid.ReportPath);
        Assert.Equal("light", Assert.Single(valid.StartupCommands).Args!["value"]);
        UiLaunchOptions invalid = UiLaunchOptions.Parse(["--theme", "--page=home", "light", "--help"]);
        Assert.Contains("--theme requires a value.", invalid.Issues);
        Assert.Contains("Unsupported startup argument 'light'.", invalid.Issues);
    }

    /// <summary>Startup and later worker-thread runtime calls apply the same choices through existing UI propagation.</summary>
    [AvaloniaTheory]
    [InlineData("theme", "light", "Light")]
    [InlineData("theme", "dark", "Dark")]
    [InlineData("theme", "system", "System")]
    [InlineData("language", "en", "English")]
    [InlineData("language", "zh-Hant", "Traditional Chinese")]
    [InlineData("motion", "full", "False")]
    [InlineData("motion", "reduced", "True")]
    [InlineData("theme", "LIGHT", "Light")]
    [InlineData("theme", "DaRk", "Dark")]
    [InlineData("theme", "SYSTEM", "System")]
    [InlineData("language", "EN", "English")]
    [InlineData("language", "ZH-HANT", "Traditional Chinese")]
    [InlineData("motion", "FULL", "False")]
    [InlineData("motion", "ReDuCeD", "True")]
    public async Task StartupAndRuntimeApplyTemporaryAppearance(string command, string value, string expected)
    {
        using var workspace = TempWorkspace.Create("startup-appearance-owner");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        ShellPreferenceSnapshot saved = new("Dark", "Traditional Chinese", true, false);
        UiLaunchOptions options = UiLaunchOptions.Parse([$"--{command}", value]);
        using var window = new MainWindow(options, StartupTraceSession.Disabled, services, saved);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        bool beforeInputsObserved = false;
        window.LaunchCoordinator.Context.BeforeInputs += (_, _) =>
        {
            Assert.Equal(saved, shell.ExportShellPreferences());
            beforeInputsObserved = true;
            return Task.CompletedTask;
        };
        window.Show();
        await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.True(beforeInputsObserved);
        AssertAppearance(window, shell, command, expected);
        Assert.Equal(saved, window.LaunchCoordinator.Persistence.ExportPreferences(shell));
        shell.ExpandInputDetailsByDefault = true;
        RuntimeQueryResponseEnvelope result = await ExecuteAsync(window, command,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["value"] = value });
        Assert.True(result.Ok, result.Error?.Message);
        AssertAppearance(window, shell, command, expected);
        ShellPreferenceSnapshot expectedSaved = saved with { ExpandInputDetailsByDefault = true };
        Assert.Equal(expectedSaved, window.LaunchCoordinator.Persistence.ExportPreferences(shell));
        await ReportControlTestHost.CloseAndFlushAsync(window);
        Assert.Equal(expectedSaved, await ShellPreferenceFileStore.LoadAsync(services.LocalFiles,
            ShellPreferenceFileStore.PathIn(services.LocalStateDirectory)));
        using var next = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, expectedSaved);
        Assert.Equal(expectedSaved, Assert.IsType<MainWindowViewModel>(next.DataContext).ExportShellPreferences());
    }

    /// <summary>Runtime validation refuses missing, unknown and extra arguments without mutating effective or saved state.</summary>
    [AvaloniaTheory]
    [InlineData("theme", "neon")]
    [InlineData("language", "zh-Hans")]
    [InlineData("motion", "automatic")]
    public async Task RuntimeRefusalLeavesAppearanceUnchanged(string command, string invalid)
    {
        using var workspace = TempWorkspace.Create("runtime-appearance-refused");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        IReadOnlyDictionary<string, string>?[] requests = [null,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["value"] = "" },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["value"] = invalid },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["other"] = invalid },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["value"] = invalid, ["confirm"] = "true" }];
        foreach (IReadOnlyDictionary<string, string>? arguments in requests)
        {
            RuntimeQueryResponseEnvelope response = await ExecuteAsync(window, command, arguments);
            Assert.Equal("INVALID_ARGUMENTS", response.Error?.Code);
            Assert.Equal(ShellPreferenceSnapshot.Default, shell.ExportShellPreferences());
            Assert.Equal(ShellPreferenceSnapshot.Default, window.LaunchCoordinator.Persistence.ExportPreferences(shell));
        }
        Assert.Empty(Directory.GetFiles(workspace.Root));
    }

    /// <summary>Core 0.2.0 cannot route a before-first-frame definition at runtime, even though startup succeeds.</summary>
    [Fact]
    public async Task BeforeFirstFrameCommandCannotShareItsDefinitionWithRuntime()
    {
        int applications = 0;
        RuntimeQueryCommand command = new("window-size", RuntimeQueryCommandRisk.ChangesState, _ =>
        {
            applications++;
            return Task.FromResult(RuntimeQueryResponseEnvelope.Success(true));
        }, RuntimeQueryStartupPhase.BeforeFirstFrame, "value");
        var router = new RuntimeQueryCommandRouter(Array.AsReadOnly<RuntimeQueryCommand>([command]), requireConfirmation: false);
        RuntimeQueryStartupParseResult parsed = router.ParseStartupArguments(["--window-size=980x640"]);
        Assert.Empty(parsed.Issues);
        RuntimeQueryStartupCallResult startup = Assert.Single(await router.ExecuteStartupPhaseAsync(parsed.Calls,
            RuntimeQueryStartupPhase.BeforeFirstFrame));
        Assert.True(startup.Response.Ok);
        RuntimeQueryResponseEnvelope runtime = await router.RouteAsync("window-size", parsed.Calls[0].Args);
        Assert.Equal(RuntimeQueryResponseEnvelope.Failure("STARTUP_ONLY",
            "Command 'window-size' can be used only at startup."), runtime);
        Assert.Equal(1, applications);
    }

    private static async Task<PresentationHostServices> CreateServicesAsync(TempWorkspace workspace)
    {
        return await Task.Run(() => PresentationTestHost.CreateServices("ui-smoke",
            static authoring => authoring, workspace.Root), TestContext.Current.CancellationToken);
    }

    private static Task<RuntimeQueryResponseEnvelope> ExecuteAsync(MainWindow window, string command,
        IReadOnlyDictionary<string, string>? arguments)
    {
        return Task.Run(() => window.LaunchCoordinator.RuntimeQuery.ExecuteAsync(new("appearance-test", command, arguments),
            "appearance-test", TestContext.Current.CancellationToken), TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
    }

    private static void AssertAppearance(MainWindow window, MainWindowViewModel shell, string command, string expected)
    {
        switch (command)
        {
            case "theme":
                Assert.Equal(expected, shell.SelectedTheme);
                ThemeVariant variant = expected switch
                {
                    "Light" => ThemeVariant.Light,
                    "Dark" => ThemeVariant.Dark,
                    _ => ThemeVariant.Default,
                };
                Assert.Equal(variant, window.RequestedThemeVariant);
                break;
            case "language":
                Assert.Equal(expected, shell.SelectedLanguage);
                Assert.Equal(ShellTextResources.LanguageFromPreference(expected), shell.Text.Language);
                break;
            case "motion":
                Assert.Equal(expected, shell.IsReducedMotionEnabled.ToString());
                break;
            default:
                throw new InvalidOperationException($"Unknown appearance command '{command}'.");
        }
    }

    /// <summary>Invalid ordinary options show the current recoverable startup report in a usable window.</summary>
    [AvaloniaTheory]
    [InlineData("theme")]
    [InlineData("language")]
    [InlineData("motion")]
    public async Task InteractiveInvalidAppearanceShowsRecoverableStartupState(string command)
    {
        using var workspace = TempWorkspace.Create("startup-appearance-recovery");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        UiLaunchOptions options = UiLaunchOptions.Parse([$"--{command}=unknown"]);
        using var window = new MainWindow(options, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        Assert.True(window.IsVisible);
        Assert.NotNull(shell.Reports.LoadedReport);
        Assert.Equal(ShellPreferenceSnapshot.Default, shell.ExportShellPreferences());
        await ReportControlTestHost.CloseAndFlushAsync(window);
    }

    /// <summary>Every appearance value reaches the real captured surface without changing any existing store or input.</summary>
    [AvaloniaTheory]
    [InlineData("theme", "light", "Light")]
    [InlineData("theme", "dark", "Dark")]
    [InlineData("theme", "system", "System")]
    [InlineData("language", "en", "English")]
    [InlineData("language", "zh-Hant", "Traditional Chinese")]
    [InlineData("motion", "full", "False")]
    [InlineData("motion", "reduced", "True")]
    public async Task CaptureUsesTemporaryAppearanceAndPreservesStores(string command, string value, string expected)
    {
        using var workspace = TempWorkspace.Create("startup-appearance-capture");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        ShellPreferenceSnapshot saved = new("Dark", "Traditional Chinese", true, false);
        string preferences = ShellPreferenceFileStore.PathIn(services.LocalStateDirectory);
        await ShellPreferenceFileStore.SaveAsync(services.LocalFiles, preferences, saved, TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(preferences, "\n ", TestContext.Current.CancellationToken);
        string report = workspace.Write("report.json", System.Text.Encoding.UTF8.GetBytes(ReportJsonSamples.Succeeded()));
        _ = workspace.Write("configuration-state.data", [7, 8, 9]);
        Dictionary<string, byte[]> before = Directory.GetFiles(workspace.Root, "*", SearchOption.AllDirectories)
            .ToDictionary(static path => path, File.ReadAllBytes);
        string destination = workspace.PathFor("out.png");
        UiLaunchOptions options = UiLaunchOptions.Parse([$"--{command}", value, "--report", report,
            "--open-report", "--capture", destination], services.LocalFiles);
        Assert.Empty(options.Issues);
        using var window = new MainWindow(options, StartupTraceSession.Disabled, services, saved);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => closed.TrySetResult();
        window.Show();
        await window.LaunchCoordinator.CaptureSession.CaptureFrameRequested.WaitAsync(TimeSpan.FromSeconds(20),
            TestContext.Current.CancellationToken);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        AssertAppearance(window, shell, command, expected);
        Assert.True(shell.Reports.IsReportModalOpen);
        shell.ExpandInputDetailsByDefault = true;
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.Equal(0, window.LaunchCoordinator.CaptureSession.CaptureExitCode);
        using var bitmap = new Bitmap(destination);
        Assert.True(bitmap.PixelSize.Width > 0);
        Assert.True(bitmap.PixelSize.Height > 0);
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(workspace.Root, "*", SearchOption.AllDirectories)
            .Where(path => path != destination).Order());
        foreach ((string path, byte[] bytes) in before) { Assert.Equal(bytes, File.ReadAllBytes(path)); }
    }

    /// <summary>Repeated runtime changes retain original saved values and never queue appearance persistence.</summary>
    [AvaloniaFact]
    public async Task RuntimeAppearanceChangesNeverWritePreferencesOrReplaceSavedValues()
    {
        using var workspace = TempWorkspace.Create("runtime-appearance-local-state");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        var files = new RecordingLocalFileStore();
        services = new(services.Composition, services.FileReveal, services.SupportMatrix, services.SystemInformation,
            services.SystemDiagnosticsExporter, services.RawBinaryEditorFileSessions, services.CanonicalCatalogLoader,
            services.ExternalEnvironmentLoader, files, services.LocalStateDirectory);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        int startupWrites = files.Writes.Count;
        foreach ((string command, string value) in new (string, string)[]
            { ("theme", "dark"), ("theme", "system"), ("language", "zh-Hant"), ("motion", "reduced"), ("motion", "full") })
        {
            Assert.True((await ExecuteAsync(window, command,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["value"] = value })).Ok);
        }
        Assert.Equal(ShellPreferenceSnapshot.Default, window.LaunchCoordinator.Persistence.ExportPreferences(shell));
        await ReportControlTestHost.CloseAndFlushAsync(window);
        Assert.Equal(startupWrites, files.Writes.Count);
        Assert.DoesNotContain(ShellPreferenceFileStore.PathIn(services.LocalStateDirectory), files.Writes);
    }

    /// <summary>Synchronous suppression nests and unwinds after failure so later user changes can persist.</summary>
    [AvaloniaFact]
    public void TemporaryLocalStateSuppressionRestoresAfterNestedFailure()
    {
        var persistence = new CapturePersistenceScope(false);
        int queues = 0;
        _ = Assert.Throws<InvalidOperationException>(() => persistence.WithoutLocalStateWrites(() =>
        {
            persistence.WithoutLocalStateWrites(() => persistence.QueueLocalState(() => queues++));
            Assert.False(persistence.AllowLocalStateWrites);
            throw new InvalidOperationException("Temporary application failed.");
        }));
        Assert.Equal(0, queues);
        Assert.True(persistence.AllowLocalStateWrites);
        persistence.QueueLocalState(() => queues++);
        Assert.Equal(1, queues);
    }
}
