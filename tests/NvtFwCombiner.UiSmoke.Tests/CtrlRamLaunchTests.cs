using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Command-line input loading uses the real startup and Browse inspection owners, never a run.</summary>
public sealed class CtrlRamLaunchTests
{
    private static string FixturePath(string relative)
    {
        return RepositoryPaths.FromRepositoryRoot(
            "testdata/golden/canonical/NT51950/ctrlram-replace/fw2.0.0/single/" +
            "nt51950-fw200-single-auto-prj-676-20260717/" + relative);
    }

    private static string BasePath => FixturePath("expected/NT51950_Flashcode_BOE1540_Faurecia_Chery_D86T80_20260709.bin");
    private static string NfPath => FixturePath("inputs/postbuild/nt51950-postbuild-nf-ctrlram.bin");
    private static string[] Arguments => ["--workflow", "ctrlram-replace", "--ic", "NT51950",
        "--ic-num", "single", "--base", BasePath, "--ctrlram", "replace-ctrlram-nf=" + NfPath];

    /// <summary>The input workflow selects Replace without requiring an additional page option.</summary>
    [Fact]
    public void ExplicitCtrlRamInputsSelectReplacePage()
    {
        UiLaunchOptions options = UiLaunchOptions.Parse(Arguments);
        Assert.Empty(options.Issues);
        Assert.Equal(ShellPage.Replace, options.Page);
        CtrlRamLaunchRequest request = Assert.IsType<CtrlRamLaunchRequest>(options.CtrlRam);
        Assert.Equal("NT51950", request.IcId);
        Assert.Equal("single", request.Number);
        Assert.Equal(BasePath, request.BasePath);
        Assert.Equal(new CtrlRamLaunchInput("replace-ctrlram-nf", NfPath), Assert.Single(request.Inputs));
    }

    /// <summary>Repeated slots are supported; file paths retain spaces and equals signs.</summary>
    [Fact]
    public void InlineArgumentsRetainPathsAndMultipleDistinctSlots()
    {
        UiLaunchOptions options = UiLaunchOptions.Parse(["--workflow=ctrlram-replace", "--ic=NT51950",
            "--ic-num=single", "--base=folder with spaces/base.bin",
            "--ctrlram=replace-ctrlram-nf=folder with spaces/nf=1.bin", "--ctrlram=replace-ctrlram-vn=vn.bin"]);
        Assert.Empty(options.Issues);
        CtrlRamLaunchRequest request = Assert.IsType<CtrlRamLaunchRequest>(options.CtrlRam);
        Assert.Equal(Path.GetFullPath("folder with spaces/base.bin"), request.BasePath);
        Assert.Equal<CtrlRamLaunchInput>([new("replace-ctrlram-nf", Path.GetFullPath("folder with spaces/nf=1.bin")),
            new("replace-ctrlram-vn", Path.GetFullPath("vn.bin"))], request.Inputs);
    }

    /// <summary>Conflicts and unknown arguments fail closed for file-loading startup.</summary>
    [Theory]
    [InlineData("--ic", "NT51951")]
    [InlineData("--base", "another.bin")]
    [InlineData("--workflow", "ctrlram-replace")]
    [InlineData("--page", "merge")]
    [InlineData("--page", "settings")]
    [InlineData("--report", "run.json")]
    [InlineData("--ctrlram", "replace-ctrlram-nf=other.bin")]
    [InlineData("--ctrlram", "missing-assignment")]
    [InlineData("--ctrlram", "replace-ctrlram-vn=")]
    [InlineData("--ctrlram", "=vn.bin")]
    [InlineData("--ctrlram", " =vn.bin")]
    [InlineData("--build", "true")]
    public void ConflictingInputLaunchNeverProducesARequest(string option, string value)
    {
        UiLaunchOptions options = UiLaunchOptions.Parse([.. Arguments, option, value]);
        Assert.NotEmpty(options.Issues);
        Assert.Null(options.CtrlRam);
    }

