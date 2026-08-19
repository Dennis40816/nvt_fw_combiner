using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealNt51950GoldenCtrlRamPanelFitsProductionRail(bool useDarkTheme)
    {
        MainWindowViewModel viewModel = await Task.Run(
            () => CreateNt51950GoldenCtrlRamViewModelAsync(TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
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
            Width = 430,
            Height = 760,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = panel,
        };
        var host = new Window
        {
            Width = 430,
            Height = 760,
            RequestedThemeVariant = theme,
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
            host.Measure(new Size(430, 760));
            host.Arrange(new Rect(0, 0, 430, 760));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            MemoryCoverageGroupViewModel common = Assert.Single(
                viewModel.Replace.ReplaceCoverageGroups,
                group => group.RegionGroup == ReplaceRegionGroup.Common);
            Assert.Equal(3, common.Items.Count);
            Assert.Equal(
                1,
                panel.GetVisualDescendants().OfType<TextBlock>()
                    .Count(candidate => candidate.Text == "NF CtrlRAM" && candidate.Bounds.Width > 0));
            Assert.All(
                panel.GetVisualDescendants().OfType<Control>()
                    .Where(candidate => candidate.Bounds is { Width: > 0, Height: > 0 }),
                candidate => AssertControlFitsWidth(candidate, panel));

            using Avalonia.Media.Imaging.Bitmap? frame = host.GetLastRenderedFrame();
            Assert.NotNull(frame);
            string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                _ = Directory.CreateDirectory(outputDirectory);
                string themeName = useDarkTheme ? "dark" : "light";
                string outputPath = Path.Combine(
                    outputDirectory,
                    $"nfc-memory-ctrlram-golden-{themeName}.png");
                await using FileStream output = File.Create(outputPath);
                frame.Save(output);
            }
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>The fixed 430 px rail stacks technical ranges and leaves the optional action its own row.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void MemoryCoverageRowStacksRangeWithoutDetailsCompetitionAtFixedRailWidth(
        bool useDarkTheme)
    {
        ThemeVariant theme = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        var resources = new ResourceInclude(ProductionSharedTemplatesUri)
        {
            Source = ProductionSharedTemplatesUri,
        };
        Assert.True(resources.TryGetResource(
            "MemoryCoverageSegmentListTemplate",
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
            Content = rail,
        };
        var buttonStyles = new StyleInclude(ProductionButtonStylesUri)
        {
            Source = ProductionButtonStylesUri,
        };
        host.Styles.Add(buttonStyles);

        host.Measure(new Size(430, 1_000));
        host.Arrange(new Rect(0, 0, 430, host.DesiredSize.Height));

        TextBlock source = Assert.Single(
            row.GetVisualDescendants().OfType<TextBlock>(),
            candidate => candidate.Text == segment.SourceLabel);
        TextBlock address = Assert.Single(
            row.GetVisualDescendants().OfType<TextBlock>(),
            candidate => candidate.Text == segment.AddressRangeLabel);
        TextBlock length = Assert.Single(
            row.GetVisualDescendants().OfType<TextBlock>(),
            candidate => candidate.Text == segment.LengthLabel);
        TextBlock description = Assert.Single(
            row.GetVisualDescendants().OfType<TextBlock>(),
            candidate => candidate.Text == segment.PreservationSummary);
        Button details = Assert.Single(
            row.GetVisualDescendants().OfType<Button>(),
            candidate => Equals(candidate.Content, segment.DetailsLabel));
        Point sourceOrigin = Assert.IsType<Point>(source.TranslatePoint(default, row));
        Point addressOrigin = Assert.IsType<Point>(address.TranslatePoint(default, row));
        Point lengthOrigin = Assert.IsType<Point>(length.TranslatePoint(default, row));
        Point descriptionOrigin = Assert.IsType<Point>(description.TranslatePoint(default, row));
        Point detailsOrigin = Assert.IsType<Point>(details.TranslatePoint(default, row));

        Assert.True(addressOrigin.Y >= sourceOrigin.Y + source.Bounds.Height);
        Assert.Equal(sourceOrigin.X, addressOrigin.X, precision: 3);
        Assert.Equal(addressOrigin.Y, lengthOrigin.Y, precision: 3);
        Assert.True(lengthOrigin.X > addressOrigin.X);
        Assert.True(descriptionOrigin.Y >= addressOrigin.Y + address.Bounds.Height);
        Assert.True(detailsOrigin.Y >= descriptionOrigin.Y + description.Bounds.Height);
        Assert.True(detailsOrigin.X + details.Bounds.Width <= row.Bounds.Width);
        Assert.Equal(ProductionButtonStylesUri, buttonStyles.Source);
        Assert.NotNull(details.Theme);
        Assert.Null(details.FocusAdorner);
        Assert.Equal(34, details.MinHeight);

        Assert.True(resources.TryGetResource(
            "MemoryCoverageLogicalItemTemplate",
            theme,
            out object? logicalTemplateResource));
        IDataTemplate logicalTemplate = Assert.IsType<IDataTemplate>(logicalTemplateResource, exactMatch: false);
        var keptTail = new MemoryCoverageSegmentViewModel(
            "0x23700-0x2560F",
            "NF CtrlRAM",
            "Output range keeps bytes from the base firmware.",
            MemoryCoverageFillRole.CtrlRamNf,
            20,
            disposition: MemoryWorkflowDisposition.Kept,
            usesBaseFirmwarePattern: true,
            regionId: "nf-ctrlram",
            sourceSlotId: "replace-base",
            rangeStart: 0x23700,
            rangeEndExclusive: 0x25610,
            addressRangeLabel: "0x23700-0x2560F",
            lengthLabel: "len 0x1F10");
        var writtenHead = new MemoryCoverageSegmentViewModel(
            "0x22C00-0x236FF",
            "NF CtrlRAM",
            "Output range will be written from NF CtrlRAM.",
            MemoryCoverageFillRole.CtrlRamNf,
            20,
            disposition: MemoryWorkflowDisposition.WillReplace,
            regionId: "nf-ctrlram",
            sourceSlotId: "replace-ctrlram-nf",
            rangeStart: 0x22C00,
            rangeEndExclusive: 0x23700,
            addressRangeLabel: "0x22C00-0x236FF",
            lengthLabel: "len 0xB00");
        var logicalItem = new MemoryCoverageLogicalItemViewModel(
            "slot:replace-ctrlram-nf",
            [writtenHead, keptTail],
            ShellTextResources.For(ShellLanguage.English));
        var logicalRow = new ContentControl
        {
            Content = logicalItem,
            ContentTemplate = logicalTemplate,
        };
        var logicalHost = new Window
        {
            Width = 430,
            RequestedThemeVariant = theme,
            Content = new Border
            {
                Width = 430,
                Padding = new Thickness(18, 16, 20, 18),
                Child = logicalRow,
            },
        };

        logicalHost.Measure(new Size(430, 1_000));
        logicalHost.Arrange(new Rect(0, 0, 430, logicalHost.DesiredSize.Height));

        _ = Assert.Single(
            logicalRow.GetVisualDescendants().OfType<TextBlock>(),
            candidate => candidate.Text == "NF CtrlRAM" && candidate.Bounds.Width > 0);
        _ = Assert.Single(
            logicalRow.GetVisualDescendants().OfType<TextBlock>(),
            candidate => candidate.Text == "0x22C00-0x2560F");
        Assert.DoesNotContain(
            logicalRow.GetVisualDescendants().OfType<Label>(),
            candidate => Equals(candidate.Content, "2 ranges"));
        _ = Assert.Single(
            logicalRow.GetVisualDescendants().OfType<Border>(),
            candidate => candidate.Classes.Contains("memoryCoverageMarker"));
        Assert.DoesNotContain(
            logicalRow.GetVisualDescendants().OfType<ItemsControl>(),
            candidate => candidate.Bounds is { Width: > 0, Height: > 0 });
        Assert.True(logicalRow.Bounds.Width <= 392);

        var separatedTail = new MemoryCoverageSegmentViewModel(
            "0x26000-0x26FFF",
            "NF CtrlRAM",
            "A second non-contiguous range.",
            MemoryCoverageFillRole.CtrlRamNf,
            10,
            disposition: MemoryWorkflowDisposition.WillReplace,
            regionId: "nf-ctrlram-tail",
            sourceSlotId: "replace-ctrlram-nf",
            rangeStart: 0x26000,
            rangeEndExclusive: 0x27000,
            addressRangeLabel: "0x26000-0x26FFF",
            lengthLabel: "len 0x1000");
        var multiRangeItem = new MemoryCoverageLogicalItemViewModel(
            "slot:replace-ctrlram-nf",
            [writtenHead, separatedTail],
            ShellTextResources.For(ShellLanguage.English));
        var multiRangeRow = new ContentControl
        {
            Content = multiRangeItem,
            ContentTemplate = logicalTemplate,
        };
        var multiRangeHost = new Window
        {
            Width = 430,
            RequestedThemeVariant = theme,
            Content = new Border
            {
                Width = 430,
                Padding = new Thickness(18, 16, 20, 18),
                Child = multiRangeRow,
            },
        };
        multiRangeHost.Measure(new Size(430, 1_000));
        multiRangeHost.Arrange(new Rect(0, 0, 430, multiRangeHost.DesiredSize.Height));
        Assert.True(multiRangeItem.HasMultipleRanges);
        _ = Assert.Single(
            multiRangeRow.GetVisualDescendants().OfType<ItemsControl>(),
            candidate => candidate.Bounds is { Width: > 0, Height: > 0 });
        Assert.Equal(
            2,
            multiRangeRow.GetVisualDescendants().OfType<Border>()
                .Count(candidate => candidate.Classes.Contains("memoryCoverageMarker")));

        Assert.True(resources.TryGetResource(
            "MemoryCoverageGroupTemplate",
            theme,
            out object? groupTemplateResource));
        IDataTemplate groupTemplate = Assert.IsType<IDataTemplate>(groupTemplateResource, exactMatch: false);
        var group = new MemoryCoverageGroupViewModel(
            "Common",
            [logicalItem],
            isExpanded: true,
            ReplaceRegionGroup.Common,
            ShellTextResources.For(ShellLanguage.English));
        var groupControl = new ContentControl
        {
            Content = group,
            ContentTemplate = groupTemplate,
        };
        var groupHost = new Window
        {
            Width = 430,
            RequestedThemeVariant = theme,
            Content = groupControl,
        };
        groupHost.Styles.Add(new StyleInclude(ProductionMainWindowStylesUri)
        {
            Source = ProductionMainWindowStylesUri,
        });

        groupHost.Measure(new Size(430, 1_000));
        groupHost.Arrange(new Rect(0, 0, 430, groupHost.DesiredSize.Height));

        ToggleButton disclosure = Assert.Single(
            groupControl.GetVisualDescendants().OfType<ToggleButton>());
        ISolidColorBrush disclosureBackground = Assert.IsType<ISolidColorBrush>(
            disclosure.Background,
            exactMatch: false);
        Assert.Equal(Colors.Transparent, disclosureBackground.Color);

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
            "<StackPanel IsVisible=\"{Binding IsReplaceCoverageFlat}\"",
            ExtractDataTemplate(workflowTemplates, "ReplaceOutputLayoutPanelTemplate"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "MaxHeight=\"320\"",
            ExtractDataTemplate(sharedTemplates, "MergeOutputLayoutPanelTemplate"),
            StringComparison.Ordinal);
        Assert.Contains(
            "<Expander Classes=\"inlineDisclosure\"",
            ExtractDataTemplate(sharedTemplates, "FirmwareSlotGroupTemplate"),
            StringComparison.Ordinal);
        Assert.Contains(
            "<Expander Classes=\"inlineDisclosure\"",
            ExtractDataTemplate(sharedTemplates, "MemoryCoverageGroupTemplate"),
            StringComparison.Ordinal);
        string mainWindowStyles = ReadPresentationFile("Styles/MainWindowStyles.axaml");
        Assert.Contains(
            "Expander.inlineDisclosure /template/ ToggleButton",
            mainWindowStyles,
            StringComparison.Ordinal);
    }

    private static async Task<MainWindowViewModel> CreateNt51950GoldenCtrlRamViewModelAsync(
        CancellationToken cancellationToken)
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51950-fw200-single-auto-prj-676-20260717");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
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

        return viewModel;
    }

    private static void AssertControlFitsWidth(Control candidate, Control panel)
    {
        Point? origin = candidate.TranslatePoint(default, panel);
        if (origin is not { } point)
        {
            return;
        }

        Assert.True(
            point.X >= -0.5 && point.X + candidate.Bounds.Width <= panel.Bounds.Width + 0.5,
            $"{candidate.GetType().Name} exceeded the 430 px Memory Layout rail: " +
            $"x={point.X:F1}, width={candidate.Bounds.Width:F1}, panel={panel.Bounds.Width:F1}.");
    }
}
