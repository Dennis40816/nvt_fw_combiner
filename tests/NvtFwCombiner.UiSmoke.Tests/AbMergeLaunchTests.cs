using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Desktop AB arguments select ordinary inspected inputs without executing firmware composition.</summary>
public sealed class AbMergeLaunchTests
{
    private static string Fixture(string name)
    {
        return RepositoryPaths.FromRepositoryRoot(
            "testdata/golden/canonical/NT51950/ab-merge/boe-d82t80/topology-unscoped/nt51950-ab-boe-d82t80/inputs/" + name);
    }

    private static string Dp => Fixture("NT51950TT_Initial Code_BOE_AS172QD0-B00 2560x1600_BOE only_PD fixed pixelonoff_D82_20260616.bin");
    private static string Tp => Fixture("nt51950_fw_T80.bin");
    private static string[] Arguments => ["--workflow", "ab-merge", "--ic", "NT51950",
        "--ic-num", "single", "--dp", Dp, "--tp-a", Tp, "--tp-b", Tp];

    /// <summary>Both separate and inline options support paths containing spaces and equals signs.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitAbInputsSelectMerge(bool inline)
    {
        string[] arguments = inline
            ? ["--workflow=ab-merge", "--ic=NT51950", "--ic-num=single", "--dp=folder with spaces/dp=1.bin",
                "--tp-a=a.bin", "--tp-b=b.bin"] : Arguments;
        UiLaunchOptions options = UiLaunchOptions.Parse(arguments);
        Assert.Empty(options.Issues);
        Assert.Equal(ShellPage.Merge, options.Page);
        Assert.Null(options.CtrlRam);
        AbMergeLaunchRequest request = Assert.IsType<AbMergeLaunchRequest>(options.AbMerge);
        Assert.Equal(inline ? Path.GetFullPath("folder with spaces/dp=1.bin") : Dp, request.DpPath);
        Assert.Equal(inline ? Path.GetFullPath("a.bin") : Tp, request.TpAPath);
        Assert.Equal(inline ? Path.GetFullPath("b.bin") : Tp, request.TpBPath);
    }

    /// <summary>Partial or conflicting requests cannot fall back to another input workflow.</summary>
    [Theory]
    [InlineData("--ic", "NT51951")]
    [InlineData("--workflow", "ctrlram-replace")]
    [InlineData("--dp", "other.bin")]
    [InlineData("--tp-a", "other.bin")]
    [InlineData("--tp-b", "other.bin")]
    [InlineData("--base", "base.bin")]
    [InlineData("--ctrlram", "replace-ctrlram-nf=nf.bin")]
    [InlineData("--page", "replace")]
    [InlineData("--page", "settings")]
    [InlineData("--report", "run.json")]
    [InlineData("--open-report", "")]
    [InlineData("--build", "true")]
    public void ConflictingInputsReportIssues(string option, string value)
    {
        UiLaunchOptions options = UiLaunchOptions.Parse([.. Arguments, option, value]);
        Assert.NotEmpty(options.Issues);
        Assert.Null(options.CtrlRam);
        Assert.Null(options.AbMerge);
    }

    /// <summary>All context and file options are explicit; none is guessed.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    public void MissingOptionReportsIssues(int offset)
    {
        string[] args = Arguments;
        UiLaunchOptions options = UiLaunchOptions.Parse([.. args.Take(offset), .. args.Skip(offset + 2)]);
        Assert.NotEmpty(options.Issues);
        Assert.False(options.HasStartupInputs);
    }

    /// <summary>A later matching page cannot hide an earlier conflict or a duplicate page.</summary>
    [Theory]
    [InlineData("replace")]
    [InlineData("merge")]
    public void RepeatedPageCannotOverrideInputStartup(string firstPage)
    {
        UiLaunchOptions options = UiLaunchOptions.Parse([.. Arguments, "--page", firstPage, "--page", "merge"]);
        Assert.NotEmpty(options.Issues);
        Assert.False(options.HasStartupInputs);
    }

