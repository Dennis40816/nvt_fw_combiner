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

    /// <summary>The real NT51950 golden route keeps complete rows inside the responsive production panel.</summary>
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
        double railWidth, bool useDarkTheme, bool useTraditionalChinese)
    {
        using var workspace = TempWorkspace.Create("ctrlram-golden-panel");
        await using ReplaceOutputLayoutTestHost host = await ReplaceOutputLayoutTestHost.CreateAsync(workspace,
            width: railWidth == 430 ? 1280 : 960, height: 1000,
            dark: useDarkTheme, chinese: useTraditionalChinese);
        await LoadNt51950GoldenCtrlRamInputsAsync(host.Shell, TestContext.Current.CancellationToken);
        await host.SettleAsync();
        OutputLayoutPanel panel = host.Panel;
        OutputLayoutPanelModel model = host.Model;
        Assert.True(host.Shell.Replace.HasCtrlRamFocusLayout);
        Assert.Same(host.Shell.Replace.OutputLayout.Current.Panel, panel.Model);
        Assert.NotEmpty(model.LaneLinks);
        Assert.Contains(model.RowSegments, static segment => segment.CtrlRamRegionRole == CtrlRamRegionRole.Nf);
        Assert.All(panel.GetVisualDescendants().OfType<Control>()
            .Where(static control => control.IsEffectivelyVisible && control.Bounds is { Width: > 0, Height: > 0 }),
            control => AssertControlFitsWidth(control, panel));
        AssertVisibleTextDoesNotOverlap(panel);
        Canvas bar = panel.FindControl<Canvas>("LayoutBar")!;
        Assert.NotEmpty(bar.Children);
        Assert.InRange(bar.Bounds.Height, 33.5, 34.5);
        Assert.True(bar.Bounds.Width > 300d);
        Assert.All(model.LaneLinks, static link => Assert.NotNull(link.Zoom));
        MemoryCoverageSegmentViewModel nf = model.RowSegments.First(
            static segment => segment.CtrlRamRegionRole == CtrlRamRegionRole.Nf && segment.IsSelectedForWrite);
        host.AssertDetails(nf);
        host.Capture($"nfc-memory-ctrlram-golden-{railWidth:F0}-{(useDarkTheme ? "dark" : "light")}-{(useTraditionalChinese ? "zh-tw" : "en")}");
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
        Assert.DoesNotContain("ReplaceOutputLayoutPanelTemplate", workflowTemplates, StringComparison.Ordinal);
        Assert.DoesNotContain("<views:MemoryCoverageBar", workflowTemplates, StringComparison.Ordinal);
        XElement slotGroupTemplate = Assert.Single(XDocument.Parse(sharedTemplates).Descendants(),
            element => element.Name.LocalName == "DataTemplate" &&
                element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "FirmwareSlotGroupTemplate"));
        _ = Assert.Single(slotGroupTemplate.Descendants(),
            element => element.Name.LocalName == "Expander" &&
                ((string?)element.Attribute("Classes"))?.Split(' ').Contains("inlineDisclosure", StringComparer.Ordinal) == true);
        Assert.DoesNotContain("ReplaceBaseCoverageGroup.IsExpanded", workflowTemplates, StringComparison.Ordinal);
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
