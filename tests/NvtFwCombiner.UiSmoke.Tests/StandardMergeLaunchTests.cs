using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Desktop Standard Merge startup selects canonical files through the ordinary inspection owner.</summary>
public sealed class StandardMergeLaunchTests
{
    private const string Case = "testdata/golden/canonical/NT51926/standard-merge/gen-flash/" +
        "topology-unscoped/nt51926-gen-flash/inputs/";
    private static string Dp => RepositoryPaths.FromRepositoryRoot(Case + "nt51926-dp-input.bin");
    private static string Tp => RepositoryPaths.FromRepositoryRoot(Case + "nt51926-tp-input.bin");
    private static string[] Arguments => ["--workflow", "standard-merge", "--ic", "NT51926",
        "--ic-num", "single", "--dp", Dp, "--tp", Tp];

    /// <summary>Explicit Standard Merge arguments select only the matching request.</summary>
    [Fact]
    public void ExplicitInputsSelectStandardMerge()
    {
        UiLaunchOptions options = UiLaunchOptions.Parse(Arguments);
        Assert.Empty(options.Issues);
        Assert.Equal(ShellPage.Merge, options.Page);
        Assert.Equal(new StandardMergeLaunchRequest("NT51926", "single", Dp, Tp), options.StandardMerge);
        Assert.Null(options.AbMerge);
        Assert.Null(options.CtrlRam);
    }

    /// <summary>Conflicting workflows, pages and automatic Build are rejected.</summary>
    [Theory]
    [InlineData("--tp-a", "other.bin")]
    [InlineData("--base", "other.bin")]
    [InlineData("--page", "replace")]
    [InlineData("--build", "true")]
    public void ConflictingInputsNeverProduceRequest(string option, string value)
    {
        UiLaunchOptions options = UiLaunchOptions.Parse([.. Arguments, option, value]);
        Assert.NotEmpty(options.Issues);
        Assert.False(options.HasStartupInputs);
    }

    /// <summary>An incomplete command cannot silently use a default TP file.</summary>
    [Fact]
    public void MissingTpNeverProducesRequest()
    {
        UiLaunchOptions options = UiLaunchOptions.Parse(Arguments[..^2]);
        Assert.NotEmpty(options.Issues);
        Assert.False(options.HasStartupInputs);
    }

    /// <summary>The real window loads and inspects both files before any run.</summary>
    [AvaloniaFact]
    public async Task RealStartupLoadsCanonicalFilesWithoutRunning()
    {
        using var workspace = TempWorkspace.Create("standard-merge-launch");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Parse(Arguments), StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default);
        MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            Assert.Equal(ShellPage.Merge, shell.SelectedPage);
            Assert.Equal("NT51926", shell.WorkflowSession.SelectedIc);
            Assert.Equal(ExperienceIds.StandardMerge, shell.Merge.SelectedMergeMode);
            Assert.Equal(Dp, shell.Merge.MergeDpSlot.FilePath);
            Assert.Equal(Tp, shell.Merge.MergeTpSlot.FilePath);
            Assert.NotNull(shell.Merge.MergeDpSlot.CurrentInspectionProjection);
            Assert.NotNull(shell.Merge.MergeTpSlot.CurrentInspectionProjection);
            Assert.False(shell.Reports.HasLoadedReport);
            Assert.True(window.FindControl<Grid>("ShellInteractionHost")!.IsEnabled);
        }
        finally { await CloseAndFlushAsync(window); }
    }
}
