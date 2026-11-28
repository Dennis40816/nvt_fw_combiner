using System.Text.Json;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    private static readonly Uri ProductionSharedTemplatesUri = new(
        "avares://NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowSharedTemplates.axaml");
    private static readonly Uri ProductionMainWindowStylesUri = new(
        "avares://NvtFwCombiner.Presentation.Avalonia/Styles/MainWindowStyles.axaml");
    private static readonly Uri ProductionWorkflowTemplatesUri = new(
        "avares://NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowWorkflowTemplates.axaml");

    /// <summary>The real NT51950 golden route stays legible and bounded in the production rail.</summary>
    [AvaloniaTheory]
    [InlineData(430, false, false)]
    [InlineData(430, false, true)]
    [InlineData(430, true, false)]
    [InlineData(430, true, true)]
    [InlineData(360, false, false)]
    [InlineData(360, false, true)]
    [InlineData(360, true, false)]
    [InlineData(360, true, true)]
    public async Task RealNt51950GoldenCtrlRamPanelFitsProductionRail(
        double railWidth,
        bool useDarkTheme,
        bool useTraditionalChinese)
    {
        MainWindowViewModel viewModel = await CreateNt51950GoldenCtrlRamViewModelAsync(
            TestContext.Current.CancellationToken);
        viewModel.IsReducedMotionEnabled = true;
        if (useTraditionalChinese)
        {
            viewModel.SelectedLanguage = "Traditional Chinese";
        }

        ThemeVariant theme = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        var workflowTemplates = new ResourceInclude(ProductionWorkflowTemplatesUri)
        {
            Source = ProductionWorkflowTemplatesUri,
        };
        Assert.True(workflowTemplates.TryGetResource(
            "ReplaceOutputLayoutPanelTemplate",
            theme,
            out object? templateResource));
        IDataTemplate template = Assert.IsType<IDataTemplate>(templateResource, exactMatch: false);
        var panel = new ContentControl
        {
            Content = viewModel.Replace,
            ContentTemplate = template,
        };
        var viewport = new ScrollViewer
        {
            Width = railWidth,
            Height = 760,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = panel,
        };
        var host = new Window
        {
            Width = railWidth,
            Height = 760,
            RequestedThemeVariant = theme,
            DataContext = viewModel,
            Content = viewport,
        };
        host.Resources.MergedDictionaries.Add(new ResourceInclude(ProductionSharedTemplatesUri)
        {
            Source = ProductionSharedTemplatesUri,
        });
        host.Resources.MergedDictionaries.Add(workflowTemplates);
        host.Styles.Add(new StyleInclude(ProductionMainWindowStylesUri)
        {
            Source = ProductionMainWindowStylesUri,
        });
        host.Styles.Add(new StyleInclude(ProductionButtonStylesUri)
        {
            Source = ProductionButtonStylesUri,
        });
        host.Styles.Add(new StyleInclude(ProductionVisualStylesUri)
        {
            Source = ProductionVisualStylesUri,
        });

        host.Show();
        try
        {
            host.Measure(new Size(railWidth, 760));
            host.Arrange(new Rect(0, 0, railWidth, 760));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            MemoryCoverageGroupViewModel common = Assert.Single(
                viewModel.Replace.ReplaceCoverageGroups,
                group => group.RegionGroup == ReplaceRegionGroup.Common);
            Assert.Equal(3, common.Items.Count);
            Assert.True(viewModel.Replace.HasCtrlRamFocusLayout);
            Assert.DoesNotContain(
                panel.GetVisualDescendants().OfType<TextBlock>(),
                candidate => candidate.Text == viewModel.Replace.ReplaceSelectedCoverageSummary && candidate.IsEffectivelyVisible);
            Assert.All(
                panel.GetVisualDescendants().OfType<Control>()
                    .Where(candidate => candidate.IsEffectivelyVisible && candidate.Bounds is { Width: > 0, Height: > 0 }),
                candidate => AssertControlFitsWidth(candidate, panel));
            AssertVisibleTextDoesNotOverlap(panel);

            MemoryCoverageBar[] bars = [.. panel.GetVisualDescendants().OfType<MemoryCoverageBar>().Where(bar => bar.IsEffectivelyVisible)];
            MemoryCoverageBar overview = Assert.Single(bars);
            foreach (MemoryCoverageBar bar in bars)
            {
                ProportionalStackPanel proportionalBar = bar.GetVisualDescendants().OfType<ProportionalStackPanel>().First();
                Control[] arrangedSegments = [.. proportionalBar.Children.Where(child => child.Bounds.Width > 0)];
                Assert.NotEmpty(arrangedSegments);
                Assert.Equal(0d, arrangedSegments[0].Bounds.X, 1);
                Assert.Equal(proportionalBar.Bounds.Width, arrangedSegments[^1].Bounds.Right, 1);
                Assert.True(proportionalBar.Bounds.Width > 300d);
            }
            Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Expander>(),
                candidate => candidate.Classes.Contains("inlineDisclosure") && candidate.IsEffectivelyVisible);

            Assert.NotEmpty(viewModel.Replace.CtrlRamFocusLanes);
            bool sawVisibleNf = false;
            foreach (MemoryFocusLaneViewModel lane in viewModel.Replace.CtrlRamFocusLanes)
            {
                ProportionalStackPanel localStrip = CtrlRamMemoryLayoutTests.OpenLane(host, lane);
                Border local = Assert.Single(host.GetVisualDescendants().OfType<Border>(), candidate => candidate.Name == "MemoryLocalView");
                Assert.Contains(local.GetVisualDescendants().OfType<TextBlock>(), candidate =>
                    candidate.Text == lane.Title && candidate.IsEffectivelyVisible);
                Assert.Contains(local.GetVisualDescendants().OfType<TextBlock>(), candidate =>
                    candidate.Text?.StartsWith(viewModel.Replace.Text.MemoryCtrlRamDetailLabel + " · ", StringComparison.Ordinal) == true && candidate.IsEffectivelyVisible);
                sawVisibleNf |= local.GetVisualDescendants().OfType<TextBlock>().Any(candidate =>
                    candidate.Text == "NF" && candidate.IsEffectivelyVisible);
                AssertControlFitsWidth(local, panel);
                AssertVisibleTextDoesNotOverlap(local);
                Control[] cells = [.. localStrip.Children.Where(child => child.Bounds.Width > 0)];
                Assert.NotEmpty(cells);
                Assert.Equal(0d, cells[0].Bounds.X, 1);
                // Same independently snapped child-width contract as
                // CtrlRamMemoryLayoutTests.AssertLiftIsNotClipped; overview precision above is unchanged.
                Assert.InRange(cells[^1].Bounds.Right - localStrip.Bounds.Width, -1, 1);
                Assert.InRange(localStrip.Bounds.Width, 1, overview.Bounds.Width);
                foreach (Control cell in cells)
                {
                    Assert.True(cell.Focus(NavigationMethod.Tab));
                    Dispatcher.UIThread.RunJobs();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    Border card = Assert.Single(host.GetVisualDescendants().OfType<Border>(), candidate => candidate.Name == "MemorySliceCard");
                    Assert.Same(cell.DataContext, card.DataContext);
                    AssertControlFitsWidth(card, panel);
                }
            }
            Assert.True(sawVisibleNf, "The real NF segment must be visible after opening its lane; shared DiffDLM has a separate lane.");

            using Avalonia.Media.Imaging.Bitmap? frame = host.GetLastRenderedFrame();
            Assert.NotNull(frame);
            string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                _ = Directory.CreateDirectory(outputDirectory);
                string themeName = useDarkTheme ? "dark" : "light";
                string languageName = useTraditionalChinese ? "zh-tw" : "en";
                string outputPath = Path.Combine(
                    outputDirectory,
                    $"nfc-memory-ctrlram-golden-{railWidth:F0}-{themeName}-{languageName}.png");
                await using FileStream output = File.Create(outputPath);
                frame.SavePng(output);
            }
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>The live region card stacks its title, range, size and summary within the fixed 430 px rail.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void MemoryCoverageRegionCardStacksRangeAtFixedRailWidth(
        bool useDarkTheme)
    {
        ThemeVariant theme = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        var resources = new ResourceInclude(ProductionSharedTemplatesUri)
        {
            Source = ProductionSharedTemplatesUri,
        };
        Assert.True(resources.TryGetResource(
            "MemoryCoverageRegionCardTemplate",
            theme,
            out object? templateResource));
        IDataTemplate template = Assert.IsType<IDataTemplate>(templateResource, exactMatch: false);
        var detail = new MemoryLayoutPreservationDetail(
            "diff-nf-0",
            blockIndex: 0,
            MemoryEndpointIdentity.NotApplicable,
            "postbuild-diffdlm",
            new ByteRange(0xB90, 0x870),
            new ByteRange(0x2DC90, 0x870));
        var segment = new MemoryCoverageSegmentViewModel(
            "0x2D100-0x2E4FF",
            "NF CtrlRAM with a deliberately long localized label",
            "Canonical DiffDLM",
            MemoryCoverageFillRole.DiffDlm,
            20,
            preservationDetails: [detail],
            addressRangeLabel: "0x2D100-0x2E4FF",
            lengthLabel: "len 0x1400");
        var row = new ContentControl
        {
            Content = segment,
            ContentTemplate = template,
        };
        var rail = new Border
        {
            Width = 430,
            Padding = new Thickness(18, 16, 20, 18),
            Child = row,
        };
        var host = new Window
        {
            Width = 430,
            RequestedThemeVariant = theme,
            DataContext = new { Text = ShellTextResources.For(ShellLanguage.English) },
            Content = rail,
        };
        host.Resources.MergedDictionaries.Add(resources);
        var buttonStyles = new StyleInclude(ProductionButtonStylesUri)
        {
            Source = ProductionButtonStylesUri,
        };
        host.Styles.Add(buttonStyles);

        host.Measure(new Size(430, 1_000));
        host.Arrange(new Rect(0, 0, 430, host.DesiredSize.Height));

        TextBlock source = Assert.Single(
            row.GetVisualDescendants().OfType<TextBlock>(),
            candidate => candidate.Text == segment.DisplayTitle && candidate.Classes.Contains("bodyEmphasisText"));
        TextBlock address = Assert.Single(
            row.GetVisualDescendants().OfType<TextBlock>(),
            candidate => candidate.Text == segment.AddressRangeLabel);
        TextBlock length = Assert.Single(
            row.GetVisualDescendants().OfType<TextBlock>(),
            candidate => candidate.Text == segment.SizeValue);
        TextBlock description = Assert.Single(
            row.GetVisualDescendants().OfType<TextBlock>(),
            candidate => candidate.Text == segment.PreservationSummary && candidate.Classes.Contains("memoryInfoDescription"));
        Point sourceOrigin = Assert.IsType<Point>(source.TranslatePoint(default, row));
        Point addressOrigin = Assert.IsType<Point>(address.TranslatePoint(default, row));
        Point lengthOrigin = Assert.IsType<Point>(length.TranslatePoint(default, row));
        Point descriptionOrigin = Assert.IsType<Point>(description.TranslatePoint(default, row));

        Assert.True(addressOrigin.Y >= sourceOrigin.Y + source.Bounds.Height);
        Assert.True(addressOrigin.X > sourceOrigin.X);
        Assert.True(lengthOrigin.Y >= addressOrigin.Y + address.Bounds.Height);
        Assert.Equal(addressOrigin.X, lengthOrigin.X, precision: 3);
        Assert.True(descriptionOrigin.Y >= addressOrigin.Y + address.Bounds.Height);
        AssertControlFitsWidth(source, row);
        AssertControlFitsWidth(address, row);
        AssertControlFitsWidth(length, row);
        AssertControlFitsWidth(description, row);
        Assert.Equal(ProductionButtonStylesUri, buttonStyles.Source);

        var inlineDisclosure = new Expander
        {
            Classes = { "inlineDisclosure" },
            Content = new TextBlock { Text = "Visible input group" },
            IsExpanded = true,
        };
        var disclosureHost = new Window
        {
            Width = 430,
            RequestedThemeVariant = theme,
            Content = inlineDisclosure,
        };
        disclosureHost.Styles.Add(new StyleInclude(ProductionMainWindowStylesUri)
        {
            Source = ProductionMainWindowStylesUri,
        });
        disclosureHost.Measure(new Size(430, 1_000));
        disclosureHost.Arrange(new Rect(0, 0, 430, disclosureHost.DesiredSize.Height));
        ISolidColorBrush disclosureSurface = Assert.IsType<ISolidColorBrush>(
            inlineDisclosure.Background,
            exactMatch: false);
        Assert.Equal(Colors.Transparent, disclosureSurface.Color);

        string sharedTemplates = ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml");
        string workflowTemplates = ReadPresentationFile("Resources/MainWindowWorkflowTemplates.axaml");
        Assert.DoesNotContain(
            "MaxHeight=\"320\"",
            ExtractDataTemplate(workflowTemplates, "ReplaceOutputLayoutPanelTemplate"),
            StringComparison.Ordinal);
        Assert.Contains(
            "ShowLegend=\"True\"",
            ExtractDataTemplate(workflowTemplates, "ReplaceOutputLayoutPanelTemplate"),
            StringComparison.Ordinal);
        XElement slotGroupTemplate = Assert.Single(XDocument.Parse(sharedTemplates).Descendants(),
            element => element.Name.LocalName == "DataTemplate" &&
                element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "FirmwareSlotGroupTemplate"));
        _ = Assert.Single(slotGroupTemplate.Descendants(),
            element => element.Name.LocalName == "Expander" &&
                ((string?)element.Attribute("Classes"))?.Split(' ').Contains("inlineDisclosure", StringComparer.Ordinal) == true);
        string replacePanelTemplate =
            ExtractDataTemplate(workflowTemplates, "ReplaceOutputLayoutPanelTemplate");
        Assert.DoesNotContain("ReplaceBaseCoverageGroup.IsExpanded", replacePanelTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryCoverageGroupTemplate", sharedTemplates, StringComparison.Ordinal);
        string mainWindowStyles = ReadPresentationFile("Styles/MainWindowStyles.axaml");
        Assert.Contains(
            "Expander.inlineDisclosure /template/ ToggleButton",
            mainWindowStyles,
            StringComparison.Ordinal);
    }

    private static async Task<MainWindowViewModel> CreateNt51950GoldenCtrlRamViewModelAsync(
        CancellationToken cancellationToken)
    {
        MainWindowViewModel viewModel = await PresentationTestHost.CreateViewModelAsync(cancellationToken);
        await LoadNt51950GoldenCtrlRamInputsAsync(viewModel, cancellationToken);
        return viewModel;
    }

    internal static async Task LoadNt51950GoldenCtrlRamInputsAsync(MainWindowViewModel viewModel, CancellationToken cancellationToken)
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51950-fw200-single-auto-prj-676-20260717");
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.WorkflowSession.SelectedNumber = "single";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        JsonElement baseArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("artifactId").GetString() == "expected-output");
        await viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-base",
            CanonicalGoldenTestData.ArtifactPath(baseArtifact),
            cancellationToken);
        var slotsByArtifactId = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["postbuild-nf-ctrlram"] = "replace-ctrlram-nf",
            ["postbuild-normal-ctrlram"] = "replace-ctrlram-normal",
            ["postbuild-vn-ctrlram"] = "replace-ctrlram-vn",
        };
        foreach (JsonElement artifact in fixtureCase.GetProperty("artifacts").EnumerateArray())
        {
            string? artifactId = artifact.GetProperty("artifactId").GetString();
            if (artifactId is not null && slotsByArtifactId.TryGetValue(artifactId, out string? slotId))
            {
                await viewModel.WorkflowSession.SetSlotFileAsync(
                    slotId,
                    CanonicalGoldenTestData.ArtifactPath(artifact),
                    cancellationToken);
            }
        }

    }

    private static void AssertControlFitsWidth(Control candidate, Control panel)
    {
        Point point = Assert.IsType<Point>(candidate.TranslatePoint(default, panel));
        Assert.True(
            point.X >= -0.5 && point.X + candidate.Bounds.Width <= panel.Bounds.Width + 0.5,
            $"{candidate.GetType().Name} exceeded the 430 px Memory Layout rail: " +
            $"x={point.X:F1}, width={candidate.Bounds.Width:F1}, panel={panel.Bounds.Width:F1}.");
    }

    private static void AssertVisibleTextDoesNotOverlap(Control panel)
    {
        TextBlock[] textBlocks =
            [.. panel.GetVisualDescendants().OfType<TextBlock>()
                .Where(candidate => candidate.IsEffectivelyVisible && candidate.Bounds is { Width: > 0, Height: > 0 })];
        for (int leftIndex = 0; leftIndex < textBlocks.Length; leftIndex++)
        {
            Point? leftOrigin = textBlocks[leftIndex].TranslatePoint(default, panel);
            if (leftOrigin is not { } leftPoint)
            {
                continue;
            }

            var leftBounds = new Rect(leftPoint, textBlocks[leftIndex].Bounds.Size);
            for (int rightIndex = leftIndex + 1; rightIndex < textBlocks.Length; rightIndex++)
            {
                Point? rightOrigin = textBlocks[rightIndex].TranslatePoint(default, panel);
                if (rightOrigin is not { } rightPoint)
                {
                    continue;
                }

                var rightBounds = new Rect(rightPoint, textBlocks[rightIndex].Bounds.Size);
                Rect intersection = leftBounds.Intersect(rightBounds);
                Assert.True(
                    intersection.Width <= 0.5 || intersection.Height <= 0.5,
                    $"Memory Layout text overlapped: '{textBlocks[leftIndex].Text}' {leftBounds} and " +
                    $"'{textBlocks[rightIndex].Text}' {rightBounds}.");
            }
        }
    }
}