    /// <summary>Partial command lines cannot silently load into a default context.</summary>
    [Theory]
    [InlineData("--workflow")]
    [InlineData("--workflow", "ctrlram-replace")]
    [InlineData("--base", "base.bin")]
    public void IncompleteInputLaunchReportsArgumentIssues(params string[] args)
    {
        Assert.NotEmpty(UiLaunchOptions.Parse(args).Issues);
    }

    /// <summary>The real window loads inputs and restores interaction without Preview or Build.</summary>
    [AvaloniaFact]
    public async Task RealStartupLoadsCtrlRamAndStopsBeforeAnyRun()
    {
        using var workspace = TempWorkspace.Create("ctrlram-launch");
        PresentationHostServices services = await CreateServicesAsync(workspace, useRetainedDpReplacePolicy: false);
        var execution = new NoRunExecution();
        PresentationCompositionServices original = services.Composition;
        services = new(new(original.Capabilities, original.StandardMergeAuthoring, original.AbMergeAuthoring,
            original.DpReplaceAuthoring, original.GeneralAuthoring, original.CtrlRamAuthoring,
            original.FirmwareInspection, original.OutputNaming, execution), services.FileReveal, services.SupportMatrix,
            services.SystemInformation, services.SystemDiagnosticsExporter, services.RawBinaryEditorFileSessions,
            services.CanonicalCatalogLoader, services.ExternalEnvironmentLoader, services.LocalFiles);
        using var window = new MainWindow(UiLaunchOptions.Parse(Arguments), StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            Assert.Equal(ShellPage.Replace, shell.SelectedPage);
            Assert.Equal("NT51950", shell.WorkflowSession.SelectedIc);
            Assert.Equal("single", shell.WorkflowSession.SelectedNumber);
            Assert.Equal(ExperienceIds.CtrlRamReplace, shell.Replace.SelectedReplaceMode);
            Assert.Equal(BasePath, shell.Replace.ReplaceBaseSlot.FilePath);
            FirmwareSlotViewModel nf = Assert.Single(shell.Replace.ReplaceSlots, s => s.SlotId == "replace-ctrlram-nf");
            Assert.Equal(NfPath, nf.FilePath);
            Assert.True(nf.IsSemanticStateVerified);
            Assert.NotEmpty(shell.Replace.ReplaceCoverageSegments);
            Assert.Empty(shell.Reports.ReportHistoryEntries);
            Assert.False(shell.Reports.HasLoadedReport);
            Assert.True(window.FindControl<Grid>("ShellInteractionHost")!.IsEnabled);
            Assert.Equal(0, execution.Calls);

            MainWindowViewModel manual = PresentationTestHost.CreateProductViewModel();
            manual.BeginCtrlRamReplaceFromHomeCommand.Execute(null);
            manual.WorkflowSession.WorkflowContextSetup.SelectedIc = "NT51950";
            manual.WorkflowSession.WorkflowContextSetup.SelectedNumber = "single";
            manual.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);
            await manual.WorkflowSession.SetSlotFileAsync("replace-base", BasePath, TestContext.Current.CancellationToken);
            await manual.WorkflowSession.SetSlotFileAsync("replace-ctrlram-nf", NfPath, TestContext.Current.CancellationToken);
            Assert.Equal(manual.Replace.ReplaceCoverageSegments.Select(s => (s.RangeLabel, s.LengthLabel, s.SourceLabel)),
                shell.Replace.ReplaceCoverageSegments.Select(s => (s.RangeLabel, s.LengthLabel, s.SourceLabel)));
        }
        finally { await CloseAndFlushAsync(window); }
    }

    /// <summary>Unsupported choices never fall back to a different IC or Number.</summary>
    [Theory]
    [InlineData("NT51952", "single")]
    [InlineData("NT51950", "99")]
    public async Task UnavailableContextDoesNotSelectAnyFiles(string ic, string number)
    {
        MainWindowViewModel shell = PresentationTestHost.CreateProductViewModel();
        CtrlRamLaunchRequest request = UiLaunchOptions.Parse(Arguments).CtrlRam! with { IcId = ic, Number = number };
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => MainWindow.ApplyCtrlRamLaunchAsync(
            shell, request, TestContext.Current.CancellationToken));
        Assert.False(shell.HasSelectedFiles);
        Assert.Equal(ShellPage.Home, shell.SelectedPage);
        Assert.False(shell.WorkflowSession.IsWorkflowContextModalOpen);
    }

    /// <summary>Every requested slot is validated before any replacement path is selected.</summary>
    [Theory]
    [InlineData("not-a-slot")]
    [InlineData("replace-base")]
    public async Task UnknownSlotDoesNotPartiallySelectReplacementInputs(string invalidSlot)
    {
        MainWindowViewModel shell = PresentationTestHost.CreateProductViewModel();
        CtrlRamLaunchRequest original = UiLaunchOptions.Parse(Arguments).CtrlRam!;
        CtrlRamLaunchRequest request = original with { Inputs = [.. original.Inputs, new(invalidSlot, NfPath)] };
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => MainWindow.ApplyCtrlRamLaunchAsync(
            shell, request, TestContext.Current.CancellationToken));
        Assert.Equal(BasePath, shell.Replace.ReplaceBaseSlot.FilePath);
        Assert.All(shell.Replace.ReplaceSlots.Where(slot => slot.SlotKind == FirmwareSlotKind.CtrlRam), slot => Assert.False(slot.HasFile));
    }

    /// <summary>A missing Base retains the existing diagnostic and does not attempt later selections.</summary>
    [Fact]
    public async Task MissingBaseStopsAtTheExistingInputError()
    {
        using var workspace = TempWorkspace.Create("ctrlram-launch-missing");
        MainWindowViewModel shell = PresentationTestHost.CreateProductViewModel();
        CtrlRamLaunchRequest request = UiLaunchOptions.Parse(Arguments).CtrlRam! with { BasePath = workspace.PathFor("missing.bin") };
        await MainWindow.ApplyCtrlRamLaunchAsync(shell, request, TestContext.Current.CancellationToken);
        Assert.True(shell.Replace.ReplaceBaseSlot.IsSemanticStateError);
        Assert.All(shell.Replace.ReplaceSlots.Where(slot => slot.SlotKind == FirmwareSlotKind.CtrlRam), slot => Assert.False(slot.HasFile));
        Assert.False(shell.Reports.HasLoadedReport);
    }

    /// <summary>A real Base number mismatch stays pending and prevents later CtrlRAM selection.</summary>
    [Fact]
    public async Task BaseNumberMismatchIsNotAcceptedAutomatically()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        MainWindowViewModel shell = PresentationTestHost.CreateProductViewModel();
        CtrlRamLaunchRequest request = UiLaunchOptions.Parse(Arguments).CtrlRam! with
        {
            IcId = "NT51926",
            BasePath = golden.ExpectedOutputPath(golden.CaseByIc("51926")),
            Inputs = [new("replace-ctrlram-vn", NfPath)],
        };
        await MainWindow.ApplyCtrlRamLaunchAsync(shell, request, TestContext.Current.CancellationToken);
        Assert.True(shell.WorkflowSession.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal("single", shell.WorkflowSession.SelectedNumber);
        Assert.All(shell.Replace.ReplaceSlots.Where(slot => slot.SlotKind == FirmwareSlotKind.CtrlRam),
            slot => Assert.False(slot.HasFile));
        Assert.False(shell.Reports.HasLoadedReport);
    }

    /// <summary>Cancellation or existing user work cannot trigger automatic input replacement.</summary>
    [Fact]
    public async Task CancelledOrOccupiedSessionDoesNotLoadInputs()
    {
        MainWindowViewModel shell = PresentationTestHost.CreateProductViewModel();
        CtrlRamLaunchRequest request = UiLaunchOptions.Parse(Arguments).CtrlRam!;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MainWindow.ApplyCtrlRamLaunchAsync(shell, request, cancellation.Token));
        Assert.False(shell.HasSelectedFiles);
        shell.ShowReplaceCommand.Execute(null);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => MainWindow.ApplyCtrlRamLaunchAsync(
            shell, request, TestContext.Current.CancellationToken));
        Assert.False(shell.HasSelectedFiles);
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
