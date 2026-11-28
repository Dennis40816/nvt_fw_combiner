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

/// <summary>Exercises the real Merge page with the shared application startup and screenshot helpers.</summary>
internal sealed class MergeOutputLayoutTestHost(MainWindow window) : IAsyncDisposable
{
    internal MainWindow Window { get; } = window;
    internal MainWindowViewModel Shell => Assert.IsType<MainWindowViewModel>(Window.DataContext);
    internal OutputLayoutPanel Panel => Assert.Single(Window.GetVisualDescendants().OfType<OutputLayoutPanel>(),
        static panel => panel.IsEffectivelyVisible);
    internal OutputLayoutPanelModel Model => Assert.IsType<OutputLayoutPanelModel>(Panel.Model);

    /// <summary>Opens the real window and waits on the shared startup completion signal.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "The returned host owns the window and closes and disposes it in DisposeAsync.")]
    internal static async Task<MergeOutputLayoutTestHost> CreateAsync(TempWorkspace workspace,
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
            var host = new MergeOutputLayoutTestHost(window);
            window.Show();
            await ReportControlTestHost.AwaitHistoryReadyAsync(window);
            host.Shell.IsReducedMotionEnabled = true;
            host.Shell.ShowMergeCommand.Execute(null);
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

    /// <summary>Loads the canonical Standard inputs through the workflow session.</summary>
    internal async Task LoadStandardAsync(string ic = "NT51929")
    {
        Shell.WorkflowSession.SelectedIc = ic;
        Shell.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement inputs = golden.CaseByIc(ic[2..]).GetProperty("inputs");
        FirmwareSlotViewModel[] slots = [.. Shell.Merge.MergeSlots];
        foreach (FirmwareSlotViewModel slot in slots)
        {
            string artifact = slot.AddressSpaceId!;
            if (inputs.TryGetProperty(artifact, out JsonElement input))
            {
                await Shell.WorkflowSession.SetSlotFileAsync(slot.SlotId, golden.ManifestPath(input), TestContext.Current.CancellationToken);
            }
        }
        await SettleAsync();
    }

    /// <summary>Loads a canonical AB example through the existing production launch path.</summary>
    internal async Task LoadAbAsync(string ic = "NT51929", string caseId = "nt51929-ab-t05-d06")
    {
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectCase("ab-merge", caseId);
        await MainWindow.ApplyAbMergeLaunchAsync(Shell, new AbMergeLaunchRequest(ic, "single",
            Input("dp-ab-input"), Input("tp-a-input"), Input("tp-b-input")), TestContext.Current.CancellationToken);
        await SettleAsync();

        string Input(string id)
        {
            return CanonicalGoldenTestData.ArtifactPath(fixture.GetProperty("artifacts").EnumerateArray()
            .Single(artifact => artifact.GetProperty("artifactId").GetString() == id));
        }
    }

    /// <summary>Authors a synthetic General mapping through the real input and inspection path.</summary>
    internal async Task LoadGeneralAsync(TempWorkspace workspace)
    {
        Shell.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        Shell.Merge.GeneralMergeOutputLength = "0x40000";
        Shell.Merge.GeneralMergeOutputFillByte = "0xFF";
        GeneralMergeMappingViewModel mapping = Assert.Single(Shell.Merge.GeneralMergeMappings);
        mapping.TargetStartAddress = "0x7000";
        mapping.Length = "0x4";
        await Shell.WorkflowSession.SetSlotFileAsync(mapping.MappingId,
            workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]), TestContext.Current.CancellationToken);
        await SettleAsync();
    }

    /// <summary>Waits for the production inspection task and pumps deterministic layout.</summary>
    internal async Task SettleAsync()
    {
        await Shell.Merge.Inspection.ActiveTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Render();
    }

    /// <summary>Finds the disclosure that owns an original or coalesced coverage member.</summary>
    internal Button RowFor(MemoryCoverageSegmentViewModel segment)
    {
        return RowFor(Panel, segment);
    }

    /// <summary>Same lookup for a test that owns its window.</summary>
    internal static Button RowFor(OutputLayoutPanel panel, MemoryCoverageSegmentViewModel segment)
    {
        return Assert.Single(panel.GetVisualDescendants().OfType<Button>(),
        button => button.DataContext is OutputLayoutPanelRow { Members.Count: 1 } row &&
            (ReferenceEquals(row.Members[0], segment) || row.Members[0].DisplayParts.Contains(segment)));
    }

    /// <summary>Reveals parent groups and opens a row with real keyboard input.</summary>
    internal void Open(MemoryCoverageSegmentViewModel segment)
    {
        Open(Window, Panel, segment);
        Render();
        Assert.True(OutputLayoutPanelTestHost.Body(RowFor(segment)).IsVisible);
    }

    /// <summary>Same keyboard path for a test that owns its window; the caller renders afterwards.</summary>
    internal static void Open(Window window, OutputLayoutPanel panel, MemoryCoverageSegmentViewModel segment)
    {
        Button row = RowFor(panel, segment);
        foreach (StackPanel ancestor in row.GetVisualAncestors().OfType<StackPanel>().Reverse())
        {
            if (ancestor.Children.Count == 2 && ancestor.Children[0] is Button header &&
                AutomationProperties.GetAutomationId(header)?.StartsWith("OutputLayout.", StringComparison.Ordinal) == true &&
                !ancestor.Children[1].IsVisible)
            {
                header.Command!.Execute(null);
            }
        }
        _ = row.Focus(NavigationMethod.Tab);
        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        window.KeyRelease(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
    }

    /// <summary>Checks supplied details, horizontal containment, technical disclosure and Escape focus.</summary>
    internal void AssertDetails(MemoryCoverageSegmentViewModel segment)
    {
        Open(segment);
        Button row = RowFor(segment);
        Control body = OutputLayoutPanelTestHost.Body(row);
        MemoryCoverageSegmentViewModel displayed = Assert.IsType<OutputLayoutPanelRow>(row.DataContext).Members[0];
        Assert.Contains(body.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text == displayed.AddressRangeLabel);
        Assert.Contains(body.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text == displayed.SizeValue);
        Assert.Contains(body.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text == displayed.SourceLabel);
        Button technical = Assert.Single(body.GetVisualDescendants().OfType<Button>(),
            static button => AutomationProperties.GetAutomationId(button) == "OutputLayout.Technical");
        Assert.False(OutputLayoutPanelTestHost.Body(technical).IsVisible);
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

    /// <summary>Writes a complete production-window frame using the existing PNG helper.</summary>
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

    /// <summary>Closes the real application through its existing confirmation and flush helper.</summary>
    public async ValueTask DisposeAsync()
    {
        await ReportControlTestHost.CloseAndFlushAsync(Window);
        Window.Dispose();
    }
}
