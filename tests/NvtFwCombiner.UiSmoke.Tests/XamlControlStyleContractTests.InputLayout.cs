using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
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
    private static readonly Uri ProductionFirmwareSlotStylesUri = new(
        "avares://NvtFwCombiner.Presentation.Avalonia/Styles/FirmwareSlotExperienceStyles.axaml");

    /// <summary>Selected-file facts use one fixed identity/four-column-facts/Browse anatomy.</summary>
    [Fact]
    public void FirmwareSlotCardUsesApprovedFixedFourColumnFactLayout()
    {
        string slotCard = ReadPresentationFile("Views/FirmwareSlotCard.axaml");
        string codeBehind = ReadPresentationFile("Views/FirmwareSlotCard.axaml.cs");
        string buttonStyles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");

        var document = XDocument.Parse(slotCard);
        XElement layout = Assert.Single(document.Descendants(), element =>
            HasXamlName(element, "SlotLayout"));
        XElement header = Assert.Single(document.Descendants(), element =>
            HasXamlName(element, "SlotHeaderContent"));
        XElement identity = Assert.Single(document.Descendants(), element =>
            HasXamlName(element, "SlotIdentity"));
        XElement factsRegion = Assert.Single(document.Descendants(), element =>
            HasXamlName(element, "SlotFactsRegion"));
        XElement browse = Assert.Single(document.Descendants(), element =>
            HasXamlName(element, "BrowseButton"));
        XElement clear = Assert.Single(document.Descendants(), element =>
            HasXamlName(element, "ClearButton"));
        XElement actions = Assert.Single(document.Descendants(), element =>
            HasXamlName(element, "SlotActions"));
        XElement primaryFacts = Assert.Single(document.Descendants(), element =>
            HasXamlName(element, "PrimaryFirmwareFactsHost"));
        XElement additionalFacts = Assert.Single(document.Descendants(), element =>
            HasXamlName(element, "AdditionalFirmwareFactsHost"));

        Assert.Equal("280,*,Auto", (string?)layout.Attribute("ColumnDefinitions"));
        Assert.Equal("*", (string?)layout.Attribute("RowDefinitions"));
        Assert.Equal("16,12", (string?)layout.Attribute("Margin"));
        Assert.Equal("72", (string?)layout.Attribute("MinHeight"));
        Assert.Equal("{DynamicResource NfcSpace8}", (string?)identity.Attribute("Spacing"));
        Assert.Equal("Grid", header.Name.LocalName);
        Assert.Equal("Auto,*", (string?)header.Attribute("ColumnDefinitions"));
        Assert.Equal("0", (string?)identity.Attribute("Grid.Column"));
        Assert.Equal("1", (string?)factsRegion.Attribute("Grid.Column"));
        Assert.Equal("Center", (string?)factsRegion.Attribute("VerticalAlignment"));
        Assert.Equal("2", (string?)actions.Attribute("Grid.Column"));
        Assert.Equal("10", (string?)actions.Attribute("Spacing"));
        Assert.Equal("Center", (string?)actions.Attribute("VerticalAlignment"));
        Assert.Null(browse.Attribute("Grid.Column"));
        Assert.Null(browse.Attribute("Grid.RowSpan"));
        Assert.Equal("Center", (string?)browse.Attribute("VerticalAlignment"));
        Assert.Equal([browse, clear], actions.Elements());
        Assert.Null(clear.Attribute("IsVisible"));
        Assert.Equal("{Binding HasFile}", (string?)clear.Attribute("IsEnabled"));
        Assert.Equal(
            "{Binding ClearSelectionCommand, ElementName=Root}",
            (string?)clear.Attribute("Command"));
        Assert.Equal("{Binding SlotId}", (string?)clear.Attribute("CommandParameter"));
        Assert.Null(clear.Attribute("Click"));
        Assert.Contains("slotClearAction", (string?)clear.Attribute("Classes"), StringComparison.Ordinal);
        Assert.Contains("danger", (string?)clear.Attribute("Classes"), StringComparison.Ordinal);
        Assert.Contains(primaryFacts.Descendants(), element =>
            element.Name.LocalName == "UniformGrid" &&
            (string?)element.Attribute("Columns") == "{Binding FactColumnCount, ElementName=Root}" &&
            element.Attribute("Rows") is null);
        Assert.Contains(additionalFacts.Descendants(), element =>
            element.Name.LocalName == "UniformGrid" &&
            (string?)element.Attribute("Columns") == "{Binding FactColumnCount, ElementName=Root}" &&
            element.Attribute("Rows") is null);
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
        XElement[] clearLabels = [.. clear.Descendants().Where(element =>
            element.Name.LocalName == "MultiBinding")];
        Assert.Equal(2, clearLabels.Length);
        Assert.All(clearLabels, binding =>
        {
            Assert.Equal(
                "{x:Static views:FirmwareSlotCard.ClearActionFormat}",
                (string?)binding.Attribute("StringFormat"));
            Assert.Contains(binding.Descendants(), element =>
                element.Name.LocalName == "Binding" &&
                (string?)element.Attribute("ElementName") == "Root" &&
                (string?)element.Attribute("Path") == "ClearSelectionLabel");
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
        Assert.Contains(
            "<Style Selector=\"Button.danger:disabled /template/ ContentPresenter#PART_ContentPresenter\">",
            buttonStyles,
            StringComparison.Ordinal);
        Assert.Contains(
            "<Setter Property=\"TextElement.Foreground\" Value=\"{DynamicResource NfcTextDisabledBrush}\" />",
            buttonStyles,
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

    /// <summary>Supported window widths keep four equal left-filled fact columns and a shared center axis.</summary>
    [AvaloniaTheory]
    [InlineData(900)]
    [InlineData(1180)]
    public void FirmwareSlotCardKeepsApprovedFourColumnGeometry(double width)
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
            new("DP Version", "DCC-00"),
            new("Jira Index", "AUTO_PRJ-576"),
            new("Common FW Version", "2.0.0"),
            new("PID", "0x135E"),
        ]);
        var card = new FirmwareSlotCard
        {
            BrowseLabel = "Browse",
            ClearSelectionLabel = "Clear selected file",
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
        Button clear = Assert.IsType<Button>(card.FindControl<Control>("ClearButton"));
        StackPanel actions = Assert.IsType<StackPanel>(card.FindControl<Control>("SlotActions"));
        StackPanel identity = Assert.IsType<StackPanel>(card.FindControl<Control>("SlotIdentity"));
        StackPanel factsRegion = Assert.IsType<StackPanel>(card.FindControl<Control>("SlotFactsRegion"));
        Border selector = Assert.Single(
            card.GetVisualDescendants().OfType<Border>(),
            candidate => candidate.Classes.Contains("firmwareSlot"));
        ItemsControl primaryFacts = Assert.IsType<ItemsControl>(
            card.FindControl<Control>("PrimaryFirmwareFactsHost"));
        Point browseOrigin = Assert.IsType<Point>(browse.TranslatePoint(default, selector));
        Point factsOrigin = Assert.IsType<Point>(factsRegion.TranslatePoint(default, selector));
        ToggleButton state = Assert.Single(
            card.GetVisualDescendants().OfType<ToggleButton>(),
            candidate => candidate.Classes.Contains("slotStateAction"));
        Point titleOrigin = Assert.IsType<Point>(title.TranslatePoint(default, header));
        Point stateOrigin = Assert.IsType<Point>(state.TranslatePoint(default, header));

        Assert.True(layout.Bounds.Width <= width);
        Assert.True(browse.Bounds.Right <= layout.Bounds.Width);
        Assert.Equal(280, identity.Bounds.Width, precision: 3);
        Assert.Equal(factsRegion.Bounds.Width, primaryFacts.Bounds.Width, precision: 3);
        double browseCenter = browseOrigin.Y + (browse.Bounds.Height / 2);
        double factsCenter = factsOrigin.Y + (factsRegion.Bounds.Height / 2);
        Assert.InRange(Math.Abs(factsCenter - browseCenter), 0, 0.5);
        double titleCenter = titleOrigin.Y + (title.Bounds.Height / 2);
        double stateCenter = stateOrigin.Y + (state.Bounds.Height / 2);
        Assert.InRange(Math.Abs(titleCenter - stateCenter), 0, 0.5);
        UniformGrid factGrid = Assert.Single(
            primaryFacts.GetVisualDescendants().OfType<UniformGrid>());
        Control[] factCells = [.. factGrid.Children.OfType<Control>()];
        Assert.Equal(4, factCells.Length);
        Assert.All(factCells, cell =>
            Assert.InRange(Math.Abs(cell.Bounds.Width - (factGrid.Bounds.Width / 4)), 0, 0.5));
        Assert.InRange(
            Math.Abs(factCells.Sum(static cell => cell.Bounds.Width) - factGrid.Bounds.Width),
            0,
            2);
        Assert.Equal(88, browse.MinWidth);
        Assert.Equal(36, browse.MinHeight);
        Assert.Equal(36, browse.MaxHeight);
        Assert.Equal(Avalonia.Layout.HorizontalAlignment.Center, browse.HorizontalContentAlignment);
        Assert.Equal(Avalonia.Layout.VerticalAlignment.Center, browse.VerticalContentAlignment);
        Assert.True(browse.Bounds.Width >= 88);
        Assert.Equal(36, browse.Bounds.Height, precision: 3);
        Assert.Equal(10, actions.Spacing);
        Assert.Equal(36, clear.Bounds.Width, precision: 3);
        Assert.Equal(36, clear.Bounds.Height, precision: 3);
        Assert.True(clear.IsVisible);
        Assert.InRange(selector.Bounds.Height, 90, 104);
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
        Assert.Equal(36, slotIcon.Bounds.Width, precision: 3);
        Assert.Equal(36, slotIcon.Bounds.Height, precision: 3);
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

    /// <summary>Rendered DP/TP selectors share one optical axis at desktop and compact widths.</summary>
    [AvaloniaTheory]
    [InlineData(900, false, false)]
    [InlineData(900, false, true)]
    [InlineData(900, true, false)]
    [InlineData(900, true, true)]
    [InlineData(480, false, false)]
    [InlineData(480, false, true)]
    [InlineData(480, true, false)]
    [InlineData(480, true, true)]
    public async Task FirmwareSlotBrowseSelectorUsesSharedOpticalCenterAcrossThemesAndBreakpoints(
        double width,
        bool useDarkTheme,
        bool useTpSlot)
    {
        FirmwareSlotViewModel slot = CreateSelectedFirmwareSlot(useTpSlot);
        var card = new FirmwareSlotCard
        {
            BrowseLabel = "Browse",
            ClearSelectionLabel = "Clear selected file",
            DataContext = slot,
            Width = width,
        };
        ThemeVariant theme = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        (Window host, _, _) = HostWithProductionFirmwareSlotStyles(card);
        host.Width = width;
        host.Height = 220;
        host.RequestedThemeVariant = theme;
        host.Content = null;
        host.Content = new StackPanel
        {
            Children = { card },
        };

        host.Show();
        try
        {
            host.Measure(new Size(width, 220));
            host.Arrange(new Rect(0, 0, width, 220));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Border selector = Assert.Single(
                card.GetVisualDescendants().OfType<Border>(),
                candidate => candidate.Classes.Contains("firmwareSlot"));
            Grid layout = Assert.IsType<Grid>(card.FindControl<Control>("SlotLayout"));
            StackPanel facts = Assert.IsType<StackPanel>(
                card.FindControl<Control>("SlotFactsRegion"));
            Button browse = Assert.IsType<Button>(card.FindControl<Control>("BrowseButton"));
            Button clear = Assert.IsType<Button>(card.FindControl<Control>("ClearButton"));
            StackPanel actions = Assert.IsType<StackPanel>(
                card.FindControl<Control>("SlotActions"));
            StackPanel identity = Assert.IsType<StackPanel>(
                card.FindControl<Control>("SlotIdentity"));
            browse.ApplyTemplate();
            ContentPresenter browseSurface = Assert.Single(
                browse.GetVisualDescendants().OfType<ContentPresenter>(),
                candidate => candidate.Name == "PART_ContentPresenter");
            Point factsOrigin = Assert.IsType<Point>(facts.TranslatePoint(default, selector));
            Point browseSurfaceOrigin = Assert.IsType<Point>(
                browseSurface.TranslatePoint(default, selector));
            Point clearOrigin = Assert.IsType<Point>(clear.TranslatePoint(default, selector));
            Point identityOrigin = Assert.IsType<Point>(identity.TranslatePoint(default, selector));
            double factsCenter = factsOrigin.Y + (facts.Bounds.Height / 2);
            double browseCenter = browseSurfaceOrigin.Y + (browseSurface.Bounds.Height / 2);
            double clearCenter = clearOrigin.Y + (clear.Bounds.Height / 2);
            double selectorCenter = selector.Bounds.Height / 2;

            Assert.InRange(Math.Abs(clearCenter - browseCenter), 0, 0.5);
            Assert.Equal(36, clear.Bounds.Width);
            Assert.Equal(36, clear.Bounds.Height);
            Assert.InRange(clearOrigin.X, 0, selector.Bounds.Width - clear.Bounds.Width);
            if (width >= 820)
            {
                Assert.InRange(Math.Abs(factsCenter - browseCenter), 0, 0.5);
                double selectorOffset = browseCenter - selectorCenter;
                Assert.True(
                    Math.Abs(selectorOffset) <= 0.5,
                    $"Browse surface offset {selectorOffset:F3}; selector={selector.Bounds}; " +
                    $"layout={layout.Bounds}; card={card.Bounds}.");
            }
            else
            {
                double identityCenter = identityOrigin.Y + (identity.Bounds.Height / 2);
                Assert.InRange(Math.Abs(identityCenter - browseCenter), 0, 0.5);
                Assert.True(factsOrigin.Y >= identityOrigin.Y + identity.Bounds.Height);
            }

            double selectedBrowseX = browseSurfaceOrigin.X;
            double selectedActionsWidth = actions.Bounds.Width;
            slot.FilePath = null;
            Dispatcher.UIThread.RunJobs();
            host.Measure(new Size(width, 220));
            host.Arrange(new Rect(0, 0, width, 220));
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Point emptyBrowseSurfaceOrigin = Assert.IsType<Point>(
                browseSurface.TranslatePoint(default, selector));
            TextBlock clearIcon = Assert.IsType<TextBlock>(clear.Content);
            Color disabledColor = Color.Parse(useDarkTheme ? "#64748B" : "#94A3B8");
            Assert.True(clear.IsVisible);
            Assert.False(clear.IsEnabled);
            Assert.Equal(disabledColor, Assert.IsType<SolidColorBrush>(clear.Foreground).Color);
            Assert.Equal(disabledColor, Assert.IsType<SolidColorBrush>(clearIcon.Foreground).Color);
            Assert.Equal(selectedBrowseX, emptyBrowseSurfaceOrigin.X, precision: 3);
            Assert.Equal(selectedActionsWidth, actions.Bounds.Width, precision: 3);

            slot.FilePath = useTpSlot ? @"C:\firmware\tp.bin" : @"C:\firmware\dp.bin";
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Assert.True(clear.IsEnabled);
            Color dangerColor = Color.Parse(useDarkTheme ? "#FCA5A5" : "#B91C1C");
            Assert.Equal(dangerColor, Assert.IsType<SolidColorBrush>(clear.Foreground).Color);
            Assert.Equal(dangerColor, Assert.IsType<SolidColorBrush>(clearIcon.Foreground).Color);
            using Avalonia.Media.Imaging.Bitmap? frame = host.GetLastRenderedFrame();
            Assert.NotNull(frame);
            string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                _ = Directory.CreateDirectory(outputDirectory);
                string themeName = useDarkTheme ? "dark" : "light";
                string slotName = useTpSlot ? "tp" : "dp";
                string outputPath = Path.Combine(
                    outputDirectory,
                    $"nfc-slot-selector-{slotName}-{width:F0}-{themeName}.png");
                await using FileStream output = File.Create(outputPath);
                frame.Save(output);
            }
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>Real long English and Chinese slot titles stay inside the fixed identity region.</summary>
    [AvaloniaTheory]
    [InlineData("Browse", "Initial Code replacement BIN")]
    [InlineData("瀏覽", "Initial Code 取代 BIN 檔案")]
    public void FirmwareSlotLongLocalizedTitleDoesNotIntersectBrowseAtSupportedWidth(
        string browseLabel,
        string slotTitle)
    {
        const double width = 900;
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
        MainWindowViewModel dpViewModel = await PresentationTestHost.CreateViewModelAsync(
            TestContext.Current.CancellationToken);
        dpViewModel.WorkflowSession.SelectedIc = "NT51927";
        OpenReplace(dpViewModel, ExperienceIds.DpReplace);
        FirmwareSlotViewModel dp = Assert.Single(
            dpViewModel.Replace.ReplaceSlots,
            slot => slot.AddressSpaceId == CompositionAddressSpaceIds.DpReplacement);
        MainWindowViewModel nt51928ViewModel = await PresentationTestHost.CreateViewModelAsync(
            TestContext.Current.CancellationToken);
        nt51928ViewModel.WorkflowSession.SelectedIc = "NT51928";
        OpenReplace(nt51928ViewModel, ExperienceIds.DpReplace);
        FirmwareSlotViewModel initialCode = Assert.Single(
            nt51928ViewModel.Replace.ReplaceSlots,
            slot => slot.AddressSpaceId == CompositionAddressSpaceIds.InitialCodeReplacement);
        FirmwareSlotViewModel ldc = Assert.Single(
            nt51928ViewModel.Replace.ReplaceSlots,
            slot => slot.AddressSpaceId == CompositionAddressSpaceIds.LdcReplacement);
        dpViewModel.SelectedLanguage = "Traditional Chinese";
        nt51928ViewModel.SelectedLanguage = "Traditional Chinese";

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
        MainWindowViewModel topologyViewModel = await PresentationTestHost.CreateViewModelAsync(
            TestContext.Current.CancellationToken);
        topologyViewModel.WorkflowSession.SelectedIc = "NT51927";
        topologyViewModel.WorkflowSession.SelectedNumber = "3";
        OpenReplace(topologyViewModel, ExperienceIds.CtrlRamReplace);
        FirmwareSlotViewModel[] topologySlots =
        [
            Assert.Single(topologyViewModel.Replace.ReplaceSlots, candidate =>
                candidate.RegionGroup == ReplaceRegionGroup.Master &&
                candidate.Title == "Normal CtrlRAM (Master)"),
            Assert.Single(topologyViewModel.Replace.ReplaceSlots, candidate =>
                candidate.RegionGroup == ReplaceRegionGroup.SlaveRight &&
                candidate.Title == "Normal CtrlRAM (Slave R)"),
            Assert.Single(topologyViewModel.Replace.ReplaceSlots, candidate =>
                candidate.RegionGroup == ReplaceRegionGroup.SlaveLeft &&
                candidate.Title == "Normal CtrlRAM (Slave L)"),
            Assert.Single(topologyViewModel.Replace.ReplaceSlots, candidate =>
                candidate.RegionGroup == ReplaceRegionGroup.Common &&
                candidate.Title == "NF CtrlRAM (Shared)"),
        ];
        MainWindowViewModel cascadeViewModel = await PresentationTestHost.CreateViewModelAsync(
            TestContext.Current.CancellationToken);
        cascadeViewModel.WorkflowSession.SelectedIc = "NT51950";
        cascadeViewModel.WorkflowSession.SelectedNumber = "cascade";
        OpenReplace(cascadeViewModel, ExperienceIds.CtrlRamReplace);
        FirmwareSlotViewModel cascadeSlot = Assert.Single(
            cascadeViewModel.Replace.ReplaceSlots,
            candidate => candidate.RegionGroup == ReplaceRegionGroup.Cascade &&
                candidate.SlotId == "replace-ctrlram-diff");
        FirmwareSlotViewModel[] slots = [.. topologySlots, cascadeSlot];
        Dictionary<FirmwareSlotViewModel, (string Title, string Description)> english =
            slots.ToDictionary(
                static slot => slot,
                static slot => (slot.Title, slot.Description));
        topologyViewModel.SelectedLanguage = "Traditional Chinese";
        cascadeViewModel.SelectedLanguage = "Traditional Chinese";

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
        int sharedTargetCount = shared.CtrlRamDescriptionFacts!.TargetRegionCount;
        Assert.True(sharedTargetCount > 0);
        Assert.Equal($"NF_Ctrlram.bin · {sharedTargetCount} 個區域", shared.Description);
        Assert.True(shared.CtrlRamDescriptionFacts.Sections.Count >= sharedTargetCount);
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

    private static bool HasXamlName(XElement element, string name)
    {
        return element.Attributes().Any(attribute =>
            attribute.Name.LocalName == "Name" && attribute.Value == name);
    }

    private static FirmwareSlotViewModel CreateSelectedFirmwareSlot(bool useTpSlot)
    {
        var slot = new FirmwareSlotViewModel(
            useTpSlot ? "tp" : "dp",
            useTpSlot ? "TP BIN" : "DP BIN",
            useTpSlot ? "Select TP firmware" : "Select DP firmware",
            useTpSlot ? FirmwareSlotKind.Tp : FirmwareSlotKind.Dp)
        {
            FilePath = useTpSlot
                ? @"C:\firmware\tp-input.bin"
                : @"C:\firmware\dp-input.bin",
        };
        slot.SetInputInspection(FirmwareInputInspectionSeverity.Valid, "The selected BIN is valid.");
        slot.SetFirmwareFacts(useTpSlot
            ?
            [
                new("Common FW Version", "2.0.0"),
                new("TP Version", "T04-00"),
                new("PID", "0x135E"),
            ]
            :
            [
                new("DP Version", "DCC-00"),
                new("Jira Index", "AUTO_PRJ-576"),
            ]);
        return slot;
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
        var firmwareSlotStyles = new StyleInclude(ProductionFirmwareSlotStylesUri)
        {
            Source = ProductionFirmwareSlotStylesUri,
        };
        var host = new Window();
        host.Resources.MergedDictionaries.Add(new ResourceInclude(ProductionSharedTemplatesUri)
        {
            Source = ProductionSharedTemplatesUri,
        });
        host.Styles.Add(buttonStyles);
        host.Styles.Add(visualStyles);
        host.Styles.Add(firmwareSlotStyles);
        host.Content = card;
        return (host, buttonStyles, visualStyles);
    }

}