    /// <summary>Actual startup traverses context selection and inspection, never Preview or Build.</summary>
    [AvaloniaFact]
    public async Task CanonicalAbInputsLoadThroughRealStartupWithoutRunning()
    {
        using var workspace = TempWorkspace.Create("ab-launch");
        PresentationHostServices isolated = await CreateServicesAsync(workspace);
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        PresentationHostServices services = PresentationTestHost.CreateServices("ui-smoke", host, static authoring => authoring);
        var execution = new NoRunExecution();
        PresentationCompositionServices original = services.Composition;
        services = new(new(original.Capabilities, original.StandardMergeAuthoring, original.AbMergeAuthoring,
            original.GeneralAuthoring, original.CtrlRamAuthoring,
            original.FirmwareInspection, original.OutputNaming, execution), services.FileReveal, services.SupportMatrix,
            services.SystemInformation, services.SystemDiagnosticsExporter, services.RawBinaryEditorFileSessions,
            services.CanonicalCatalogLoader, services.ExternalEnvironmentLoader, isolated.LocalFiles);
        using var window = new MainWindow(UiLaunchOptions.Parse(Arguments), StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            Assert.Equal(ShellPage.Merge, shell.SelectedPage);
            Assert.Equal("NT51950", shell.WorkflowSession.SelectedIc);
            Assert.Equal("single", shell.WorkflowSession.SelectedNumber);
            Assert.Equal(ExperienceIds.AbMerge, shell.Merge.SelectedMergeMode);
            Assert.False(shell.Merge.UseDummyDpForAbMerge);
            Assert.False(shell.Merge.UseSameTpForAbMerge);
            Assert.Equal([Dp, Tp, Tp], new[] { CompositionAddressSpaceIds.DpAbInput,
                CompositionAddressSpaceIds.TpAInput, CompositionAddressSpaceIds.TpBInput }
                .Select(id => shell.Merge.AbMergeSlotsByAddressSpace[id].FilePath));
            Assert.All(shell.Merge.AbMergeSlots, slot => Assert.NotNull(slot.CurrentInspectionProjection));
            foreach (string id in new[] { CompositionAddressSpaceIds.TpAInput, CompositionAddressSpaceIds.TpBInput })
            {
                Assert.True(shell.Merge.AbMergeSlotsByAddressSpace[id].CurrentInspectionProjection!.InputSlotStatus!.IsTerminal);
                Assert.False(shell.Merge.AbMergeSlotsByAddressSpace[id].BlocksBuild);
            }
            Assert.True(shell.Merge.AbMergeSlotsByAddressSpace[CompositionAddressSpaceIds.DpAbInput].IsInputInspectionValid);
            Assert.Empty(shell.Reports.ReportHistoryEntries);
            Assert.False(shell.Reports.HasLoadedReport);
            Assert.Equal(0, execution.Calls);
            Assert.True(window.FindControl<Grid>("ShellInteractionHost")!.IsEnabled);
        }
        finally { await CloseAndFlushAsync(window); }
    }

    /// <summary>Read failure stops immediately; an undersized DP stays blocked after pair admission.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidDpRetainsTheExistingInputFailure(bool truncated)
    {
        using var workspace = TempWorkspace.Create("ab-launch-bad-input");
        MainWindowViewModel shell = CreateShell(workspace);
        string path = truncated ? workspace.Write("short.bin", new byte[8]) : workspace.PathFor("missing.bin");
        AbMergeLaunchRequest request = UiLaunchOptions.Parse(Arguments).AbMerge! with { DpPath = path };
        await MainWindow.ApplyAbMergeLaunchAsync(shell, request, TestContext.Current.CancellationToken);
        Assert.Equal(path, shell.Merge.AbMergeSlotsByAddressSpace[CompositionAddressSpaceIds.DpAbInput].FilePath);
        Assert.True(shell.Merge.AbMergeSlotsByAddressSpace[CompositionAddressSpaceIds.DpAbInput].BlocksBuild);
        Assert.Equal(truncated, shell.Merge.AbMergeSlotsByAddressSpace[CompositionAddressSpaceIds.TpAInput].HasFile);
        Assert.Equal(truncated, shell.Merge.AbMergeSlotsByAddressSpace[CompositionAddressSpaceIds.TpBInput].HasFile);
        if (truncated)
        {
            Assert.False(shell.Merge.CanBuildMerge);
        }
        Assert.False(shell.Reports.HasLoadedReport);
    }

