using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises the real AB page and modal against the accepted horizontal-row interaction.</summary>
public sealed class AbDummyDpControlTests
{
    /// <summary>The row, confirmation focus and disabled DP surface use the production window.</summary>
    [AvaloniaTheory]
    [InlineData(1440, 900, false, false)]
    [InlineData(1440, 900, true, true)]
    [InlineData(980, 640, false, true)]
    [InlineData(980, 640, true, false)]
    public async Task DummyDpUsesApprovedRowAndConfirmation(int width, int height, bool dark, bool chinese)
    {
        using var workspace = TempWorkspace.Create("dummy-dp-control");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default);
        var shell = (MainWindowViewModel)window.DataContext!;
        window.Width = width;
        window.Height = height;
        window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        window.Show();
        await AwaitHistoryReadyAsync(window);
        try
        {
            shell.ShowMergeCommand.Execute(null);
            shell.WorkflowSession.SelectedIc = "NT51929";
            shell.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
            if (chinese) { shell.SelectedLanguage = "Traditional Chinese"; }
            Render();
            Save("initial");
            CheckBox sameTp = Assert.Single(window.GetVisualDescendants().OfType<CheckBox>(),
                c => Equals(c.Content, shell.Text.AbSameTpOptionLabel));
            CheckBox dummy = Assert.Single(window.GetVisualDescendants().OfType<CheckBox>(),
                c => c.Name == "AbDummyDpCheckBox");
            Rect sameBounds = Bounds(sameTp);
            Rect dummyBounds = Bounds(dummy);
            Assert.InRange(Math.Abs(sameBounds.Center.Y - dummyBounds.Center.Y), 0, 0.5);
            Assert.True(dummyBounds.Left >= sameBounds.Right);
            Assert.False(dummy.IsChecked);
            Assert.True(dummy.Focus());
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            Render();
            Assert.False(dummy.IsChecked);
            Assert.False(shell.IsCompositionActionRailVisible);
            Assert.False(window.FindControl<Control>("ShellInteractionHost")!.IsEffectivelyEnabled);
            ContentControl host = window.FindControl<ContentControl>("AbDummyDpConfirmationModalHost")!;
            Assert.True(host.IsVisible);
            Button cancel = Assert.Single(host.GetVisualDescendants().OfType<Button>(), b => b.Name == "CancelButton");
            Button enable = Assert.Single(host.GetVisualDescendants().OfType<Button>(), b => b.Name == "EnableButton");
            Assert.Same(cancel, window.FocusManager?.GetFocusedElement());
            Assert.Contains("secondary", cancel.Classes);
            Assert.Contains("primary", enable.Classes);
            Save("confirmation");
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Render();
            Assert.False(shell.Merge.IsAbDummyDpPromptOpen);
            Assert.False(shell.Merge.UseDummyDpForAbMerge);
            await shell.Merge.ToggleAbDummyDpCommand.ExecuteAsync(null);
            Render();
            Assert.True(cancel.IsEffectivelyVisible);
            Assert.True(cancel.IsEffectivelyEnabled);
            Assert.Same(cancel, window.FocusManager?.GetFocusedElement());
            await shell.Merge.ConfirmAbDummyDpCommand.ExecuteAsync(null);
            Render();
            Assert.True(dummy.IsChecked);
            Assert.DoesNotContain("DP_AB", shell.Merge.MergeReadinessStatus);
            Control disabledDp = Assert.Single(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "AbDummyDpDisplay");
            Assert.True(disabledDp.IsVisible);
            Assert.False(disabledDp.IsEffectivelyEnabled);
            Border dummyOutline = Assert.Single(disabledDp.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("firmwareSlot"));
            Rect dummyCardBounds = Bounds(dummyOutline);
            FirmwareSlotCard[] inputCards = [.. window.GetVisualDescendants().OfType<FirmwareSlotCard>()
                .Where(card => card.IsEffectivelyVisible && card.IsEffectivelyEnabled)];
            Assert.NotEmpty(inputCards);
            foreach (FirmwareSlotCard card in inputCards)
            {
                Rect cardBounds = Bounds(Assert.Single(card.GetVisualDescendants().OfType<Border>(),
                    border => border.Classes.Contains("firmwareSlot")));
                Assert.InRange(Math.Abs(cardBounds.Left - dummyCardBounds.Left), 0, 0.5);
                Assert.InRange(Math.Abs(cardBounds.Right - dummyCardBounds.Right), 0, 0.5);
            }
            Assert.DoesNotContain(shell.Merge.AbMergeSlots, slot => slot.SlotId == CompositionAddressSpaceIds.DpAbInput);
            MemoryCoverageSegmentViewModel blank = Assert.Single(shell.Merge.MergeCoverageSegments,
                segment => segment.RangeStart == 0 && segment.RangeEndExclusive == 0x7000);
            Assert.Equal(NvtFwCombiner.Application.MemoryLayout.MemoryContentRole.General, blank.ContentRole);
            Assert.Equal(chinese ? "資料" : "Data", blank.DisplayTitle);
            Assert.Equal("0xFF", blank.SourceLabel);
            Assert.Equal(chinese ? "初始化" : "Initialization", blank.SourceFieldLabel);
            Assert.False(blank.UsesKeptPattern);
            CtrlRamSelectorLayoutTests.AssertMergePanelAlignment(window);
            // Source facts belong to the hover card, not a permanently expanded detail list.
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), border => border.Name == "MemorySliceCard");
            MemoryCoverageBar rail = Assert.Single(window.GetVisualDescendants().OfType<MemoryCoverageBar>(), control => control.IsEffectivelyVisible);
            rail.BringIntoView();
            rail.ReducedMotion = true;
            Render();
            Border blankCell = Assert.Single(rail.GetVisualDescendants().OfType<Border>(), border =>
                border.Classes.Contains("memoryCoverageBarSegment") && ReferenceEquals(border.DataContext, blank));
            window.MouseMove(Bounds(blankCell).Center, RawInputModifiers.None);
            Render();
            Border hoverCard = Assert.Single(window.GetVisualDescendants().OfType<Border>(), border => border.Name == "MemorySliceCard");
            Assert.Same(blank, hoverCard.DataContext);
            Assert.Contains(hoverCard.GetVisualDescendants().OfType<TextBlock>(),
                block => block.IsEffectivelyVisible && block.Text == (chinese ? "初始化" : "Initialization"));
            Assert.Contains(hoverCard.GetVisualDescendants().OfType<TextBlock>(), block => block.IsEffectivelyVisible && block.Text == "0xFF");
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == "Output range uses bytes from Reserved.");
            Save("enabled");
            window.MouseMove(new Point(4, 4), RawInputModifiers.None);
            await shell.Merge.ToggleAbDummyDpCommand.ExecuteAsync(null);
            Render();
            Assert.False(dummy.IsChecked);
            Assert.False(disabledDp.IsVisible);
            Assert.Null(shell.Merge.MergeSlots.Single(slot => slot.SlotId == CompositionAddressSpaceIds.DpAbInput).FilePath);
        }
        finally { await CloseAndFlushAsync(window); }

        Rect Bounds(Control control)
        {
            return new(Assert.IsType<Point>(control.TranslatePoint(default, window)), control.Bounds.Size);
        }
        void Save(string state)
        {
            string? directory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (string.IsNullOrWhiteSpace(directory)) { return; }
            _ = Directory.CreateDirectory(directory);
            using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(Path.Combine(directory, $"dummy-{width}-{height}-{dark}-{chinese}-{state}.png"));
        }
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
