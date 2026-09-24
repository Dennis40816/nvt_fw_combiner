using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
            Capture(window, ic + "-cascade");
            window.Width = 980;
            Render();
            MemoryCoverageBar rail = Assert.Single(window.GetVisualDescendants().OfType<MemoryCoverageBar>(), bar => bar.IsEffectivelyVisible);
            TextBlock[] labels = [.. rail.GetVisualDescendants().OfType<TextBlock>().Where(label => label.Classes.Contains("memoryPositionLabel"))];
            Assert.Contains(labels, label => label.Text == "Cascade");
            foreach (TextBlock label in labels)
            {
                Assert.True(label.Bounds.Width >= label.TextLayout.Width - 1,
                    $"{ic} {label.Text}: allocated {label.Bounds.Width}, glyphs {label.TextLayout.Width}");
                double left = label.TranslatePoint(default, rail)!.Value.X;
                Assert.InRange(left, -1, rail.Bounds.Width - label.Bounds.Width + 1);
            }
            Rect[] labelBounds = [.. labels.Select(label => new Rect(label.TranslatePoint(default, window)!.Value, label.Bounds.Size))];
            for (int index = 1; index < labelBounds.Length; index++)
            {
                Assert.False(labelBounds[index - 1].Intersects(labelBounds[index]));
            }
            TextBlock cascadeLabel = Assert.Single(labels, label => label.Text == "Cascade");
            window.MouseMove(new Rect(cascadeLabel.TranslatePoint(default, window)!.Value, cascadeLabel.Bounds.Size).Center, RawInputModifiers.None);
            Render();
            Border local = Assert.Single(window.GetVisualDescendants().OfType<Border>(), border => border.Name == "MemoryLocalView");
            Assert.Contains(local.GetVisualDescendants().OfType<TextBlock>(), label => label.Text == "Cascade");
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), border => border.Name == "MemorySliceCard");
            Capture(window, ic + "-cascade-narrow-hover");
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

    internal static void Capture(Window window, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) { return; }
        for (int tick = 0; tick < 4; tick++) { Thread.Sleep(80); Render(); }
        _ = Directory.CreateDirectory(directory);
        using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, name + ".png"));
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
