using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    private static readonly Uri ProductionButtonStylesUri = new(
        "avares://NvtFwCombiner.Presentation.Avalonia/Styles/MainWindowButtonStyles.axaml");
    private static readonly Uri ProductionVisualStylesUri = new(
        "avares://NvtFwCombiner.Presentation.Avalonia/Styles/MainWindowVisualStyles.axaml");

    /// <summary>Selected-file facts use the full card body while the localized picker stays in the header.</summary>
    [Fact]
    public void FirmwareSlotCardUsesAResponsiveFullWidthFactBody()
    {
        string slotCard = ReadPresentationFile("Views/FirmwareSlotCard.axaml");
        string codeBehind = ReadPresentationFile("Views/FirmwareSlotCard.axaml.cs");

        var document = XDocument.Parse(slotCard);
        XElement layout = Assert.Single(document.Descendants(), element =>
            HasXamlName(element, "SlotLayout"));
        XElement header = Assert.Single(document.Descendants(), element =>
            HasXamlName(element, "SlotHeaderContent"));
        XElement body = Assert.Single(document.Descendants(), element =>
            HasXamlName(element, "SlotBody"));
        XElement browse = Assert.Single(document.Descendants(), element =>
            HasXamlName(element, "BrowseButton"));
        XElement primaryFacts = Assert.Single(document.Descendants(), element =>
            HasXamlName(element, "PrimaryFirmwareFactsHost"));
        XElement additionalFacts = Assert.Single(document.Descendants(), element =>
            HasXamlName(element, "AdditionalFirmwareFactsHost"));

        Assert.Equal("*,Auto", (string?)layout.Attribute("ColumnDefinitions"));
        Assert.Equal("Auto,Auto", (string?)layout.Attribute("RowDefinitions"));
        Assert.Equal("Grid", header.Name.LocalName);
        Assert.Equal("Auto,*", (string?)header.Attribute("ColumnDefinitions"));
        Assert.Equal("1", (string?)body.Attribute("Grid.Row"));
        Assert.Null(body.Attribute("Grid.ColumnSpan"));
        Assert.Equal("1", (string?)browse.Attribute("Grid.Column"));
        Assert.Equal("2", (string?)browse.Attribute("Grid.RowSpan"));
        Assert.Equal("Center", (string?)browse.Attribute("VerticalAlignment"));
        XElement[] browseLabels = [.. browse.Descendants().Where(element =>
            element.Name.LocalName == "MultiBinding")];
        Assert.Equal(2, browseLabels.Length);
        Assert.All(browseLabels, binding =>
        {
            Assert.Equal(
                "{x:Static views:FirmwareSlotCard.BrowseActionFormat}",
                (string?)binding.Attribute("StringFormat"));
            Assert.Contains(binding.Descendants(), element =>
                element.Name.LocalName == "Binding" &&
                (string?)element.Attribute("ElementName") == "Root" &&
                (string?)element.Attribute("Path") == "BrowseLabel");
            Assert.Contains(binding.Descendants(), element =>
                element.Name.LocalName == "Binding" &&
                (string?)element.Attribute("Path") == "Title");
        });
        Assert.Null(primaryFacts.Attribute("MaxWidth"));
        Assert.Null(additionalFacts.Attribute("MaxWidth"));
        Assert.DoesNotContain("\"Select BIN file\"", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("slot.Description", codeBehind, StringComparison.Ordinal);
        Assert.Contains(
            "FormatBrowseActionLabel(BrowseLabel, slot.Title)",
            codeBehind,
            StringComparison.Ordinal);
    }

    /// <summary>The visible, assistive, and picker-title Browse phrase has one language-neutral formatter.</summary>
    [Theory]
    [InlineData("Browse", "Initial Code replacement BIN", "Browse — Initial Code replacement BIN")]
    [InlineData("瀏覽", "Initial Code 取代 BIN 檔案", "瀏覽 — Initial Code 取代 BIN 檔案")]
    public void FirmwareSlotBrowseActionUsesTheLocalizedActionAndTitle(
        string browseLabel,
        string slotTitle,
        string expected)
    {
        Assert.Equal(
            expected,
            FirmwareSlotCard.FormatBrowseActionLabel(browseLabel, slotTitle));
    }

    /// <summary>The supported compact card widths keep Browse in bounds and give facts the complete body width.</summary>
    [AvaloniaTheory]
    [InlineData(320)]
    [InlineData(480)]
    [InlineData(900)]
    public void FirmwareSlotCardFitsSupportedCompactWidths(double width)
    {
        var slot = new FirmwareSlotViewModel(
            "dp",
            "DP BIN",
            "Select DP firmware",
            FirmwareSlotKind.Dp)
        {
            FilePath = @"C:\firmware\dp.bin",
        };
        slot.SetInputInspection(FirmwareInputInspectionSeverity.Valid, "The selected BIN is valid.");
        slot.SetFirmwareFacts(
        [
            new("DP", "DCC-00"),
            new("Jira", "AUTO_PRJ-576"),
            new("Common FW", "2.0.0"),
            new("PID", "0x135E"),
        ]);
        var card = new FirmwareSlotCard
        {
            BrowseLabel = "Browse",
            DataContext = slot,
            Width = width,
        };
        (Window styleHost, StyleInclude buttonStyles, StyleInclude visualStyles) =
            HostWithProductionFirmwareSlotStyles(card);

        styleHost.Measure(new Size(width, 1_000));
        card.Measure(new Size(width, 1_000));
        card.Arrange(new Rect(0, 0, width, card.DesiredSize.Height));

        Grid layout = Assert.IsType<Grid>(card.FindControl<Control>("SlotLayout"));
        Grid header = Assert.IsType<Grid>(card.FindControl<Control>("SlotHeaderContent"));
        TextBlock title = Assert.IsType<TextBlock>(card.FindControl<Control>("SlotTitle"));
        Button browse = Assert.IsType<Button>(card.FindControl<Control>("BrowseButton"));
        StackPanel body = Assert.IsType<StackPanel>(card.FindControl<Control>("SlotBody"));
        Border selector = Assert.Single(
            card.GetVisualDescendants().OfType<Border>(),
            candidate => candidate.Classes.Contains("firmwareSlot"));
        ItemsControl primaryFacts = Assert.IsType<ItemsControl>(
            card.FindControl<Control>("PrimaryFirmwareFactsHost"));
        Point browseOrigin = Assert.IsType<Point>(browse.TranslatePoint(default, selector));
        ToggleButton state = Assert.Single(
            card.GetVisualDescendants().OfType<ToggleButton>(),
            candidate => candidate.Classes.Contains("slotStateAction"));
        Point titleOrigin = Assert.IsType<Point>(title.TranslatePoint(default, header));
        Point stateOrigin = Assert.IsType<Point>(state.TranslatePoint(default, header));

        Assert.True(layout.Bounds.Width <= width);
        Assert.True(browse.Bounds.Right <= layout.Bounds.Width);
        Assert.True(body.Bounds.Width < layout.Bounds.Width);
        Assert.Equal(body.Bounds.Width, primaryFacts.Bounds.Width, precision: 3);
        double selectorCenter = selector.Bounds.Height / 2;
        double browseCenter = browseOrigin.Y + (browse.Bounds.Height / 2);
        Assert.InRange(Math.Abs(selectorCenter - browseCenter), 0, 0.5);
        if (width >= 480)
        {
            double titleCenter = titleOrigin.Y + (title.Bounds.Height / 2);
            double stateCenter = stateOrigin.Y + (state.Bounds.Height / 2);
            Assert.InRange(Math.Abs(titleCenter - stateCenter), 0, 0.5);
            Assert.True(
                title.Bounds.Width < 100,
                $"Title width {title.Bounds.Width}, desired {title.DesiredSize.Width}, state X {stateOrigin.X}.");
            Assert.Equal(
                8,
                stateOrigin.X - (titleOrigin.X + title.Bounds.Width),
                precision: 3);
        }
        Assert.Equal(88, browse.MinWidth);
        Assert.Equal(36, browse.MinHeight);
        Assert.Equal(36, browse.MaxHeight);
        Assert.Equal(Avalonia.Layout.HorizontalAlignment.Center, browse.HorizontalContentAlignment);
        Assert.Equal(Avalonia.Layout.VerticalAlignment.Center, browse.VerticalContentAlignment);
        Assert.True(browse.Bounds.Width >= 88);
        Assert.Equal(36, browse.Bounds.Height, precision: 3);
        Assert.Null(browse.FocusAdorner);
        Assert.NotNull(browse.Theme);
        browse.ApplyTemplate();
        ContentPresenter presenter = Assert.Single(
            browse.GetVisualDescendants().OfType<ContentPresenter>(),
            candidate => candidate.Name == "PART_ContentPresenter");
        Border slotIcon = Assert.Single(
            card.GetVisualDescendants().OfType<Border>(),
            candidate => candidate.Classes.Contains("slotTypeIcon"));
        Assert.Equal(new CornerRadius(999), presenter.CornerRadius);
        Assert.Equal(30, slotIcon.Bounds.Width, precision: 3);
        Assert.Equal(30, slotIcon.Bounds.Height, precision: 3);
        Assert.Equal(ProductionButtonStylesUri, buttonStyles.Source);
        Assert.Equal(ProductionVisualStylesUri, visualStyles.Source);

        slot.SetSelectionReadiness(
            ResolvedChildReadiness.PendingInput,
            "Waiting for DP BIN",
            "Select and verify the DP BIN first.",
            "Waiting for DP BIN: Select and verify the DP BIN first.",
            canSelect: false);
        Assert.False(browse.IsEnabled);
        Assert.Equal("Waiting for DP BIN", slot.SemanticStateLabel);

        slot.SetSelectionReadiness(
            ResolvedChildReadiness.Ready,
            "Ready",
            "This input can now be selected.",
            "Ready: This input can now be selected.");
        Assert.True(browse.IsEnabled);
        Assert.NotEqual("Checking", slot.SemanticStateLabel);
    }

    /// <summary>Real long English and Chinese slot titles wrap inside the 320 px header without entering Browse.</summary>
    [AvaloniaTheory]
    [InlineData("Browse", "Initial Code replacement BIN")]
    [InlineData("瀏覽", "Initial Code 取代 BIN 檔案")]
    public void FirmwareSlotLongLocalizedTitleDoesNotIntersectBrowseAt320Pixels(
        string browseLabel,
        string slotTitle)
    {
        const double width = 320;
        var card = new FirmwareSlotCard
        {
            BrowseLabel = browseLabel,
            DataContext = new FirmwareSlotViewModel(
                "initial-code",
                slotTitle,
                "unused guidance",
                FirmwareSlotKind.Dp),
            Width = width,
        };
        (Window styleHost, _, _) = HostWithProductionFirmwareSlotStyles(card);

        styleHost.Measure(new Size(width, 1_000));
        card.Measure(new Size(width, 1_000));
        card.Arrange(new Rect(0, 0, width, card.DesiredSize.Height));

        Grid layout = Assert.IsType<Grid>(card.FindControl<Control>("SlotLayout"));
        TextBlock title = Assert.IsType<TextBlock>(card.FindControl<Control>("SlotTitle"));
        Button browse = Assert.IsType<Button>(card.FindControl<Control>("BrowseButton"));
        Point titleOrigin = Assert.IsType<Point>(title.TranslatePoint(default, layout));
        Point browseOrigin = Assert.IsType<Point>(browse.TranslatePoint(default, layout));
        var titleRect = new Rect(titleOrigin, title.Bounds.Size);
        var browseRect = new Rect(browseOrigin, browse.Bounds.Size);

        Assert.False(titleRect.Intersects(browseRect));
        Assert.True(titleRect.Right <= browseRect.Left);
        Assert.True(browseRect.Right <= layout.Bounds.Width);
        Assert.Equal(slotTitle, title.Text);
        string expectedBrowseAction = FirmwareSlotCard.FormatBrowseActionLabel(
            browseLabel,
            slotTitle);
        Assert.Equal(expectedBrowseAction, AutomationProperties.GetName(browse));
        Assert.Equal(expectedBrowseAction, ToolTip.GetTip(browse));
    }

    /// <summary>Real compiled DP Replace slots relocalize their copy and Browse-facing strings.</summary>
    [AvaloniaFact]
    public async Task DpReplaceSlotsRelocalizeTypedInitialCodeAndLdcBrowseActions()
    {
        (MainWindowViewModel dpViewModel, MainWindowViewModel nt51928ViewModel, FirmwareSlotViewModel dp,
            FirmwareSlotViewModel initialCode, FirmwareSlotViewModel ldc) =
            await Task.Run(
                () =>
                {
                    MainWindowViewModel createdDpViewModel = PresentationTestHost.CreateViewModel();
                    createdDpViewModel.WorkflowSession.SelectedIc = "NT51927";
                    OpenReplace(createdDpViewModel, ExperienceIds.DpReplace);
                    FirmwareSlotViewModel createdDp = Assert.Single(
                        createdDpViewModel.Replace.ReplaceSlots,
                        slot => slot.AddressSpaceId == CompositionAddressSpaceIds.DpReplacement);
                    MainWindowViewModel createdNt51928ViewModel = PresentationTestHost.CreateViewModel();
                    createdNt51928ViewModel.WorkflowSession.SelectedIc = "NT51928";
                    OpenReplace(createdNt51928ViewModel, ExperienceIds.DpReplace);
                    FirmwareSlotViewModel createdInitialCode = Assert.Single(
                        createdNt51928ViewModel.Replace.ReplaceSlots,
                        slot => slot.AddressSpaceId == CompositionAddressSpaceIds.InitialCodeReplacement);
                    FirmwareSlotViewModel createdLdc = Assert.Single(
                        createdNt51928ViewModel.Replace.ReplaceSlots,
                        slot => slot.AddressSpaceId == CompositionAddressSpaceIds.LdcReplacement);
                    createdDpViewModel.SelectedLanguage = "Traditional Chinese";
                    createdNt51928ViewModel.SelectedLanguage = "Traditional Chinese";
                    return (
                        createdDpViewModel,
                        createdNt51928ViewModel,
                        createdDp,
                        createdInitialCode,
                        createdLdc);
                },
                TestContext.Current.CancellationToken);

        Assert.Equal("DP 取代 BIN 檔案", dp.Title);
        Assert.Equal("由編譯後的 DP Replace 設定檔宣告之 DP 取代資料。", dp.Description);
        Assert.Equal("Initial Code 取代 BIN 檔案", initialCode.Title);
        Assert.Equal("由編譯後的 DP Replace 設定檔宣告之 Initial Code 資料。", initialCode.Description);
        Assert.Equal("LDC 取代 BIN 檔案", ldc.Title);
        Assert.Equal("由編譯後的 DP Replace 設定檔宣告之 LDC 資料。", ldc.Description);
        Assert.DoesNotContain("profile", dp.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("profile", initialCode.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("profile", ldc.Description, StringComparison.OrdinalIgnoreCase);
        AssertBrowseAction(dp, dpViewModel.Text);
        AssertBrowseAction(initialCode, nt51928ViewModel.Text);
        AssertBrowseAction(ldc, nt51928ViewModel.Text);

        dpViewModel.SelectedLanguage = "English";
        nt51928ViewModel.SelectedLanguage = "English";

        Assert.Equal("DP replacement BIN", dp.Title);
        Assert.Equal(
            "Replacement DP payload declared by the compiled DP Replace profile.",
            dp.Description);
        Assert.Equal("Initial Code replacement BIN", initialCode.Title);
        Assert.Equal(
            "Initial Code payload declared by the compiled DP Replace profile.",
            initialCode.Description);
        Assert.Equal("LDC replacement BIN", ldc.Title);
        Assert.Equal(
            "LDC payload declared by the compiled DP Replace profile.",
            ldc.Description);
        AssertBrowseAction(dp, dpViewModel.Text);
        AssertBrowseAction(initialCode, nt51928ViewModel.Text);
        AssertBrowseAction(ldc, nt51928ViewModel.Text);
    }

    /// <summary>Real typed CtrlRAM groups localize every group label without changing technical identifiers.</summary>
    [AvaloniaFact]
    public async Task CtrlRamSlotRelocalizesTypedRegionBrowseAction()
    {
        (MainWindowViewModel topologyViewModel, MainWindowViewModel cascadeViewModel,
            FirmwareSlotViewModel[] topologySlots, FirmwareSlotViewModel cascadeSlot,
            Dictionary<FirmwareSlotViewModel, (string Title, string Description)> english) =
            await Task.Run(
                () =>
                {
                    MainWindowViewModel createdTopologyViewModel = PresentationTestHost.CreateViewModel();
                    createdTopologyViewModel.WorkflowSession.SelectedIc = "NT51927";
                    createdTopologyViewModel.WorkflowSession.SelectedNumber = "3";
                    OpenReplace(createdTopologyViewModel, ExperienceIds.CtrlRamReplace);
                    FirmwareSlotViewModel[] createdTopologySlots =
                    [
                        Assert.Single(createdTopologyViewModel.Replace.ReplaceSlots, candidate =>
                            candidate.RegionGroup == ReplaceRegionGroup.Master &&
                            candidate.Title == "Normal CtrlRAM (Master)"),
                        Assert.Single(createdTopologyViewModel.Replace.ReplaceSlots, candidate =>
                            candidate.RegionGroup == ReplaceRegionGroup.SlaveRight &&
                            candidate.Title == "Normal CtrlRAM (Slave R)"),
                        Assert.Single(createdTopologyViewModel.Replace.ReplaceSlots, candidate =>
                            candidate.RegionGroup == ReplaceRegionGroup.SlaveLeft &&
                            candidate.Title == "Normal CtrlRAM (Slave L)"),
                        Assert.Single(createdTopologyViewModel.Replace.ReplaceSlots, candidate =>
                            candidate.RegionGroup == ReplaceRegionGroup.Common &&
                            candidate.Title == "NF CtrlRAM (Shared)"),
                    ];
                    MainWindowViewModel createdCascadeViewModel = PresentationTestHost.CreateViewModel();
                    createdCascadeViewModel.WorkflowSession.SelectedIc = "NT51950";
                    createdCascadeViewModel.WorkflowSession.SelectedNumber = "cascade";
                    OpenReplace(createdCascadeViewModel, ExperienceIds.CtrlRamReplace);
                    FirmwareSlotViewModel createdCascadeSlot = Assert.Single(
                        createdCascadeViewModel.Replace.ReplaceSlots,
                        candidate => candidate.RegionGroup == ReplaceRegionGroup.Cascade &&
                            candidate.SlotId == "replace-ctrlram-diff");
                    FirmwareSlotViewModel[] createdSlots = [.. createdTopologySlots, createdCascadeSlot];
                    Dictionary<FirmwareSlotViewModel, (string Title, string Description)> createdEnglish =
                        createdSlots.ToDictionary(
                            static slot => slot,
                            static slot => (slot.Title, slot.Description));
                    createdTopologyViewModel.SelectedLanguage = "Traditional Chinese";
                    createdCascadeViewModel.SelectedLanguage = "Traditional Chinese";
                    return (
                        createdTopologyViewModel,
                        createdCascadeViewModel,
                        createdTopologySlots,
                        createdCascadeSlot,
                        createdEnglish);
                },
                TestContext.Current.CancellationToken);

        FirmwareSlotViewModel master = topologySlots[0];
        FirmwareSlotViewModel slaveRight = topologySlots[1];
        FirmwareSlotViewModel slaveLeft = topologySlots[2];
        FirmwareSlotViewModel shared = topologySlots[3];
        Assert.Equal("Normal CtrlRAM（主控）", master.Title);
        AssertCtrlRamDescriptionIsFullyLocalized(master, "Normal CtrlRAM（主控）：上限 ");
        Assert.Equal("Normal CtrlRAM（右側從屬）", slaveRight.Title);
        AssertCtrlRamDescriptionIsFullyLocalized(slaveRight, "Normal CtrlRAM（右側從屬）：上限 ");
        Assert.Equal("Normal CtrlRAM（左側從屬）", slaveLeft.Title);
        AssertCtrlRamDescriptionIsFullyLocalized(slaveLeft, "Normal CtrlRAM（左側從屬）：上限 ");
        Assert.Equal("NF CtrlRAM（共用）", shared.Title);
        AssertCtrlRamDescriptionIsFullyLocalized(
            shared,
            "NF CtrlRAM（主控）：上限 ",
            "NF CtrlRAM（右側從屬）：上限 ",
            "NF CtrlRAM（左側從屬）：上限 ");
        Assert.Equal("DiffDLM", cascadeSlot.Title);
        AssertCtrlRamDescriptionIsFullyLocalized(cascadeSlot, "DiffDLM.bin");
        foreach (FirmwareSlotViewModel slot in topologySlots)
        {
            AssertBrowseAction(slot, topologyViewModel.Text);
        }

        AssertBrowseAction(cascadeSlot, cascadeViewModel.Text);

        topologyViewModel.SelectedLanguage = "English";
        cascadeViewModel.SelectedLanguage = "English";

        foreach ((FirmwareSlotViewModel slot, (string Title, string Description) snapshot) in english)
        {
            Assert.Equal(snapshot.Title, slot.Title);
            Assert.Equal(snapshot.Description, slot.Description);
        }

        foreach (FirmwareSlotViewModel slot in topologySlots)
        {
            AssertBrowseAction(slot, topologyViewModel.Text);
        }

        AssertBrowseAction(cascadeSlot, cascadeViewModel.Text);
    }

    /// <summary>Shell headers stay readable at compact width and vertically aligned while startup is running.</summary>
    [Fact]
    public void StartupPreparationHeaderCentersLabelsAndRightAlignsPercentage()
    {
        var shell = XDocument.Parse(ReadPresentationFile("MainWindow.axaml"));
        XElement replaceHeader = Assert.Single(shell.Descendants(), element =>
            HasXamlName(element, "ReplacePageHeader"));
        XElement mergeHeader = Assert.Single(shell.Descendants(), element =>
            HasXamlName(element, "MergePageHeader"));
        Assert.Equal("Auto,Auto", (string?)replaceHeader.Attribute("RowDefinitions"));
        Assert.Equal("Auto,Auto", (string?)mergeHeader.Attribute("RowDefinitions"));

        XElement host = Assert.Single(shell.Descendants(), element =>
            HasXamlName(element, "OptionalPreloadStatusHost"));
        XElement[] centeredElements =
        [
            Assert.Single(host.Descendants(), element =>
                (string?)element.Attribute("Text") ==
                "{ReflectionBinding $parent[Window].DataContext.Text.PreloadStatusTitle}"),
            Assert.Single(host.Descendants(), element =>
                (string?)element.Attribute("Text") == "{ReflectionBinding SummaryStage.PositionLabel}"),
            Assert.Single(host.Descendants(), element =>
                (string?)element.Attribute("Grid.Column") == "2" &&
                element.Name.LocalName == "StackPanel" &&
                (string?)element.Attribute("Orientation") == "Horizontal"),
            Assert.Single(host.Descendants(), element =>
                (string?)element.Attribute("Text") == "{ReflectionBinding SummaryStage.ProgressLabel}"),
        ];

        Assert.All(centeredElements, element =>
            Assert.Equal("Center", (string?)element.Attribute("VerticalAlignment")));
        XElement percentage = centeredElements[^1];
        Assert.Equal("44", (string?)percentage.Attribute("MinWidth"));
        Assert.Equal("Right", (string?)percentage.Attribute("TextAlignment"));
    }

    private static bool HasXamlName(XElement element, string name)
    {
        return element.Attributes().Any(attribute =>
            attribute.Name.LocalName == "Name" && attribute.Value == name);
    }

    private static void AssertBrowseAction(
        FirmwareSlotViewModel slot,
        ShellTextResources text)
    {
        var card = new FirmwareSlotCard
        {
            BrowseLabel = text.BrowseLabel,
            DataContext = slot,
            Width = 320,
        };
        (Window styleHost, _, _) = HostWithProductionFirmwareSlotStyles(card);
        styleHost.Measure(new Size(320, 1_000));
        card.Measure(new Size(320, 1_000));
        card.Arrange(new Rect(0, 0, 320, card.DesiredSize.Height));

        Button browse = Assert.IsType<Button>(card.FindControl<Control>("BrowseButton"));
        string pickerTitle = FirmwareSlotCard.FormatBrowseActionLabel(text.BrowseLabel, slot.Title);
        Assert.Equal($"{text.BrowseLabel} — {slot.Title}", pickerTitle);
        Assert.Equal(pickerTitle, AutomationProperties.GetName(browse));
        Assert.Equal(pickerTitle, ToolTip.GetTip(browse));
    }

    private static void AssertCtrlRamDescriptionIsFullyLocalized(
        FirmwareSlotViewModel slot,
        params string[] expectedFragments)
    {
        Assert.Contains("：上限 ", slot.Description, StringComparison.Ordinal);
        Assert.DoesNotContain(": max ", slot.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("(Master)", slot.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("(Slave R)", slot.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("(Slave L)", slot.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("(Shared)", slot.Description, StringComparison.Ordinal);
        Assert.All(expectedFragments, expected =>
            Assert.Contains(expected, slot.Description, StringComparison.Ordinal));
    }

    private static (Window Host, StyleInclude ButtonStyles, StyleInclude VisualStyles)
        HostWithProductionFirmwareSlotStyles(FirmwareSlotCard card)
    {
        var buttonStyles = new StyleInclude(ProductionButtonStylesUri)
        {
            Source = ProductionButtonStylesUri,
        };
        var visualStyles = new StyleInclude(ProductionVisualStylesUri)
        {
            Source = ProductionVisualStylesUri,
        };
        var host = new Window();
        host.Styles.Add(buttonStyles);
        host.Styles.Add(visualStyles);
        host.Content = card;
        return (host, buttonStyles, visualStyles);
    }

}
