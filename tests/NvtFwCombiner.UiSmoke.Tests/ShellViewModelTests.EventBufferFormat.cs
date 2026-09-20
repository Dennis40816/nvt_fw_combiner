using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Contracts.Configuration;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>Explicit reload accepts external changes and cannot replace an unsaved editor draft.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EventBufferFormatExplicitReloadRecoversExternalChangesWithoutDiscardingDraft(bool invalid)
    {
        var storage = new EventBufferFormatStorage();
        using var session = new EventBufferFormatConfigurationSession(
            "event-buffer-format", [new("desay", "Desay")], [new("desay", null, [0x97])], storage);
        MainWindowViewModel viewModel = CreateEventBufferFormatViewModel(session);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;
        CommunityToolkit.Mvvm.Input.IAsyncRelayCommand reload = viewModel.Settings.ReloadEventBufferFormatCommand;
        int reapplies = 0;
        viewModel.Settings.ReapplyEventBufferFormatAsync = () => { reapplies++; return Task.FromResult(true); };
        EventBufferFormatDraftRowViewModel row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
        row.AliasName = "Unsaved";
        storage.Stored = new(new(1, invalid ? "wrong-scope" : "event-buffer-format",
            [new("desay", "External", ["0xA6"])]), new string('b', 64));
        Assert.False(reload.CanExecute(null));
        await reload.ExecuteAsync(null);
        Assert.Equal("Unsaved", row.AliasName);
        Assert.Equal(0, reapplies);
        viewModel.Settings.DiscardEventBufferFormatChangesCommand.Execute(null);
        Assert.True(reload.CanExecute(null));
        var readHold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        storage.ReadHold = readHold;
        Task loading = reload.ExecuteAsync(null);
        try
        {
            Assert.True(viewModel.Settings.IsEventBufferFormatLoading);
            Assert.False(reload.CanExecute(null));
            Assert.False(viewModel.Settings.CanSaveEventBufferFormat);
            Assert.False(viewModel.Settings.RequestSettingsClose());
        }
        finally
        {
            readHold.SetResult();
        }
        await loading;
        Assert.Equal(1, reapplies);
        Assert.False(viewModel.Settings.HasEventBufferFormatUnsavedChanges);
        Assert.Equal(invalid ? EventBufferFormatConfigurationStatus.Invalid : EventBufferFormatConfigurationStatus.Ready,
            session.Current.Status);
        if (invalid)
        {
            Assert.Null(session.Current.Configuration);
            Assert.Equal(viewModel.Text.EventBufferFormatScopeMismatchLabel, viewModel.Settings.EventBufferFormatStatus);
        }
        else
        {
            Assert.Equal("External", Assert.Single(viewModel.Settings.EventBufferFormatRows).AliasName);
            Assert.Equal([0xA6], Assert.Single(viewModel.Settings.EventBufferFormatRows).RecognitionValues);
        }
    }

    /// <summary>Declaration-only slots have honest pending-size text; exact contracts retain numeric requirements.</summary>
    [Theory]
    [InlineData(false, "dp-ab")]
    [InlineData(false, "tp-a")]
    [InlineData(false, "tp-b")]
    [InlineData(true, "dp-ab")]
    [InlineData(true, "tp-a")]
    [InlineData(true, "tp-b")]
    public void EventBufferFormatDeclarationDescriptionsDoNotInventSizes(bool chinese, string role)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        var input = new CompiledAuthoringInputBinding("slot", "space", role);
        string pending = text.GetAbSlotDescription(input);
        Assert.Contains(chinese ? "偵測格式後" : "after format detection", pending, StringComparison.Ordinal);
        string compiled = text.GetAbSlotDescription(input with { RequiredEndExclusive = 0x37000 });
        Assert.Contains(chinese ? "必要 prefix" : "Required prefix", compiled, StringComparison.Ordinal);
        Assert.DoesNotContain(chinese ? "偵測格式後" : "after format detection", compiled, StringComparison.Ordinal);
        _ = Assert.Throws<InvalidOperationException>(() => text.GetAbSlotDescription(input with { Role = "unrecognized" }));
    }

    /// <summary>Saving recognition bytes updates already-loaded AB input and output authority without reopening BIN paths.</summary>
    [Theory]
    [InlineData("active")]
    [InlineData("standard")]
    [InlineData("replace")]
    [InlineData("failure")]
    [InlineData("reload")]
    [InlineData("alias")]
    public async Task EventBufferFormatSaveReappliesLoadedAbInputsWithoutFileReload(string context)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ui-ab-config-reapply");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        MainWindowViewModel viewModel = await CreateLoadedFormatAbViewModelAsync(workspace, host,
            secondFormat: context == "failure" ? (byte)0xA6 : (byte)0x97);
        FirmwareSlotViewModel slot = viewModel.Merge.AbMergeSlots.Single(static slot => slot.SlotId == "tp-a-input");
        Assert.Contains(slot.FirmwareFacts, fact => fact.Label == "Event Buffer Version" && fact.Value == "0x97 - Desay");
        File.Delete(workspace.PathFor("a.bin"));
        File.Delete(workspace.PathFor("b.bin"));
        if (context == "standard") { viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge; }
        if (context == "replace")
        {
            viewModel.ShowReplaceCommand.Execute(null);
            Assert.True(viewModel.Navigation.IsNavigationClearConfirmationOpen);
            viewModel.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
            viewModel.WorkflowSession.SelectedIc = "NT51928";
            Assert.True(viewModel.IsReplaceVisible);
        }
        string selectedIc = viewModel.WorkflowSession.SelectedIc;
        string selectedMode = viewModel.Merge.SelectedMergeMode;
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;
        if (context == "reload")
        {
            IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
            Assert.True((await configuration.SaveAsync([new("desay", null, [])], TestContext.Current.CancellationToken)).Succeeded);
            await viewModel.Settings.ReloadEventBufferFormatCommand.ExecuteAsync(null);
        }
        else
        {
            if (context == "alias") { Assert.Single(viewModel.Settings.EventBufferFormatRows).AliasName = "My vendor"; }
            else { Assert.Single(viewModel.Settings.EventBufferFormatRows).RecognitionValues.Clear(); }
            if (context == "failure") { Assert.Single(viewModel.Settings.EventBufferFormatRows).RecognitionValues.Add(0xA6); }
            await viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);
        }
        if (context == "failure")
        {
            Assert.True(slot.BlocksBuild);
            Assert.Null(slot.CurrentInspectionProjection!.AbMergeFacts!.EventBufferFormat);
            Assert.DoesNotContain(slot.FirmwareFacts, fact => fact.Label == viewModel.Text.EventBufferVersionLabel);
            viewModel.SelectedLanguage = "Traditional Chinese";
            Assert.DoesNotContain(slot.FirmwareFacts, fact => fact.Label == viewModel.Text.EventBufferVersionLabel);
            Assert.True(slot.BlocksBuild);
            Assert.Equal(viewModel.Text.EventBufferFormatReapplyFailedLabel, viewModel.Settings.EventBufferFormatStatus);
            Assert.False(viewModel.Settings.HasEventBufferFormatUnsavedChanges);
            Assert.Single(viewModel.Settings.EventBufferFormatRows).RecognitionValues.Clear();
            await viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);
            Assert.False(slot.BlocksBuild);
            Assert.Empty(slot.CurrentInspectionProjection!.AuthoringCompilationIssues);
            Assert.Equal(viewModel.Text.EventBufferFormatSavedLabel, viewModel.Settings.EventBufferFormatStatus);
            return;
        }
        if (context == "replace")
        {
            Assert.False(slot.HasFile);
            Assert.Null(slot.CurrentInspectionProjection);
            Assert.Equal(selectedIc, viewModel.WorkflowSession.SelectedIc);
            Assert.True(viewModel.IsReplaceVisible);
            return;
        }
        Assert.Equal(context == "alias" ? "nt51950-ab-desay-maps" : "nt51950-ab-merge-maps",
            Assert.Single(slot.CurrentInspectionProjection!.InputSlotCatalog!.Routes).ExactCapability!.Identity.MapVariant);
        Assert.False(slot.BlocksBuild);
        _ = Assert.NotNull(slot.CurrentInspectionProjection.InputSlotStatus!.AcceptedBytes);
        Assert.Contains(slot.FirmwareFacts, fact => fact.Label == viewModel.Text.EventBufferVersionLabel &&
            fact.Value == (context == "alias" ? "0x97 - My vendor" : "0x97 - Common"));
        Assert.False(viewModel.Settings.HasEventBufferFormatUnsavedChanges);
        Assert.Equal(viewModel.Text.EventBufferFormatSavedLabel, viewModel.Settings.EventBufferFormatStatus);
        Assert.Equal(selectedIc, viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(selectedMode, viewModel.Merge.SelectedMergeMode);
    }

    private static async Task<MainWindowViewModel> CreateLoadedFormatAbViewModelAsync(
        TempWorkspace workspace, CompositionHostServices host, IAbMergeAuthoring? authoring = null, byte secondFormat = 0x97,
        ICompositionExecution? execution = null, bool initializeConfiguration = true, ICompositionOutputNaming? outputNaming = null)
    {
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        if (initializeConfiguration)
        {
            Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        }
        PresentationHostServices services = PresentationTestHost.CreateServices(ApplicationVersionProvider.InformationalVersion,
            host, static general => general, authoring, execution, outputNaming);
        MainWindowViewModel viewModel = PresentationTestHost.PublishCanonicalCatalog(services, ShellViewModelFactory.Create(services, ShellLanguage.English));
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        await viewModel.WorkflowSession.SetAbDummyDpModeAsync(true, TestContext.Current.CancellationToken);
        Assert.True(viewModel.Merge.UseDummyDpForAbMerge);
        byte[] tp = new byte[0x37000];
        tp[0x22200] = 0x31;
        tp[0x22201] = 0xCE;
        tp[0x2220C] = 0x97;
        tp[0x36000] = 0x42;
        tp[0x36001] = 0xBD;
        tp[0x36017] = 1;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(tp, 0x36FFC);
        string a = workspace.Write("a.bin", tp);
        tp[0x2220C] = secondFormat;
        string b = workspace.Write("b.bin", tp);
        await viewModel.WorkflowSession.SetSlotFileAsync("tp-a-input", a, TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync("tp-b-input", b, TestContext.Current.CancellationToken);
        FirmwareSlotViewModel slot = viewModel.Merge.AbMergeSlots.Single(static slot => slot.SlotId == "tp-a-input");
        if (initializeConfiguration)
        {
            Assert.Equal("nt51950-ab-desay-maps", Assert.Single(slot.CurrentInspectionProjection!.InputSlotCatalog!.Routes).ExactCapability!.Identity.MapVariant);
        }
        return viewModel;
    }

    /// <summary>Saving first-install Config must reevaluate already-read inputs without reopening their paths.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EventBufferFormatFirstSaveReevaluatesInputsPreviouslyBlockedByMissingConfig(bool invalidConfig)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ui-ab-first-config-save");
        if (invalidConfig) { _ = workspace.Write("format.json", "invalid"u8.ToArray()); }
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        MainWindowViewModel viewModel = await CreateLoadedFormatAbViewModelAsync(workspace, host, initializeConfiguration: false);
        FirmwareSlotViewModel slot = viewModel.Merge.AbMergeSlots.Single(static slot => slot.SlotId == "tp-a-input");
        Assert.Equal(invalidConfig, slot.BlocksBuild);
        if (invalidConfig) { Assert.Null(slot.CurrentInspectionProjection!.InputSlotStatus?.AcceptedBytes); }
        else { _ = Assert.NotNull(slot.CurrentInspectionProjection!.InputSlotStatus?.AcceptedBytes); }
        File.Delete(workspace.PathFor("a.bin"));
        File.Delete(workspace.PathFor("b.bin"));
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;
        Assert.Equal(invalidConfig, viewModel.Settings.IsEventBufferFormatMissingOrInvalid);
        await viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);
        Assert.False(slot.BlocksBuild);
        Assert.Equal("nt51950-ab-desay-maps", Assert.Single(slot.CurrentInspectionProjection!.InputSlotCatalog!.Routes).ExactCapability!.Identity.MapVariant);
        _ = Assert.NotNull(slot.CurrentInspectionProjection.InputSlotStatus!.AcceptedBytes);
    }

    /// <summary>Saving while a real execution result awaits UI publication must not reset that run or adopt a newer map into its report.</summary>
    [Fact]
    public async Task EventBufferFormatSavePreservesActiveRunUntilResultPublication()
    {
        using TempWorkspace workspace = TempWorkspace.Create("ui-ab-running-save");
        var environment = new ExternalProcessorEnvironmentLoader(RepositoryPaths.FromRepositoryRoot("external-tools"));
        Assert.True((await ((NvtFwCombiner.Application.ExternalTools.IExternalProcessorEnvironmentLoader)environment)
            .LoadToCompletionAsync(null, TestContext.Current.CancellationToken)).Succeeded);
        CompositionHostServices host = CompositionHostServices.Create(environment,
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        var execution = new HeldAbResultPublication(host.CompositionExecution);
        MainWindowViewModel viewModel = await CreateLoadedFormatAbViewModelAsync(workspace, host, execution: execution);
        Task running = viewModel.Merge.PreviewMergeCommand.ExecuteAsync(null);
        try
        {
            await execution.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            UiRunResultViewModel previous = viewModel.RunSession.LastRunResult;
            Assert.True(viewModel.RunSession.IsRunInProgress);
            viewModel.OpenSettingsCommand.Execute(null);
            Assert.True(viewModel.IsSettingsModalOpen);
            viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
            await viewModel.Settings.EventBufferFormatLoadTask;
            Assert.Single(viewModel.Settings.EventBufferFormatRows).RecognitionValues.Clear();
            await viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);
            Assert.Equal(viewModel.Text.EventBufferFormatSavedLabel, viewModel.Settings.EventBufferFormatStatus);
            Assert.True(viewModel.RunSession.IsRunInProgress);
            Assert.Same(previous, viewModel.RunSession.LastRunResult);
            FirmwareSlotViewModel slot = viewModel.Merge.AbMergeSlots.Single(static slot => slot.SlotId == "tp-a-input");
            Assert.Equal("nt51950-ab-merge-maps", Assert.Single(slot.CurrentInspectionProjection!.InputSlotCatalog!.Routes).ExactCapability!.Identity.MapVariant);
            Assert.Equal("nt51950-ab-desay-maps", execution.Result!.ResolvedCapability!.Identity.MapVariant);
            execution.Release.SetResult();
            await running;
            Assert.False(viewModel.RunSession.IsRunInProgress);
            Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        }
        finally
        {
            _ = execution.Release.TrySetResult();
            await running;
        }
    }

    private sealed class HeldAbResultPublication(ICompositionExecution inner) : ICompositionExecution
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CompositionRunResult? Result { get; private set; }

        public async ValueTask<CompositionRunResult> ExecuteAsync(AcceptedCompositionExecutionRequest request,
            CompositionRunProgressFeed progress, CancellationToken cancellationToken)
        {
            Result = await inner.ExecuteAsync(request, progress, cancellationToken);
            Entered.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return Result;
        }
    }

    /// <summary>Progress publication may trigger a reapply, but the queued run must keep its already accepted request.</summary>
    [Fact]
    public async Task EventBufferFormatRunCapturesReadinessBeforePublishingProgress()
    {
        using TempWorkspace workspace = TempWorkspace.Create("ui-ab-run-capture");
        var environment = new ExternalProcessorEnvironmentLoader(RepositoryPaths.FromRepositoryRoot("external-tools"));
        Assert.True((await ((NvtFwCombiner.Application.ExternalTools.IExternalProcessorEnvironmentLoader)environment)
            .LoadToCompletionAsync(null, TestContext.Current.CancellationToken)).Succeeded);
        CompositionHostServices host = CompositionHostServices.Create(environment,
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int holdNext = 0;
        var authoring = new AbMergeAuthoringExperience(host.Compiler, host.Catalog, environment, async token =>
        {
            if (Interlocked.Exchange(ref holdNext, 0) == 1)
            {
                entered.SetResult();
                await resume.Task.WaitAsync(token);
            }
            return await host.GetEventBufferFormatConfigurationAsync(token);
        });
        MainWindowViewModel viewModel = await CreateLoadedFormatAbViewModelAsync(workspace, host, authoring);
        Task<bool>? reapply = null;
        bool refreshStarted = false;
        viewModel.RunSession.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CompositionRunPresentationViewModel.IsRunInProgress) &&
                viewModel.RunSession.IsRunInProgress && !refreshStarted)
            {
                refreshStarted = true;
                Volatile.Write(ref holdNext, 1);
                reapply = viewModel.Merge.ReapplyAbMergeConfigurationAsync();
            }
        };
        try
        {
            await viewModel.Merge.PreviewMergeCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.True(entered.Task.IsCompleted);
            Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        }
        finally
        {
            _ = resume.TrySetResult();
            if (reapply is not null) { _ = await reapply; }
        }
    }

    /// <summary>Neither delayed success nor delayed failure may overwrite a more recently inspected file.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task EventBufferFormatDelayedSaveCannotOverwriteNewInput(bool oldFormatFails, bool preCompilation)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ui-ab-config-race");
        var environment = new ExternalProcessorEnvironmentLoader();
        CompositionHostServices host = CompositionHostServices.Create(environment,
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int holdNext = 0;
        var authoring = new AbMergeAuthoringExperience(host.Compiler, host.Catalog, environment, async token =>
        {
            if (Interlocked.Exchange(ref holdNext, 0) == 1)
            {
                entered.SetResult();
                await resume.Task.WaitAsync(token);
            }
            return await host.GetEventBufferFormatConfigurationAsync(token);
        });
        MainWindowViewModel viewModel = await CreateLoadedFormatAbViewModelAsync(workspace, host, authoring,
            oldFormatFails ? (byte)0xA6 : (byte)0x97, initializeConfiguration: !preCompilation);
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;
        Assert.Single(viewModel.Settings.EventBufferFormatRows).RecognitionValues.Clear();
        if (oldFormatFails) { Assert.Single(viewModel.Settings.EventBufferFormatRows).RecognitionValues.Add(0xA6); }
        Volatile.Write(ref holdNext, 1);
        Task save = viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);
        try
        {
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            FirmwareSlotViewModel pendingSlot = viewModel.Merge.AbMergeSlots.Single(static slot => slot.SlotId == "tp-a-input");
            Assert.Null(pendingSlot.CurrentInspectionProjection!.AbMergeFacts!.EventBufferFormat);
            Assert.DoesNotContain(pendingSlot.FirmwareFacts, fact => fact.Label == viewModel.Text.EventBufferVersionLabel);
            if (!preCompilation) { Assert.Contains(pendingSlot.FirmwareFacts, static fact => fact.Label == "TPA"); }
            byte[] replacement = await File.ReadAllBytesAsync(workspace.PathFor("b.bin"), TestContext.Current.CancellationToken);
            string replacementPath = workspace.Write("replacement-a.bin", replacement);
            await viewModel.WorkflowSession.SetSlotFileAsync("tp-a-input", replacementPath, TestContext.Current.CancellationToken);
            FirmwareSlotViewModel slot = viewModel.Merge.AbMergeSlots.Single(static slot => slot.SlotId == "tp-a-input");
            FirmwareInspectionSnapshot accepted = slot.CurrentInspectionProjection!;
            Assert.False(slot.BlocksBuild);
            resume.SetResult();
            await save;
            Assert.Same(accepted, slot.CurrentInspectionProjection);
            Assert.Equal(replacementPath, slot.FilePath);
            Assert.False(slot.BlocksBuild);
            Assert.Empty(slot.CurrentInspectionProjection!.AuthoringCompilationIssues);
            Assert.Contains(slot.FirmwareFacts, fact => fact.Label == viewModel.Text.EventBufferVersionLabel);
        }
        finally
        {
            _ = resume.TrySetResult();
            await save;
        }
    }

    /// <summary>Runtime refresh errors do not undo persistence or become a false save failure after language changes.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EventBufferFormatRefreshFailureKeepsSavedConfigurationAndLocalizedStatus(bool throws)
    {
        using var session = new EventBufferFormatConfigurationSession(
            "event-buffer-format", [new("desay", "Desay")], [new("desay", null, [0x97])], new EventBufferFormatStorage());
        MainWindowViewModel viewModel = CreateEventBufferFormatViewModel(session);
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;
        viewModel.Settings.ReapplyEventBufferFormatAsync = () => throws
            ? Task.FromException<bool>(new InvalidOperationException("refresh failed")) : Task.FromResult(false);
        Assert.Single(viewModel.Settings.EventBufferFormatRows).AliasName = "Saved alias";
        await viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);
        Assert.Equal("Saved alias", Assert.Single(session.Current.Configuration!.Entries).AliasName);
        Assert.False(viewModel.Settings.HasEventBufferFormatUnsavedChanges);
        Assert.Equal(viewModel.Text.EventBufferFormatReapplyFailedLabel, viewModel.Settings.EventBufferFormatStatus);
        viewModel.SelectedLanguage = "Traditional Chinese";
        Assert.Equal(viewModel.Text.EventBufferFormatReapplyFailedLabel, viewModel.Settings.EventBufferFormatStatus);
        Assert.NotEqual(viewModel.Text.EventBufferFormatSaveFailedLabel, viewModel.Settings.EventBufferFormatStatus);
        viewModel.Settings.ReapplyEventBufferFormatAsync = () => Task.FromResult(true);
        Assert.Single(viewModel.Settings.EventBufferFormatRows).AliasName = "Retry";
        await viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);
        Assert.Equal(viewModel.Text.EventBufferFormatSavedLabel, viewModel.Settings.EventBufferFormatStatus);
    }

    /// <summary>The editor adopts the exact publication used by reapply instead of retaining an earlier read.</summary>
    [Fact]
    public async Task EventBufferFormatReloadAdoptsPublicationChangedDuringReapply()
    {
        var storage = new EventBufferFormatStorage
        {
            Stored = new(new(1, "event-buffer-format", [new("desay", "Publication A", ["0x97"])]), new string('a', 64)),
        };
        using var session = new EventBufferFormatConfigurationSession(
            "event-buffer-format", [new("desay", "Desay")], [new("desay", null, [0x97])], storage);
        MainWindowViewModel viewModel = CreateEventBufferFormatViewModel(session);
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;
        Assert.Equal("Publication A", Assert.Single(viewModel.Settings.EventBufferFormatRows).AliasName);
        viewModel.Settings.ReapplyEventBufferFormatAsync = async () =>
        {
            storage.Stored = new(new(1, "event-buffer-format", [new("desay", "Publication B", ["0xA6"])]), new string('b', 64));
            _ = await session.ReloadAsync(TestContext.Current.CancellationToken);
            return true;
        };

        await viewModel.Settings.ReloadEventBufferFormatCommand.ExecuteAsync(null);

        EventBufferFormatDraftRowViewModel row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
        Assert.Equal("Publication B", row.AliasName);
        Assert.Equal([0xA6], row.RecognitionValues);
        Assert.False(viewModel.Settings.HasEventBufferFormatUnsavedChanges);
        Assert.Equal(viewModel.Text.EventBufferFormatSavedLabel, viewModel.Settings.EventBufferFormatStatus);
    }

    /// <summary>Deleting a different saved override resets editor and Discard baseline without losing saved provenance.</summary>
    [Fact]
    public async Task EventBufferFormatDeletedOverrideRestoresBuiltInEditorAndDiscardBaseline()
    {
        var storage = new EventBufferFormatStorage();
        using var session = new EventBufferFormatConfigurationSession(
            "event-buffer-format", [new("desay", "Desay")], [new("desay", null, [0x97, 0xA6])], storage);
        Assert.True((await session.SaveAsync([new("desay", "Custom", [0x84])], TestContext.Current.CancellationToken)).Succeeded);
        MainWindowViewModel viewModel = CreateEventBufferFormatViewModel(session);
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;
        Assert.Equal("Custom", Assert.Single(viewModel.Settings.EventBufferFormatRows).AliasName);
        EventBufferFormatConfiguration saved = session.Current.LastSaved!;

        storage.Stored = null;
        await viewModel.Settings.ReloadEventBufferFormatCommand.ExecuteAsync(null);
        EventBufferFormatDraftRowViewModel row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
        Assert.Equal([0x97, 0xA6], row.RecognitionValues);
        Assert.True(string.IsNullOrEmpty(row.AliasName));
        Assert.False(viewModel.Settings.HasEventBufferFormatUnsavedChanges);
        row.AliasName = "Unsaved";
        viewModel.Settings.DiscardEventBufferFormatChangesCommand.Execute(null);
        row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
        Assert.True(string.IsNullOrEmpty(row.AliasName));
        Assert.Equal([0x97, 0xA6], row.RecognitionValues);
        Assert.True(session.Current.UsesBuiltInDefaults);
        Assert.Same(saved, session.Current.LastSaved);
        Assert.Null(storage.Stored);
        Assert.Equal(viewModel.Text.EventBufferFormatBuiltInLabel, viewModel.Settings.EventBufferFormatStatus);
    }

    /// <summary>Missing configuration opens independent defaults, then saves only through the typed session.</summary>
    [Fact]
    public async Task EventBufferFormatUsesDraftDefaultsAndPreservesUnsavedCloseConfirmation()
    {
        var storage = new EventBufferFormatStorage();
        using var session = new EventBufferFormatConfigurationSession(
            "event-buffer-format", [new("desay", "Desay"), new("other", "Other")], [new("desay", null, [0x97, 0xA6])], storage);
        MainWindowViewModel viewModel = CreateEventBufferFormatViewModel(session);

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;

        EventBufferFormatDraftRowViewModel row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
        Assert.Equal("desay", row.UniqueId);
        Assert.Equal([0x97, 0xA6], row.RecognitionValues);
        Assert.False(viewModel.Settings.IsEventBufferFormatMissingOrInvalid);
        Assert.False(viewModel.Settings.CanSaveEventBufferFormat);
        Assert.True(session.Current.UsesBuiltInDefaults);
        Assert.Equal(viewModel.Text.EventBufferFormatBuiltInLabel, viewModel.Settings.EventBufferFormatStatus);

        row.AliasName = "Desk display";
        await viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);

        Assert.Equal(EventBufferFormatConfigurationStatus.Ready, session.Current.Status);
        Assert.False(viewModel.Settings.HasEventBufferFormatUnsavedChanges);
        Assert.Equal("Desk display", Assert.Single(session.Current.Configuration!.Entries).AliasName);

        row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
        row.SelectedIdentity = row.IdentityChoices.Single(identity => identity.UniqueId == "other");
        await viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);
        Assert.Equal("other", Assert.Single(session.Current.Configuration!.Entries).UniqueId);

        row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
        row.AliasName = "Pending alias";
        viewModel.CloseSettingsCommand.Execute(null);

        Assert.True(viewModel.IsSettingsModalOpen);
        Assert.True(viewModel.Settings.IsEventBufferFormatCloseConfirmationOpen);
        viewModel.Settings.ConfirmEventBufferFormatCloseCommand.Execute(null);
        Assert.False(viewModel.IsSettingsModalOpen);
        Assert.Equal("Desk display", Assert.Single(viewModel.Settings.EventBufferFormatRows).AliasName);
    }

    /// <summary>Does not turn a valid empty saved configuration into unrequested catalog entries.</summary>
    [Fact]
    public async Task EventBufferFormatRetainsAnAdmittedEmptySavedDraftWithoutExpandingTheCatalog()
    {
        var storage = new EventBufferFormatStorage
        {
            Stored = new EventBufferFormatStoredConfiguration(
                new EventBufferFormatConfigurationDocument(1, "event-buffer-format", []), "empty-draft"),
        };
        using var session = new EventBufferFormatConfigurationSession(
            "event-buffer-format", [new("desay", "Desay")], [new("desay", null, [0x97])], storage);
        MainWindowViewModel viewModel = CreateEventBufferFormatViewModel(session);

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;

        Assert.Equal(EventBufferFormatConfigurationStatus.Ready, session.Current.Status);
        Assert.Empty(viewModel.Settings.EventBufferFormatRows);
        Assert.False(viewModel.Settings.HasEventBufferFormatUnsavedChanges);
    }

    /// <summary>A rejected write leaves both the editor draft and its prior admitted publication intact.</summary>
    [Fact]
    public async Task EventBufferFormatSaveFailureRetainsDraftAndReportsTheTypedPersistenceFailure()
    {
        var storage = new EventBufferFormatStorage
        {
            Stored = new EventBufferFormatStoredConfiguration(
                new EventBufferFormatConfigurationDocument(1, "event-buffer-format",
                    [new("desay", "Saved", ["0x97"])]), "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
            WriteError = new IOException("disk unavailable"),
        };
        using var session = new EventBufferFormatConfigurationSession(
            "event-buffer-format", [new("desay", "Desay")], [new("desay", null, [0x97])], storage);
        MainWindowViewModel viewModel = CreateEventBufferFormatViewModel(session);

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;
        EventBufferFormatDraftRowViewModel row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
        row.AliasName = "Unsaved";
        int refreshCalls = 0;
        viewModel.Settings.ReapplyEventBufferFormatAsync = () => { refreshCalls++; return Task.FromResult(true); };

        await viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);

        Assert.Equal("Saved", Assert.Single(session.Current.Configuration!.Entries).AliasName);
        Assert.Equal(0, refreshCalls);
        Assert.Equal("Unsaved", Assert.Single(viewModel.Settings.EventBufferFormatRows).AliasName);
        Assert.Equal(viewModel.Text.EventBufferFormatSaveFailedLabel, viewModel.Settings.EventBufferFormatStatus);

        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.Equal(viewModel.Text.EventBufferFormatSaveFailedLabel, viewModel.Settings.EventBufferFormatStatus);
        Assert.NotEqual(viewModel.Text.EventBufferFormatSavedLabel, viewModel.Settings.EventBufferFormatStatus);
    }

    /// <summary>Save serialisation blocks destructive commands and leaves lexical versus semantic validation distinct.</summary>
    [Fact]
    public async Task EventBufferFormatSaveBusyAndValueFeedbackAreGuarded()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storage = new EventBufferFormatStorage { WriteHold = hold };
        using var session = new EventBufferFormatConfigurationSession(
            "event-buffer-format", [new("desay", "Desay")], [new("desay", null, [0x97])], storage);
        MainWindowViewModel viewModel = CreateEventBufferFormatViewModel(session);

        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;
        EventBufferFormatDraftRowViewModel row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
        row.RecognitionValueDraft = "wrong";
        row.AddRecognitionValueCommand.Execute(null);
        Assert.Equal(viewModel.Text.EventBufferFormatInvalidHexLabel, row.RecognitionValueValidationMessage);

        row.AliasName = "pending";
        Task save = viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);
        await Task.Yield();

        try
        {
            Assert.True(viewModel.Settings.IsEventBufferFormatBusy);
            Assert.False(viewModel.Settings.CanRestoreEventBufferFormat);
            Assert.False(viewModel.Settings.CanDiscardEventBufferFormat);
            Assert.False(viewModel.Settings.RequestSettingsClose());
            viewModel.Settings.RestoreEventBufferFormatDefaultsCommand.Execute(null);
            Assert.Equal([0x97], row.RecognitionValues);
            Assert.Equal("pending", row.AliasName);
        }
        finally
        {
            hold.SetResult();
        }
        await save;

        row = Assert.Single(viewModel.Settings.EventBufferFormatRows);
        row.BeginAddRecognitionValueCommand.Execute(null);
        row.RecognitionValueDraft = "0x97";
        row.AddRecognitionValueCommand.Execute(null);
        await viewModel.Settings.SaveEventBufferFormatCommand.ExecuteAsync(null);

        Assert.Contains("0x97", viewModel.Settings.EventBufferFormatStatus, StringComparison.Ordinal);
    }

    /// <summary>Reload failures remain actionable and localized without activating recovery defaults.</summary>
    [Theory]
    [InlineData(EventBufferFormatConfigurationFailure.InvalidValues, "already assigned", "其他識別碼")]
    [InlineData(EventBufferFormatConfigurationFailure.ScopeMismatch, "different configuration group", "不同設定群組")]
    [InlineData(EventBufferFormatConfigurationFailure.ReadFailed, "Cannot read", "無法讀取")]
    public async Task EventBufferFormatReloadPreservesSpecificFailureAcrossLanguageChange(
        EventBufferFormatConfigurationFailure failure, string englishDetail, string chineseDetail)
    {
        var storage = new EventBufferFormatStorage
        {
            Stored = new(new(1, failure == EventBufferFormatConfigurationFailure.ScopeMismatch ? "foreign-scope" : "event-buffer-format",
                [new("desay", null, ["0x97"]), new("other", null, ["0x97"])]), new string('b', 64)),
            ReadError = failure == EventBufferFormatConfigurationFailure.ReadFailed ? new IOException("unreadable") : null,
        };
        using var session = new EventBufferFormatConfigurationSession(
            "event-buffer-format", [new("desay", "Desay"), new("other", "Other")], [new("desay", null, [0x97])], storage);
        MainWindowViewModel viewModel = CreateEventBufferFormatViewModel(session);
        viewModel.OpenSettingsCommand.Execute(null);
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.EventBufferFormat);
        await viewModel.Settings.EventBufferFormatLoadTask;
        Assert.Equal(EventBufferFormatConfigurationStatus.Invalid, session.Current.Status);
        Assert.Null(session.Current.Configuration);
        Assert.Contains(englishDetail, viewModel.Settings.EventBufferFormatStatus, StringComparison.Ordinal);
        viewModel.SelectedLanguage = "Traditional Chinese";
        Assert.Contains(chineseDetail, viewModel.Settings.EventBufferFormatStatus, StringComparison.Ordinal);
        Assert.Equal([0x97], Assert.Single(viewModel.Settings.EventBufferFormatRows).RecognitionValues);
    }

    private static MainWindowViewModel CreateEventBufferFormatViewModel(
        IEventBufferFormatConfigurationSession session)
    {
        PresentationHostServices original = PresentationTestHost.CreateServices(
            ApplicationVersionProvider.InformationalVersion);
        var services = new PresentationHostServices(
            original.Composition,
            original.FileReveal,
            original.SupportMatrix,
            original.SystemInformation,
            original.SystemDiagnosticsExporter,
            original.RawBinaryEditorFileSessions,
            original.CanonicalCatalogLoader,
            original.ExternalEnvironmentLoader,
            original.LocalFiles,
            versionManagement: null,
            managedApplicationStartup: null,
            stableLauncherHandoff: null,
            eventBufferFormatConfigurationSessionFactory: _ => Task.FromResult(session));
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(services, ShellLanguage.English);
        return PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
    }

    private sealed class EventBufferFormatStorage : IEventBufferFormatConfigurationStorage
    {
        internal EventBufferFormatStoredConfiguration? Stored { get; set; }
        internal Exception? WriteError { get; set; }
        internal Exception? ReadError { get; set; }
        internal TaskCompletionSource? WriteHold { get; set; }
        internal TaskCompletionSource? ReadHold { get; set; }

        public async ValueTask<EventBufferFormatStoredConfiguration?> ReadAsync(CancellationToken cancellationToken)
        {
            if (ReadHold is { } hold)
            {
                await hold.Task.WaitAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return ReadError is { } error ? throw error : Stored;
        }

        public async ValueTask<string> WriteAsync(
            EventBufferFormatConfigurationDocument document,
            CancellationToken cancellationToken)
        {
            if (WriteHold is { } hold)
            {
                await hold.Task.WaitAsync(cancellationToken);
            }

            if (WriteError is { } error)
            {
                throw error;
            }

            cancellationToken.ThrowIfCancellationRequested();
            Stored = new EventBufferFormatStoredConfiguration(document, new string('a', 64));
            return Stored.SourceSha256;
        }
    }
}
