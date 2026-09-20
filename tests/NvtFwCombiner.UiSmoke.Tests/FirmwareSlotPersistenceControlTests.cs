using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
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

/// <summary>Shared file cards render current input facts, never hide selected facts or retain cleared metadata.</summary>
public sealed class FirmwareSlotPersistenceControlTests
{
    /// <summary>Standard DP/TP and AB DP_AB/TPA/TPB use current rendered cards throughout the public input lifecycle.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task MergeCardsKeepCurrentFactsAcrossSelectionAndLanguageChanges(bool ab, bool dark)
    {
        using var workspace = TempWorkspace.Create("merge-slot-persistence");
        using var standard = StandardMergeGoldenManifest.Load();
        (string SlotId, string Path)[] inputs;
        if (ab)
        {
            JsonElement golden = CanonicalGoldenTestData.LoadDirectCase("ab-merge", "nt51929-ab-t05-d06");
            JsonElement[] artifacts = [.. golden.GetProperty("artifacts").EnumerateArray()];
            string[] slots = [CompositionAddressSpaceIds.DpAbInput, CompositionAddressSpaceIds.TpAInput, CompositionAddressSpaceIds.TpBInput];
            inputs = [.. slots.Select(id => (id, CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact =>
                artifact.GetProperty("artifactId").GetString() == id))))];
        }
        else
        {
            JsonElement paths = standard.CaseByIc("51951").GetProperty("inputs");
            inputs = [(CompositionSlotIds.MergeDp, standard.ManifestPath(paths.GetProperty("dp-input"))),
                (CompositionSlotIds.MergeTp, standard.ManifestPath(paths.GetProperty("tp-input")))];
        }
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default)
        { Width = dark ? 980 : 1440, Height = 900, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            shell.ShowMergeCommand.Execute(null);
            shell.WorkflowSession.SelectedIc = ab ? "NT51929" : "NT51951";
            shell.Merge.SelectedMergeMode = ab ? ExperienceIds.AbMerge : ExperienceIds.StandardMerge;
            foreach ((string id, string path) in inputs)
            {
                await shell.WorkflowSession.SetSlotFileAsync(id, path, TestContext.Current.CancellationToken);
            }
            if (!ab)
            {
                FirmwareSlotViewModel dp = Assert.Single(shell.Merge.MergeSlots, slot => slot.SlotId == CompositionSlotIds.MergeDp);
                Assert.Contains(dp.FirmwareFacts, fact => fact.Label == "DP Version" && fact.Value == "D05-00");
                Assert.Contains(dp.FirmwareFacts, fact => fact.Label == "Jira Index" && fact.Value == "AUTO_PRJ-695");
            }
            foreach ((string id, string path) in inputs)
            {
                for (int step = 0; step < 3; step++)
                {
                    if (step == 1) { await shell.WorkflowSession.ClearSlotFileAsync(id, TestContext.Current.CancellationToken); }
                    if (step == 2) { await shell.WorkflowSession.SetSlotFileAsync(id, path, TestContext.Current.CancellationToken); }
                    foreach (bool chinese in new[] { false, true })
                    {
                        shell.SelectedLanguage = chinese ? "Traditional Chinese" : "English";
                        Render();
                        FirmwareSlotViewModel slot = Assert.Single(shell.Merge.MergeSlots, candidate => candidate.SlotId == id);
                        AssertCard(window, slot, Path.GetFileName(path), selected: step != 1);
                        if (step == 0 && id == inputs[0].SlotId && Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR") is { Length: > 0 } directory)
                        {
                            _ = Directory.CreateDirectory(directory);
                            using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
                            Assert.NotNull(frame);
                            frame.Save(Path.Combine(directory, $"input-column-{(ab ? "ab" : "standard")}-{dark}-{chinese}.png"));
                        }
                        foreach ((string peer, string peerPath) in inputs.Where(input => input.SlotId != id))
                        {
                            Assert.Equal(peerPath, Assert.Single(shell.Merge.MergeSlots, candidate => candidate.SlotId == peer).FilePath);
                        }
                    }
                }
            }
        }
        finally { await CloseAndFlushAsync(window); }
    }

    /// <summary>The retained structured Replace view shares the input inset without changing its catalog admission.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StructuredReplaceCardsUseTheSameInputColumn(bool dark)
    {
        using var workspace = TempWorkspace.Create("replace-slot-column");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default)
        { Width = dark ? 980 : 1440, Height = 900, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            shell.ShowReplaceCommand.Execute(null);
            shell.WorkflowSession.SelectedIc = "NT51928";
            shell.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;
            Render();
            Assert.True(shell.Replace.IsCtrlRamReplaceModeSelected);
            FirmwareSlotCard[] cards = [.. window.GetVisualDescendants().OfType<FirmwareSlotCard>()
                .Where(card => card.IsEffectivelyVisible)];
            Assert.Equal(shell.Replace.ReplaceSlots.Count, cards.Length);
            Assert.NotEmpty(cards);
            Assert.All(cards, card => AssertInputColumn(card, window));
        }
        finally { await CloseAndFlushAsync(window); }
    }

    private static void AssertCard(Window window, FirmwareSlotViewModel slot, string fileName, bool selected)
    {
        FirmwareSlotCard card = Assert.Single(window.GetVisualDescendants().OfType<FirmwareSlotCard>(),
            candidate => ReferenceEquals(candidate.DataContext, slot));
        card.BringIntoView();
        Render();
        AssertInputColumn(card, window);
        Assert.Equal(selected, slot.HasFile);
        Button clear = Assert.IsType<Button>(card.FindControl<Control>("ClearButton"));
        Assert.Equal(selected, clear.IsEnabled);
        if (!selected)
        {
            Assert.Empty(slot.FirmwareFacts);
            Assert.DoesNotContain(card.GetVisualDescendants().OfType<TextBlock>(),
                block => block.IsEffectivelyVisible && block.Classes.Contains("firmwareSlotFactValue"));
            Assert.DoesNotContain(card.GetVisualDescendants().OfType<TextBlock>(),
                block => block.IsEffectivelyVisible && block.Text?.Contains(fileName, StringComparison.Ordinal) == true);
            return;
        }
        Assert.False(slot.IsGuidanceVisible); // Selected-file metadata must not depend on empty-slot guidance visibility.
        Assert.NotEmpty(slot.PrimaryFirmwareFacts);
        ItemsControl facts = Assert.IsType<ItemsControl>(card.FindControl<Control>("PrimaryFirmwareFactsHost"));
        Assert.True(facts.IsEffectivelyVisible, $"{slot.SlotId}: selected metadata host is hidden.");
        TextBlock[] values = [.. facts.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.Classes.Contains("firmwareSlotFactValue"))];
        TextBlock[] labels = [.. facts.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.Classes.Contains("firmwareSlotFactLabel"))];
        Assert.Equal(slot.PrimaryFirmwareFacts.Select(static fact => fact.Label), labels.Select(static block => block.Text));
        Assert.All(labels, label => Assert.True(label.IsEffectivelyVisible && label.Bounds.Width > 0 && label.Bounds.Height > 0));
        Assert.Equal(slot.PrimaryFirmwareFacts.Select(static fact => fact.Value), values.Select(static block => block.Text));
        Assert.All(values, block =>
        {
            Assert.True(block.IsEffectivelyVisible, $"{slot.SlotId}: '{block.Text}' disappeared.");
            Assert.True(block.Bounds.Width > 0 && block.Bounds.Height > 0);
            // Existing responsive cards allow ellipsis, but must retain the complete value on hover.
            Assert.NotEmpty(block.TextLayout.TextLines);
            Assert.Equal(block.Text, ToolTip.GetTip(block));
            Point position = block.TranslatePoint(default, card)!.Value;
            Assert.InRange(position.X, 0, card.Bounds.Width - block.Bounds.Width + 1);
            Assert.InRange(position.Y, 0, card.Bounds.Height - block.Bounds.Height + 1);
        });
        Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(),
            block => block.IsEffectivelyVisible && block.Text?.Contains(fileName, StringComparison.Ordinal) == true);
    }

    private static void AssertInputColumn(FirmwareSlotCard card, Window window)
    {
        Border panel = card.GetVisualAncestors().OfType<Border>().First(border => border.Classes.Contains("roomyPanel"));
        Border outline = Assert.Single(card.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("firmwareSlot"));
        double panelLeft = panel.TranslatePoint(default, window)!.Value.X;
        double left = outline.TranslatePoint(default, window)!.Value.X;
        // Cards share the Input files content edges after the approved group flattening.
        double expectedLeft = panelLeft + panel.BorderThickness.Left + panel.Padding.Left;
        double expectedRight = panelLeft + panel.Bounds.Width - panel.BorderThickness.Right - panel.Padding.Right;
        Assert.InRange(Math.Abs(left - expectedLeft), 0, 0.5);
        Assert.InRange(Math.Abs(left + outline.Bounds.Width - expectedRight), 0, 0.5);
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
