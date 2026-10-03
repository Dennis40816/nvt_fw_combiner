using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Measures responsive fact columns on the production firmware slot card.</summary>
public sealed class FirmwareSlotFactColumnsTests
{
    /// <summary>Loaded work pages use four fact columns at Full HD and wrap without clipping at smaller viewports.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkPageFactColumnsFitTheActualSlotPane(bool replace)
    {
        using var workspace = TempWorkspace.Create("work-page-fact-columns");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        { Width = 1920, Height = 1080 };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            if (replace)
            {
                shell.ShowReplaceCommand.Execute(null);
                shell.WorkflowSession.SelectedIc = "NT51926";
                shell.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;
                JsonElement golden = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace",
                    "nt51926-fw200-single-auto-prj-597-20260718");
                JsonElement[] artifacts = [.. golden.GetProperty("artifacts").EnumerateArray()];
                JsonElement baseArtifact = Assert.Single(artifacts,
                    artifact => artifact.GetProperty("artifactId").GetString() == "expected-output");
                await shell.WorkflowSession.SetSlotFileAsync("replace-base", CanonicalGoldenTestData.ArtifactPath(baseArtifact),
                    TestContext.Current.CancellationToken);
                foreach (string role in new[] { "mp", "nf", "normal", "vn" })
                {
                    JsonElement artifact = Assert.Single(artifacts,
                        artifact => artifact.GetProperty("artifactId").GetString() == $"{role}-ctrlram-input");
                    await shell.WorkflowSession.SetSlotFileAsync($"replace-ctrlram-{role}",
                        CanonicalGoldenTestData.ArtifactPath(artifact), TestContext.Current.CancellationToken);
                }
            }
            else
            {
                shell.ShowMergeCommand.Execute(null);
                shell.WorkflowSession.SelectedIc = "NT51951";
                shell.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
                using var golden = StandardMergeGoldenManifest.Load();
                JsonElement inputs = golden.CaseByIc("51951").GetProperty("inputs");
                await shell.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.MergeDp,
                    golden.ManifestPath(inputs.GetProperty("dp-input")), TestContext.Current.CancellationToken);
                await shell.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.MergeTp,
                    golden.ManifestPath(inputs.GetProperty("tp-input")), TestContext.Current.CancellationToken);
            }

            foreach ((int width, int height) in new[] { (1920, 1080), (1280, 800) })
            {
                window.Width = width;
                window.Height = height;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                FirmwareSlotCard[] cards = [.. window.GetVisualDescendants().OfType<FirmwareSlotCard>()
                    .Where(card => card.IsEffectivelyVisible)];
                Assert.Equal(replace ? shell.Replace.ReplaceSlots.Count : shell.Merge.MergeSlots.Count, cards.Length);
                Assert.NotEmpty(cards);
                Assert.Contains(cards, card => ((FirmwareSlotViewModel)card.DataContext!).PrimaryFirmwareFacts.Count > 0);
                foreach (FirmwareSlotCard card in cards)
                {
                    FirmwareSlotViewModel slot = Assert.IsType<FirmwareSlotViewModel>(card.DataContext);
                    Assert.True(slot.HasFile);
                    Assert.NotNull(slot.CurrentInspectionProjection);
                    if (width == 1920) { Assert.Equal(4, card.FactColumnCount); }
                    if (width == 1280) { Assert.InRange(card.FactColumnCount, 1, 3); }
                    card.BringIntoView();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    ItemsControl facts = card.FindControl<ItemsControl>("PrimaryFirmwareFactsHost")!;
                    TextBlock[] blocks = [.. facts.GetVisualDescendants().OfType<TextBlock>()];
                    Assert.Equal(slot.PrimaryFirmwareFacts.Count * 2, blocks.Length);
                    Assert.All(blocks, block => AssertFactTextFitsCard(block, card));
                    Assert.All(card.GetVisualAncestors().OfType<ScrollViewer>(), scroll =>
                        Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 0.5));
                }
            }
        }
        finally { await CloseAndFlushAsync(window); }
    }

    /// <summary>Fact cells reflow from one to four columns without clipping their labels or values.</summary>
    [AvaloniaTheory]
    [InlineData(400, 1)]
    [InlineData(600, 2)]
    [InlineData(900, 3)]
    [InlineData(1200, 4)]
    public void FactColumnsReflowWithoutClippingText(double width, int expectedColumns)
    {
        var slot = new FirmwareSlotViewModel("tp", "TP BIN", "Select TP firmware", FirmwareSlotKind.Tp)
        {
            FilePath = @"C:\firmware\tp-input.bin",
        };
        slot.SetInputInspection(FirmwareInputInspectionSeverity.Valid, "Verified");
        slot.SetFirmwareFacts([
            new("Common FW Version", "2.0.0"),
            new("TP Version", "T04-00"),
            new("PID", "0x135E"),
            new("Jira Index", "AUTO_PRJ-576"),
        ]);
        var card = new FirmwareSlotCard
        {
            BrowseLabel = "Browse",
            DataContext = slot,
            Width = width,
            VerticalAlignment = VerticalAlignment.Top,
        };
        var host = new Window { Width = width, Height = 1_000 };
        var templatesUri = new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowSharedTemplates.axaml");
        host.Resources.MergedDictionaries.Add(new ResourceInclude(templatesUri) { Source = templatesUri });
        var buttonStylesUri = new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Styles/MainWindowButtonStyles.axaml");
        host.Styles.Add(new StyleInclude(buttonStylesUri) { Source = buttonStylesUri });
        var visualStylesUri = new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Styles/MainWindowVisualStyles.axaml");
        host.Styles.Add(new StyleInclude(visualStylesUri) { Source = visualStylesUri });
        var slotStylesUri = new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Styles/FirmwareSlotExperienceStyles.axaml");
        host.Styles.Add(new StyleInclude(slotStylesUri) { Source = slotStylesUri });
        host.Content = card;
        host.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Assert.Equal(width, card.Bounds.Width);
            Assert.Equal(expectedColumns, card.FactColumnCount);
            ItemsControl facts = card.FindControl<ItemsControl>("PrimaryFirmwareFactsHost")!;
            UniformGrid grid = Assert.Single(facts.GetVisualDescendants().OfType<UniformGrid>());
            Assert.Equal(expectedColumns, grid.Columns);
            Control[] cells = [.. grid.Children];
            Assert.Equal(4, cells.Length);
            for (int index = 0; index < cells.Length; index++)
            {
                Control cell = cells[index];
                Assert.Equal(cells[index / expectedColumns * expectedColumns].Bounds.Y, cell.Bounds.Y);
                if (index >= expectedColumns)
                {
                    Assert.True(cell.Bounds.Y > cells[index - expectedColumns].Bounds.Y);
                }
                TextBlock[] text = [.. cell.GetVisualDescendants().OfType<TextBlock>()];
                Assert.Equal([slot.PrimaryFirmwareFacts[index].Label, slot.PrimaryFirmwareFacts[index].Value],
                    text.Select(block => block.Text));
                foreach (TextBlock block in text)
                {
                    AssertFactTextFitsCard(block, card);
                }
            }
        }
        finally
        {
            host.Close();
        }
    }

    private static void AssertFactTextFitsCard(TextBlock block, FirmwareSlotCard card)
    {
        Assert.True(block.IsEffectivelyVisible);
        Assert.All(block.TextLayout.TextLines, line => Assert.False(line.HasCollapsed));
        Assert.InRange(block.TextLayout.WidthIncludingTrailingWhitespace, 1, block.Bounds.Width + 0.5);
        Assert.InRange(block.TextLayout.Height, 1, block.Bounds.Height + 0.5);
        Point origin = Assert.IsType<Point>(block.TranslatePoint(default, card));
        Assert.InRange(origin.X, 0, card.Bounds.Width);
        Assert.True(origin.X + block.Bounds.Width <= card.Bounds.Width + 0.5);
    }
}
