using System.Text.Json;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Nvt.Core.RuntimeQuery;
using NvtFwCombiner.Application.Capabilities;
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
        Assert.Equal("{\"state\":\"Loading\",\"isStale\":false,\"reloadIssues\":[],\"icChoices\":[],\"numberChoices\":[],\"workflowModes\":[]}",
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

    /// <summary>Argument-less commands reject every supplied key, including a blank value.</summary>
    [AvaloniaTheory]
    [InlineData("state", "unexpected", "value")]
    [InlineData("catalog.list", "unexpected", "value")]
    [InlineData("state", "confirm", "true")]
    [InlineData("catalog.list", "unexpected", "")]
    public async Task ArgumentlessCommandsRejectArguments(string command, string key, string value)
    {
        using var workspace = TempWorkspace.Create("runtime-arguments");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default);
        RuntimeQueryResponseEnvelope response = await ExecuteFromWorkerAsync(
            window.LaunchCoordinator.RuntimeQuery, command,
            new Dictionary<string, string>(StringComparer.Ordinal) { [key] = value });
        Assert.Equal(RuntimeQueryResponseEnvelope.Failure("INVALID_ARGUMENTS",
            "This command accepts no arguments."), response);
    }

    /// <summary>An empty dictionary is equivalent to omitting the arguments.</summary>
    [AvaloniaTheory]
    [InlineData("state")]
    [InlineData("catalog.list")]
    public async Task ArgumentlessCommandsAcceptEmptyArguments(string command)
    {
        using var workspace = TempWorkspace.Create("runtime-empty-arguments");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default);
        RuntimeQueryResponseEnvelope response = await ExecuteFromWorkerAsync(window.LaunchCoordinator.RuntimeQuery,
            command, new Dictionary<string, string>(StringComparer.Ordinal));
        Assert.True(response.Ok, response.Error?.Message);
    }

    /// <summary>Every workflow and page transition projects the same identity the session uses for inspection.</summary>
    [AvaloniaTheory]
    [InlineData(ExperienceIds.StandardMerge, "Merge")]
    [InlineData(ExperienceIds.AbMerge, "Merge")]
    [InlineData(ExperienceIds.GeneralMerge, "Merge")]
    [InlineData(ExperienceIds.CtrlRamReplace, "Replace")]
    [InlineData(ExperienceIds.GeneralReplace, "Replace")]
    public async Task StateUsesWorkflowSessionIdentityAcrossNavigation(string mode, string page)
    {
        using var workspace = TempWorkspace.Create("runtime-workflow-owner");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default);
        window.Show();
        await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        IRelayCommand begin = mode switch
        {
            ExperienceIds.StandardMerge => shell.BeginNormalMergeFromHomeCommand,
            ExperienceIds.AbMerge => shell.BeginAbMergeFromHomeCommand,
            ExperienceIds.GeneralMerge => shell.BeginGeneralMergeFromHomeCommand,
            ExperienceIds.CtrlRamReplace => shell.BeginCtrlRamReplaceFromHomeCommand,
            ExperienceIds.GeneralReplace => shell.BeginGeneralReplaceFromHomeCommand,
            _ => throw new InvalidOperationException("Unknown test workflow."),
        };
        begin.Execute(null);
        shell.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);
        Assert.Equal(mode, shell.WorkflowSession.ActiveInspectionContext?.Mode);
        DesktopRuntimeQueryState state = (await QueryAsync(window, "state")).Deserialize(
            DesktopRuntimeQueryJsonContext.Default.DesktopRuntimeQueryState)!;
        Assert.Equal(new(page, mode, true, false, "Open"), state);
        shell.ShowHomeCommand.Execute(null);
        Assert.Null(shell.WorkflowSession.ActiveInspectionContext);
        Assert.Equal(JsonValueKind.Null, (await QueryAsync(window, "state")).GetProperty("workflowMode").ValueKind);
        await ReportControlTestHost.CloseAndFlushAsync(window);
    }

    /// <summary>Failed stages settle, but a dependent report stays unfinished until the session cancels it.</summary>
    [AvaloniaFact]
    public async Task StateUsesPreloadSessionSettlementForBlockedAndTerminalStages()
    {
        using var workspace = TempWorkspace.Create("runtime-preload-owner");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default);
        using var preload = new ShellPreloadSession(static _ => { },
            ShellTextResources.For(ShellLanguage.English), includeStartupReport: true);
        var query = new DesktopRuntimeQuery(window, Assert.IsType<MainWindowViewModel>(window.DataContext),
            preload, services.SupportMatrix);
        async Task AssertFinishedAsync(bool expected)
        {
            Assert.Equal(expected, preload.IsSettled);
            RuntimeQueryResponseEnvelope response = await ExecuteFromWorkerAsync(query, "state");
            Assert.True(response.Ok, response.Error?.Message);
            Assert.Equal(expected, Assert.IsType<JsonElement>(response.Data)
                .GetProperty("startupPreloadFinished").GetBoolean());
        }
        await AssertFinishedAsync(false);
        preload.AdoptReadyCatalog();
        await AssertFinishedAsync(false);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task optional = preload.RunOptionalStagesAsync(new(
            static () => { },
            async token =>
            {
                entered.SetResult();
                await release.Task.WaitAsync(token);
                throw new InvalidOperationException("Synthetic history failure.");
            },
            static (_, _) => Task.CompletedTask,
            static _ => Task.FromException(new InvalidOperationException("Synthetic diagnostics failure.")),
            static (progress, _, _) =>
            {
                progress(1, 1);
                return Task.CompletedTask;
            }), TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            Assert.Equal(ShellPreloadStageState.Running, preload.Stage(ShellPreloadSession.HistoryStageId).State);
            await AssertFinishedAsync(false);
        }
        finally
        {
            _ = release.TrySetResult();
            await optional.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        }
        Assert.Equal(ShellPreloadStageState.DependencyBlocked, preload.Stage(ShellPreloadSession.ReportStageId).State);
        await AssertFinishedAsync(false);
        Assert.True(preload.TrySkipOptional(ShellPreloadSession.HistoryStageId));
        await AssertFinishedAsync(false);
        await preload.CancelOptionalsAndDrainAsync();
        Assert.Equal(ShellPreloadStageState.Cancelled, preload.Stage(ShellPreloadSession.ReportStageId).State);
        Assert.Equal(ShellPreloadStageState.Failed, preload.Stage(ShellPreloadSession.DiagnosticsStageId).State);
        Assert.Equal(ShellPreloadStageState.Skipped, preload.Stage(ShellPreloadSession.HistoryStageId).State);
        Assert.Equal(ShellPreloadStageState.Succeeded, preload.Stage(ShellPreloadSession.ViewsStageId).State);
        await AssertFinishedAsync(true);
    }

    /// <summary>A cancelled draft's retained choices are not part of the current catalog query.</summary>
    [AvaloniaFact]
    public async Task CatalogDoesNotExposeChoicesAfterDraftCancellation()
    {
        using var workspace = TempWorkspace.Create("runtime-cancelled-draft");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default);
        window.Show();
        await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        DesktopRuntimeQueryCatalog before = await CatalogAsync(window);
        shell.BeginAbMergeFromHomeCommand.Execute(null);
        Assert.True(shell.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.NotEmpty((await CatalogAsync(window)).IcChoices);
        shell.WorkflowSession.CancelWorkflowContextCommand.Execute(null);
        Assert.False(shell.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.NotEmpty(shell.WorkflowSession.WorkflowContextSetup.IcChoices);
        DesktopRuntimeQueryCatalog cancelled = await CatalogAsync(window);
        Assert.Empty(cancelled.IcChoices);
        Assert.Empty(cancelled.NumberChoices);
        Assert.Equal(before.WorkflowModes, cancelled.WorkflowModes);
        await ReportControlTestHost.CloseAndFlushAsync(window);
    }

    /// <summary>Loading, blocking, empty, current and stale publications keep their Application decisions.</summary>
    [AvaloniaTheory]
    [InlineData(CanonicalSupportMatrixCatalogState.Loading, false, false)]
    [InlineData(CanonicalSupportMatrixCatalogState.ColdStartBlocked, false, false)]
    [InlineData(CanonicalSupportMatrixCatalogState.Current, true, false)]
    [InlineData(CanonicalSupportMatrixCatalogState.Current, true, true)]
    [InlineData(CanonicalSupportMatrixCatalogState.LastKnownGood, true, true)]
    public async Task CatalogPreservesLifecycleAndReloadIssues(CanonicalSupportMatrixCatalogState state,
        bool hasPublication, bool hasRows)
    {
        using var workspace = TempWorkspace.Create("runtime-catalog-lifecycle");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default);
        window.Show();
        await window.StartupWork.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        CanonicalSupportMatrixSnapshot published = Assert.IsType<CanonicalSupportMatrixSnapshot>(
            services.SupportMatrix.Query().Matrix);
        CanonicalSupportMatrixSnapshot? matrix = hasPublication
            ? new(published.CatalogId, published.CatalogVersion, published.SourceSha256,
                published.ResolutionToken, hasRows ? published.Rows : [])
            : null;
        CapabilityCatalogIssue[] issues = state is CanonicalSupportMatrixCatalogState.ColdStartBlocked or
            CanonicalSupportMatrixCatalogState.LastKnownGood
            ? [new(CapabilityCatalogIssueCodes.SourceInvalid, "Invalid catalog.", "test-catalog")]
            : [];
        var result = new CanonicalSupportMatrixQueryResult(state, matrix, issues);
        using var preload = new ShellPreloadSession(static _ => { },
            ShellTextResources.For(ShellLanguage.English));
        var query = new DesktopRuntimeQuery(window, Assert.IsType<MainWindowViewModel>(window.DataContext),
            preload, new FixedSupportMatrix(result));
        RuntimeQueryResponseEnvelope response = await ExecuteFromWorkerAsync(query, "catalog.list");
        Assert.True(response.Ok, response.Error?.Message);
        JsonElement data = Assert.IsType<JsonElement>(response.Data);
        Assert.Equal(state.ToString(), data.GetProperty("state").GetString());
        Assert.Equal(result.IsStale, data.GetProperty("isStale").GetBoolean());
        Assert.Equal(issues, data.GetProperty("reloadIssues").Deserialize<CapabilityCatalogIssue[]>(
            RuntimeQueryProtocol.CompactJsonOptions));
        Assert.Equal((matrix?.Rows ?? []).Select(static row => row.Identity.WorkflowId)
                .Distinct(StringComparer.Ordinal),
            data.GetProperty("workflowModes").EnumerateArray().Select(static mode => mode.GetString()));
        await ReportControlTestHost.CloseAndFlushAsync(window);
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
        return ExecuteFromWorkerAsync(window.LaunchCoordinator.RuntimeQuery, command);
    }

    private static Task<RuntimeQueryResponseEnvelope> ExecuteFromWorkerAsync(DesktopRuntimeQuery query,
        string command, IReadOnlyDictionary<string, string>? arguments = null)
    {
        // The production handlers VerifyAccess before reading any UI facts; successful data proves dispatch.
        return Task.Run(() =>
        {
            Assert.False(Dispatcher.UIThread.CheckAccess());
            return query.ExecuteAsync(new(Version, command, arguments), Version,
                TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(15),
            TestContext.Current.CancellationToken);
    }

    private sealed class FixedSupportMatrix(CanonicalSupportMatrixQueryResult result) : ICanonicalSupportMatrixQuery
    {
        public CanonicalSupportMatrixQueryResult Query()
        {
            return result;
        }
    }
}