    /// <summary>Invalid custom configuration retains the selected source and the shared configuration blocker.</summary>
    [Fact]
    public async Task InvalidConfigurationStopsAutomaticSelection()
    {
        using var workspace = TempWorkspace.Create("ab-launch-bad-config");
        _ = workspace.Write("format.json", "invalid"u8.ToArray());
        MainWindowViewModel shell = CreateShell(workspace);
        await MainWindow.ApplyAbMergeLaunchAsync(shell, UiLaunchOptions.Parse(Arguments).AbMerge!,
            TestContext.Current.CancellationToken);
        Assert.Contains(shell.Merge.AbMergeSlots,
            slot => slot.CurrentInspectionProjection?.InputSlotStatus?.ConfigurationBlocker is not null);
        Assert.Contains(shell.Merge.AbMergeSlots, slot => !slot.HasFile);
        Assert.False(shell.Reports.HasLoadedReport);
    }

    /// <summary>Explicit context mismatch keeps the existing confirmation and stops remaining selections.</summary>
    [Fact]
    public async Task IcMismatchRequiresUserDecision()
    {
        using var workspace = TempWorkspace.Create("ab-launch-mismatch");
        MainWindowViewModel shell = CreateShell(workspace);
        string mismatch = workspace.Write("NT51919_flash.bin", File.ReadAllBytes(Dp));
        AbMergeLaunchRequest request = UiLaunchOptions.Parse(Arguments).AbMerge! with { DpPath = mismatch };
        await MainWindow.ApplyAbMergeLaunchAsync(shell, request, TestContext.Current.CancellationToken);
        Assert.True(shell.WorkflowSession.IsFirmwareIcMismatchModalOpen);
        Assert.Equal("NT51950", shell.WorkflowSession.SelectedIc);
        Assert.False(shell.Merge.AbMergeSlotsByAddressSpace[CompositionAddressSpaceIds.TpBInput].HasFile);
    }

    /// <summary>Unavailable context, cancellation and an occupied session cannot silently choose defaults.</summary>
    [Theory]
    [InlineData("ic")]
    [InlineData("number")]
    [InlineData("cancel")]
    [InlineData("occupied")]
    public async Task UnavailableStartupDoesNotSelectFiles(string reason)
    {
        using var workspace = TempWorkspace.Create("ab-launch-context");
        MainWindowViewModel shell = CreateShell(workspace);
        AbMergeLaunchRequest request = UiLaunchOptions.Parse(Arguments).AbMerge!;
        using var cancellation = new CancellationTokenSource();
        if (reason == "ic") { request = request with { IcId = "unavailable" }; }
        if (reason == "number") { request = request with { Number = "unavailable" }; }
        if (reason == "occupied") { shell.ShowMergeCommand.Execute(null); }
        if (reason == "cancel")
        {
            await cancellation.CancelAsync();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                MainWindow.ApplyAbMergeLaunchAsync(shell, request, cancellation.Token));
        }
        else
        {
            _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                MainWindow.ApplyAbMergeLaunchAsync(shell, request, cancellation.Token));
        }
        Assert.False(shell.HasSelectedFiles);
        Assert.False(shell.WorkflowSession.IsWorkflowContextModalOpen);
    }

    private static MainWindowViewModel CreateShell(TempWorkspace workspace)
    {
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        PresentationHostServices services = PresentationTestHost.CreateServices("ui-smoke", host, static authoring => authoring);
        return PresentationTestHost.PublishCanonicalCatalog(services, ShellViewModelFactory.Create(services, ShellLanguage.English));
    }

    private sealed class NoRunExecution : ICompositionExecution
    {
        internal int Calls { get; private set; }
        public ValueTask<CompositionRunResult> ExecuteAsync(AcceptedCompositionExecutionRequest request,
            CompositionRunProgressFeed progress, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("Startup must not execute Preview or Build.");
        }
    }
}
