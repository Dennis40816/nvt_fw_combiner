using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Real 950-family Cascade input inspection keeps physical display and Build readiness independent.</summary>
public sealed class CtrlRamCascadeMemoryLayoutTests
{
    /// <summary>The loaded Cascade view includes preserved Diff NF without reporting a Base error.</summary>
    [AvaloniaTheory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public async Task CascadeInputsProduceCompleteDiffLayoutAndRetainBuildReadiness(string ic)
    {
        using var workspace = TempWorkspace.Create("ctrlram-cascade-layout");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default)
        { Width = 1440, Height = 1040, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            await LoadCascadeAsync(shell, ic);
            foreach (FirmwareSlotGroupViewModel group in shell.Replace.ReplaceSlotGroups) { group.IsExpanded = true; }
            Render();
            Assert.Equal(WorkflowInspectionAttemptState.Succeeded, shell.Replace.Inspection.State);
            Assert.False(shell.Replace.ReplaceBaseSlot.IsSemanticStateError);
            Assert.True(shell.Replace.CanBuildReplace, shell.Replace.ReplaceBaseSlot.InputInspectionStatus);
            Assert.Equal(3, shell.Replace.ReplaceSlots.Count(slot => slot.SlotKind == FirmwareSlotKind.CtrlRam && slot.HasFile));
            CtrlRamRegionViewModel diff = Assert.Single(shell.Replace.CtrlRamRegions, region => region.IsDiffRegion);
            Assert.Equal("0x33200-0x345FF", diff.StartAddress);
            Assert.Equal("len 0x1400", diff.SizeHex);
            Assert.True(shell.Replace.HasCtrlRamFocusLayout);
            Assert.Contains(shell.Replace.CtrlRamFocusLanes, lane => lane.Title == "Common");
            Assert.Contains(shell.Replace.CtrlRamFocusLanes, lane => lane.Title == "Cascade");
            Assert.DoesNotContain(shell.Replace.CtrlRamFocusLanes, lane => lane.Title == "Master");
            Assert.NotEmpty(shell.Replace.ReplaceCoverageSegments);
            Assert.Empty(shell.Reports.ReportHistoryEntries);
            await CaptureAsync(window, ic + "-cascade");
            window.Width = 980;
            Render();
            OutputLayoutPanel panel = Assert.Single(window.GetVisualDescendants().OfType<OutputLayoutPanel>(),
                static control => control.IsEffectivelyVisible);
            OutputLayoutPanelModel model = Assert.IsType<OutputLayoutPanelModel>(shell.Replace.OutputLayout.Current.Panel);
            Assert.Same(model, panel.Model);
            Assert.Contains(model.LaneLinks, static link => link.Lane.Title == "Cascade" && link.Zoom is not null);
            Assert.Contains(model.RowSegments, static segment => segment.CtrlRamRegionRole == CtrlRamRegionRole.DiffDlm);
            Assert.DoesNotContain(window.GetVisualDescendants(),
                static control => control.Name is "MemoryLocalView" or "MemorySliceCard");
            await CaptureAsync(window, ic + "-cascade-narrow-inline");
        }
        finally { await CloseAndFlushAsync(window); }
    }

    internal static async Task LoadCascadeAsync(MainWindowViewModel shell, string ic)
    {
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", "nt51951-fw200-cascade2-auto-prj-599-20260731");
        string PathFor(string id)
        {
            return CanonicalGoldenTestData.ArtifactPath(fixture.GetProperty("artifacts").EnumerateArray()
                .Single(artifact => artifact.GetProperty("artifactId").GetString() == id));
        }
        var inputs = new Dictionary<string, string>
        {
            ["replace-ctrlram-normal"] = PathFor("normal-ctrlram-input"),
            ["replace-ctrlram-vn"] = PathFor("vn-ctrlram-input"),
            ["replace-ctrlram-diff"] = PathFor("diffdlm-input"),
        };
        // NT51950 has a declared TP-work geometry alias, not a 512-KiB FlashCode fixture.
        UiLaunchOptions options = UiLaunchOptions.Parse(["--workflow", "ctrlram-replace", "--ic", ic,
            "--ic-num", "cascade", "--base", PathFor(ic == "NT51950" ? "tp-firmware-input" : "expected-output"),
            "--ctrlram", "replace-ctrlram-normal=" + inputs["replace-ctrlram-normal"]]);
        Assert.Empty(options.Issues);
        await MainWindow.ApplyCtrlRamLaunchAsync(shell, options.CtrlRam!, TestContext.Current.CancellationToken);
        if (shell.WorkflowSession.IsFirmwareIcMismatchModalOpen)
        {
            Assert.Equal("NT51950", ic);
            shell.WorkflowSession.DismissFirmwareIcMismatchCommand.Execute(null);
        }
        foreach ((string slot, string path) in inputs)
        {
            if (!shell.Replace.ReplaceSlots.Single(candidate => candidate.SlotId == slot).HasFile)
            {
                await shell.WorkflowSession.SetSlotFileAsync(slot, path, TestContext.Current.CancellationToken);
            }
            if (shell.WorkflowSession.IsFirmwareIcMismatchModalOpen)
            {
                Assert.Equal("NT51950", ic);
                shell.WorkflowSession.DismissFirmwareIcMismatchCommand.Execute(null);
            }
        }
    }

    internal static async Task CaptureAsync(Window window, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) { return; }
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            Render();
            Dispatcher.UIThread.RunJobs();
            // Reveal and rail-lift animations must finish with valid layout before capture.
            string? pending = null;
            foreach (Control control in window.GetVisualDescendants().OfType<Control>().Prepend(window)
                .Where(control => control.IsEffectivelyVisible))
            {
                if (!control.IsMeasureValid || !control.IsArrangeValid)
                {
                    pending = $"layout is invalid for {control.GetType().Name}#{control.Name} (measure={control.IsMeasureValid}, arrange={control.IsArrangeValid})";
                    break;
                }
                if (control.IsAnimating(Visual.RenderTransformProperty) || control.IsAnimating(Visual.OpacityProperty))
                {
                    pending = $"animation is active for {control.GetType().Name}#{control.Name} (transform={control.RenderTransform?.Value}, opacity={control.Opacity})";
                    break;
                }
            }
            if (pending is null)
            {
                Render();
                break;
            }
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(10),
                $"Capture '{name}' did not settle within 10 seconds: {pending}");
            await Task.Delay(16, TestContext.Current.CancellationToken);
        }
        _ = Directory.CreateDirectory(directory);
        using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        frame.SavePng(Path.Combine(directory, name + ".png"));
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
