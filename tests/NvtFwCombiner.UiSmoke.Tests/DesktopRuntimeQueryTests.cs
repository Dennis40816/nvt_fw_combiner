using System.Text.Json;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Nvt.Core.RuntimeQuery;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Read-only product queries through Core routing and the real UI dispatcher.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class DesktopRuntimeQueryTests
{
    private const string Version = "test-runtime-v1";

    /// <summary>Both definitions are read-only and leave startup spellings with NFC's existing parser.</summary>
    [AvaloniaFact]
    public async Task CommandDefinitionsAreReadOnlyAndRuntimeOnly()
    {
        using var workspace = TempWorkspace.Create("runtime-definitions");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default);
        Assert.Equal<string>(["state", "catalog.list"], window.LaunchCoordinator.RuntimeQuery.Router.RegisteredCommands);
        Assert.Equal(window.LaunchCoordinator.RuntimeQuery.Router.RegisteredCommands,
            window.LaunchCoordinator.RuntimeQuery.Commands.Select(static command => command.Name));
        Assert.All(window.LaunchCoordinator.RuntimeQuery.Commands, static command =>
        {
            Assert.Equal(RuntimeQueryCommandRisk.ReadOnly, command.Risk);
            Assert.Equal(RuntimeQueryStartupPhase.None, command.StartupPhase);
            Assert.Null(command.StartupValueKey);
            Assert.Null(command.StartupValidator);
        });
        string[] arguments = ["--state", "--catalog.list", "--confirm"];
        RuntimeQueryStartupParseResult parsed = window.LaunchCoordinator.RuntimeQuery.Router.ParseStartupArguments(arguments);
        Assert.Empty(parsed.Calls);
        Assert.Empty(parsed.Issues);
        Assert.Equal(arguments, parsed.RemainingArguments);
    }

    /// <summary>A fresh window exposes its complete state and empty UI/catalog choices without starting preload.</summary>
    [AvaloniaFact]
    public async Task FreshWindowQueriesReadOnUiThreadWithoutChangingState()
    {
        using var workspace = TempWorkspace.Create("runtime-fresh");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default);
        Assert.Equal(
            "{\"page\":\"Home\",\"workflowMode\":null,\"startupPreloadFinished\":false,\"isRunInProgress\":false,\"closePhase\":\"Open\"}",
            (await QueryAsync(window, "state")).GetRawText());
        Assert.Equal("{\"icChoices\":[],\"numberChoices\":[],\"workflowModes\":[]}",
            (await QueryAsync(window, "catalog.list")).GetRawText());
        Assert.False(window.IsVisible);
        Assert.False(Assert.IsType<MainWindowViewModel>(window.DataContext).HasSelectedFiles);
    }

    /// <summary>Queries read the actual page, workflow and completed report preload; catalog reads the UI draft.</summary>
    [AvaloniaTheory]
    [InlineData("home", "Home", null)]
    [InlineData("merge", "Merge", ExperienceIds.StandardMerge)]
    [InlineData("replace", "Replace", ExperienceIds.CtrlRamReplace)]
    [InlineData("hex-editor", "HexEditor", null)]
    public async Task QueriesReflectPreloadAndCurrentUiChoices(string startupPage, string page, string? workflow)
    {
        using var workspace = TempWorkspace.Create("runtime-preload");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        string report = workspace.Write("preload.json",
            System.Text.Encoding.UTF8.GetBytes(ReportJsonSamples.Succeeded("runtime-preload")));
        UiLaunchOptions options = UiLaunchOptions.Parse(["--page", startupPage, "--report", report]);
        Assert.Empty(options.Issues);
        using var window = new MainWindow(options, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default);
        window.Show();
        await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        Assert.True(shell.Reports.HasLoadedReport);
        DesktopRuntimeQueryState state = (await QueryAsync(window, "state")).Deserialize(
            DesktopRuntimeQueryJsonContext.Default.DesktopRuntimeQueryState)!;
        Assert.Equal(new(page, workflow, true, false, "Open"), state);

        DesktopRuntimeQueryCatalog beforeSetup = await CatalogAsync(window);
        Assert.Empty(beforeSetup.IcChoices);
        Assert.Empty(beforeSetup.NumberChoices);
        Assert.Equal<string>([ExperienceIds.AbMerge, ExperienceIds.CtrlRamReplace, ExperienceIds.GeneralMerge,
            ExperienceIds.GeneralReplace, ExperienceIds.StandardMerge],
            beforeSetup.WorkflowModes.Order(StringComparer.Ordinal));

        shell.ShowHomeCommand.Execute(null);
        shell.BeginNormalMergeFromHomeCommand.Execute(null);
        WorkflowContextSetupViewModel setup = shell.WorkflowSession.WorkflowContextSetup;
        Assert.True(shell.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.NotEmpty(setup.IcChoices);
        Assert.NotEmpty(setup.NumberChoices);
        DesktopRuntimeQueryCatalog catalog = await CatalogAsync(window);
        Assert.Equal(setup.IcChoices, catalog.IcChoices);
        Assert.Equal(setup.NumberChoices.Select(static choice => (choice.Token, choice.DisplayLabel)),
            catalog.NumberChoices.Select(static choice => (choice.Token, choice.DisplayLabel)));
        Assert.Equal(beforeSetup.WorkflowModes, catalog.WorkflowModes);
        Assert.True(shell.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.Equal(ShellPage.Home, shell.SelectedPage);

        setup.SelectedIc = setup.IcChoices[^1];
        DesktopRuntimeQueryCatalog changed = await CatalogAsync(window);
        Assert.Equal(setup.IcChoices, changed.IcChoices);
        Assert.Equal(setup.NumberChoices.Select(static choice => (choice.Token, choice.DisplayLabel)),
            changed.NumberChoices.Select(static choice => (choice.Token, choice.DisplayLabel)));
        shell.WorkflowSession.CancelWorkflowContextCommand.Execute(null);
        await ReportControlTestHost.CloseAndFlushAsync(window);
        Assert.Equal("Closed", (await QueryAsync(window, "state")).GetProperty("closePhase").GetString());
    }

    /// <summary>Unknown commands, including later generic commands, retain Core's exact error.</summary>
    [AvaloniaTheory]
    [InlineData(" unknown ")]
    [InlineData("help")]
    [InlineData("ping")]
    [InlineData("focus")]
    [InlineData("page")]
    [InlineData("screenshot")]
    [InlineData("exit")]
    public async Task RouterRetainsCoreUnknownCommandError(string command)
    {
        using var workspace = TempWorkspace.Create("runtime-unknown");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default);
        RuntimeQueryResponseEnvelope response = await ExecuteFromWorkerAsync(window, command);
        Assert.Equal(RuntimeQueryResponseEnvelope.Failure("UNKNOWN_COMMAND",
            $"Unknown query command '{command}'."), response);
    }

    /// <summary>Preview and Build share the existing run lifetime; queries neither cancel nor start a run.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StateReflectsActivePreviewOrBuildAndItsCompletion(bool build)
    {
        using var workspace = TempWorkspace.Create("runtime-active-run");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Parse(["--page=merge"]),
            StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        window.Show();
        await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task run = shell.RunSession.RunCompositionAsync(
            shell.Merge.CaptureRunContext(ExperienceIds.StandardMerge, build), build,
            async (_, token) =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("Synthetic run must end by cancellation.");
            }, (_, _) => { });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            DesktopRuntimeQueryState active = (await QueryAsync(window, "state")).Deserialize(
                DesktopRuntimeQueryJsonContext.Default.DesktopRuntimeQueryState)!;
            Assert.Equal(new("Merge", ExperienceIds.StandardMerge, true, true, "Open"), active);
            Assert.True(shell.RunSession.IsRunInProgress);
            Assert.False(run.IsCompleted);
        }
        finally
        {
            shell.RunSession.CancelActiveRun();
            await run.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        }
        Assert.False((await QueryAsync(window, "state")).GetProperty("isRunInProgress").GetBoolean());
        await ReportControlTestHost.CloseAndFlushAsync(window);
    }

    private static Task<PresentationHostServices> CreateServicesAsync(TempWorkspace workspace)
    {
        return Task.Run(() => PresentationTestHost.CreateServices("ui-smoke",
            static authoring => authoring, workspace.Root), TestContext.Current.CancellationToken);
    }

    private static async Task<DesktopRuntimeQueryCatalog> CatalogAsync(MainWindow window)
    {
        return (await QueryAsync(window, "catalog.list")).Deserialize(
            DesktopRuntimeQueryJsonContext.Default.DesktopRuntimeQueryCatalog)!;
    }

    private static async Task<JsonElement> QueryAsync(MainWindow window, string command)
    {
        RuntimeQueryResponseEnvelope response = await ExecuteFromWorkerAsync(window, command);
        Assert.True(response.Ok, response.Error?.Message);
        Assert.Null(response.Error);
        return Assert.IsType<JsonElement>(response.Data);
    }

    private static Task<RuntimeQueryResponseEnvelope> ExecuteFromWorkerAsync(MainWindow window, string command)
    {
        // The production handlers VerifyAccess before reading any UI facts; successful data proves dispatch.
        return Task.Run(() =>
        {
            Assert.False(Dispatcher.UIThread.CheckAccess());
            return window.LaunchCoordinator.RuntimeQuery.ExecuteAsync(new(Version, command, null), Version,
                TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(15),
            TestContext.Current.CancellationToken);
    }
}
