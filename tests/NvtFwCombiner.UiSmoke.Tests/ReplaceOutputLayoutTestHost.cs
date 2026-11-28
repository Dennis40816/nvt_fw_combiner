// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises the real Replace page through its shared output-layout publication.</summary>
internal sealed class ReplaceOutputLayoutTestHost(MainWindow window) : IAsyncDisposable
{
    internal MainWindow Window { get; } = window;
    internal MainWindowViewModel Shell => Assert.IsType<MainWindowViewModel>(Window.DataContext);
    internal OutputLayoutPanel Panel => Assert.Single(Window.GetVisualDescendants().OfType<OutputLayoutPanel>(),
        static panel => panel.IsEffectivelyVisible);
    internal OutputLayoutPanelModel Model => Assert.IsType<OutputLayoutPanelModel>(Panel.Model);

    /// <summary>Opens the real Replace page after shared application startup.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "The returned host closes and disposes the window in DisposeAsync.")]
    internal static async Task<ReplaceOutputLayoutTestHost> CreateAsync(TempWorkspace workspace,
        int width = 1280, int height = 1000, bool dark = false, bool chinese = false)
    {
        PresentationHostServices services = await ReportControlTestHost.CreateServicesAsync(workspace);
        MainWindow? window = null;
        try
        {
            window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default)
            {
                Width = width,
                Height = height,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            };
            var host = new ReplaceOutputLayoutTestHost(window);
            window.Show();
            await ReportControlTestHost.AwaitHistoryReadyAsync(window);
            host.Shell.IsReducedMotionEnabled = true;
            host.Shell.ShowReplaceCommand.Execute(null);
            host.Shell.WorkflowSession.SelectedIc = "NT51929";
            host.Shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
            host.Render();
            window = null;
            return host;
        }
        finally
        {
            if (window is not null)
            {
                await ReportControlTestHost.CloseAndFlushAsync(window);
                window.Dispose();
            }
        }
    }

    /// <summary>Authors a small General replacement through the real input and inspection path.</summary>
    internal async Task LoadGeneralAsync(TempWorkspace workspace)
    {
        Shell.WorkflowSession.SelectedIc = "NT51926";
        Shell.Replace.SelectedReplaceMode = ExperienceIds.GeneralReplace;
        using var golden = StandardMergeGoldenManifest.Load();
        await Shell.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.ReplaceBase,
            golden.ExpectedOutputPath(golden.CaseByIc("51926")), TestContext.Current.CancellationToken);
        GeneralReplaceMappingViewModel mapping = Assert.Single(Shell.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "0x3E020";
        mapping.Length = "0x4";
        await Shell.WorkflowSession.SetSlotFileAsync(mapping.MappingId,
            workspace.Write("replacement.bin", [0x10, 0x11, 0x12, 0x13]), TestContext.Current.CancellationToken);
        await SettleAsync();
    }

    /// <summary>Loads a canonical CtrlRAM replacement through the production launch path.</summary>
    internal async Task LoadCtrlRamAsync(string ic = "NT51950", string caseId = "nt51950-fw200-single-auto-prj-676-20260717")
    {
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", caseId);
        var options = UiLaunchOptions.Parse(["--workflow", "ctrlram-replace", "--ic", ic, "--ic-num", "single",
            "--base", PathFor("expected-output"), "--ctrlram", "replace-ctrlram-nf=" + PathFor("postbuild-nf-ctrlram")]);
        await MainWindow.ApplyCtrlRamLaunchAsync(Shell, options.CtrlRam!, TestContext.Current.CancellationToken);
        await SettleAsync();

        string PathFor(string id)
        {
            return CanonicalGoldenTestData.ArtifactPath(fixture.GetProperty("artifacts").EnumerateArray()
                .Single(artifact => artifact.GetProperty("artifactId").GetString() == id));
        }
    }

    /// <summary>Loads the canonical three-chip CtrlRAM evidence case through its declared slots.</summary>
    internal async Task LoadThreeChipAsync()
    {
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace", "nt51927-3chip-self-20260705");
        var arguments = new List<string> { "--workflow", "ctrlram-replace", "--ic", "NT51927", "--ic-num", "3" };
        foreach (JsonElement artifact in fixture.GetProperty("artifacts").EnumerateArray())
        {
            string slot = artifact.GetProperty("slotId").GetString()!;
            string path = CanonicalGoldenTestData.ArtifactPath(artifact);
            arguments.Add(slot == "replace-base" ? "--base" : "--ctrlram");
            arguments.Add(slot == "replace-base" ? path : $"{slot}={path}");
        }
        UiLaunchOptions options = UiLaunchOptions.Parse([.. arguments]);
        Assert.Empty(options.Issues);
        await MainWindow.ApplyCtrlRamLaunchAsync(Shell, options.CtrlRam!, TestContext.Current.CancellationToken);
        await SettleAsync();
    }

    /// <summary>Waits on the current inspection task and pumps deterministic layout.</summary>
    internal async Task SettleAsync()
    {
        await Shell.Replace.Inspection.ActiveTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Render();
    }

    /// <summary>Finds a row containing an original or coalesced coverage member.</summary>
    internal Button RowFor(MemoryCoverageSegmentViewModel segment)
    {
        return MergeOutputLayoutTestHost.RowFor(Panel, segment);
    }

    /// <summary>Opens a row through the same keyboard path as the Merge page.</summary>
    internal void Open(MemoryCoverageSegmentViewModel segment)
    {
        MergeOutputLayoutTestHost.Open(Window, Panel, segment);
        Render();
        Assert.True(OutputLayoutPanelTestHost.Body(RowFor(segment)).IsVisible);
    }

    /// <summary>Checks complete inline details, technical context, containment and Escape focus.</summary>
    internal void AssertDetails(MemoryCoverageSegmentViewModel segment)
    {
        Open(segment);
        Button row = RowFor(segment);
        Control body = OutputLayoutPanelTestHost.Body(row);
        MemoryCoverageSegmentViewModel displayed = Assert.IsType<OutputLayoutPanelRow>(row.DataContext).Members[0];
        Assert.Contains(body.GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == displayed.AddressRangeLabel);
        Assert.Contains(body.GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == displayed.SourceLabel);
        Button technical = Assert.Single(body.GetVisualDescendants().OfType<Button>(),
            static button => AutomationProperties.GetAutomationId(button) == "OutputLayout.Technical");
        technical.Command!.Execute(null);
        Render();
        Assert.Contains(OutputLayoutPanelTestHost.Body(technical).GetVisualDescendants().OfType<TextBlock>(),
            text => text.IsEffectivelyVisible && text.Text == displayed.AddressSpaceId);
        Assert.All(body.GetVisualDescendants().OfType<TextBlock>().Where(static text => text.IsEffectivelyVisible),
            text => Assert.True(Bounds(text).Right <= Bounds(Panel).Right + 0.5));
        _ = row.Focus(NavigationMethod.Tab);
        Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Render();
        Assert.False(body.IsVisible);
        Assert.True(row.IsFocused);
        Assert.DoesNotContain(Window.GetVisualDescendants(), static control => control.Name is "MemorySliceCard" or "MemoryLocalView");
    }

    /// <summary>Measures a production control in window coordinates.</summary>
    internal Rect Bounds(Control control)
    {
        return new(control.TranslatePoint(default, Window)!.Value, control.Bounds.Size);
    }

    /// <summary>Pumps the shared headless renderer without a timed wait.</summary>
    internal void Render()
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Writes a complete production-window frame when visual output is enabled.</summary>
    internal void Capture(string name)
    {
        string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) { return; }
        Render();
        _ = Directory.CreateDirectory(directory);
        using Avalonia.Media.Imaging.Bitmap? frame = Window.GetLastRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(new PixelSize((int)Window.Width, (int)Window.Height), frame.PixelSize);
        frame.SavePng(Path.Combine(directory, name + ".png"));
    }

    /// <summary>Closes the real application through its existing confirmation helper.</summary>
    public async ValueTask DisposeAsync()
    {
        await ReportControlTestHost.CloseAndFlushAsync(Window);
        Window.Dispose();
    }
}
