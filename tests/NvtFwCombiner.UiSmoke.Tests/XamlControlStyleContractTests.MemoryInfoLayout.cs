using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    private static readonly Uri ProductionSharedTemplatesUri = new(
        "avares://NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowSharedTemplates.axaml");

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
    }
}
