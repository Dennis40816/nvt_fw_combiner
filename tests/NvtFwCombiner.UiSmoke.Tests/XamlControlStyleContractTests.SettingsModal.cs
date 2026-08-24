using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>The approved Settings reference owns one identical 1584x997 geometry in Light and Dark.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SettingsVersionReferenceGeometryMatchesInBothThemes(bool useDarkTheme)
    {
        MainWindowViewModel viewModel = await Task.Run(
            () => PresentationTestHost.CreateViewModel(),
            TestContext.Current.CancellationToken);
        viewModel.Settings.Refresh(viewModel.Text);
        viewModel.Settings.ApplyVersionSnapshot(CreateApprovedSettingsReferenceSnapshot());
        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.Version);
        var modal = new SettingsModal
        {
            DataContext = viewModel,
            IsOpen = true,
        };
        var window = new Window
        {
            Width = 1584,
            Height = 997,
            RequestedThemeVariant = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = modal,
        };
        Uri versionTemplateUri = new(
            "avares://NvtFwCombiner.Presentation.Avalonia/Resources/SettingsVersionPageTemplate.axaml");
        Uri pageTemplatesUri = new(
            "avares://NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowPageTemplates.axaml");
        window.Resources.MergedDictionaries.Add(new ResourceInclude(versionTemplateUri)
        {
            Source = versionTemplateUri,
        });
        window.Resources.MergedDictionaries.Add(new ResourceInclude(pageTemplatesUri)
        {
            Source = pageTemplatesUri,
        });
        window.Styles.Add(new StyleInclude(ProductionMainWindowStylesUri)
        {
            Source = ProductionMainWindowStylesUri,
        });
        window.Styles.Add(new StyleInclude(ProductionButtonStylesUri)
        {
            Source = ProductionButtonStylesUri,
        });
        window.Styles.Add(new StyleInclude(ProductionVisualStylesUri)
        {
            Source = ProductionVisualStylesUri,
        });
        try
        {
            window.Show();
            window.Measure(new Size(1584, 997));
            window.Arrange(new Rect(0, 0, 1584, 997));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Border surface = Assert.IsType<Border>(modal.FindControl<Control>("SettingsSurface"));
            Grid header = Assert.IsType<Grid>(modal.FindControl<Control>("SettingsHeader"));
            Border body = Assert.IsType<Border>(modal.FindControl<Control>("SettingsBody"));
            Border navigation = Assert.Single(
                modal.GetVisualDescendants().OfType<Border>(),
                control => control.Name == "SettingsNavigationRail");
            ScrollViewer content = Assert.Single(
                modal.GetVisualDescendants().OfType<ScrollViewer>(),
                control => control.Name == "SettingsContentViewport");
            Control versionPage = Assert.Single(
                modal.GetVisualDescendants().OfType<Control>(),
                control => control.Name == "VersionPageRoot");
            Control currentSection = Assert.Single(
                modal.GetVisualDescendants().OfType<Control>(),
                control => control.Name == "CurrentVersionSection");
            Border updateBanner = Assert.Single(
                modal.GetVisualDescendants().OfType<Border>(),
                control => control.Name == "VerifiedUpdateBanner");
            Control sourceSection = Assert.Single(
                modal.GetVisualDescendants().OfType<Control>(),
                control => control.Name == "UpdateSourceSection");
            Control versionTable = Assert.Single(
                modal.GetVisualDescendants().OfType<Control>(),
                control => control.Name == "VersionTableSurface");
            TextBlock pageTitle = Assert.Single(
                modal.GetVisualDescendants().OfType<TextBlock>(),
                control => control.Classes.Contains("versionPageTitle"));
            TextBlock pageSubtitle = Assert.Single(
                modal.GetVisualDescendants().OfType<TextBlock>(),
                control => control.Classes.Contains("versionPageSubtitle"));
            TextBlock updateMessage = Assert.Single(
                modal.GetVisualDescendants().OfType<TextBlock>(),
                control => control.Classes.Contains("versionUpdateMessage"));
            Border sourceEditor = Assert.Single(
                modal.GetVisualDescendants().OfType<Border>(),
                control => control.Classes.Contains("versionSourceEditor"));
            RadioButton selectedNavigation = Assert.Single(
                modal.GetVisualDescendants().OfType<RadioButton>(),
                control => control.Classes.Contains("selected"));
            Button installUpdate = Assert.Single(
                modal.GetVisualDescendants().OfType<Button>(),
                control => control.Classes.Contains("versionInstallAction") &&
                    !control.Classes.Contains("versionSourceAction"));
            Button close = Assert.IsType<Button>(modal.FindControl<Control>("CloseButton"));

            Assert.InRange(surface.Bounds.Width, 1555.5, 1556.5);
            Assert.InRange(surface.Bounds.Height, 974.5, 975.5);
            Assert.InRange(header.Bounds.Height, 59.5, 60.5);
            Assert.InRange(body.Bounds.Width, 1527.5, 1528.5);
            Assert.InRange(navigation.Bounds.Width, 317.5, 318.5);
            Assert.Equal(40, close.Bounds.Width);
            Assert.Equal(close.Bounds.Width, close.Bounds.Height);
            ContentPresenter closePresenter = Assert.Single(
                close.GetVisualDescendants().OfType<ContentPresenter>(),
                control => control.Name == "PART_ContentPresenter");
            Assert.Equal(new CornerRadius(999), closePresenter.CornerRadius);
            Assert.Equal(
                Colors.Transparent,
                Assert.IsType<ISolidColorBrush>(closePresenter.Background, exactMatch: false).Color);
            Assert.Equal(
                Colors.Transparent,
                Assert.IsType<ISolidColorBrush>(closePresenter.BorderBrush, exactMatch: false).Color);

            Point bodyOrigin = Assert.IsType<Point>(body.TranslatePoint(default, surface));
            Point contentOrigin = Assert.IsType<Point>(content.TranslatePoint(default, body));
            Point pageOrigin = Assert.IsType<Point>(versionPage.TranslatePoint(default, body));
            Assert.InRange(bodyOrigin.X, 13.5, 14.5);
            Assert.InRange(bodyOrigin.Y, 61.5, 62.5);
            Assert.InRange(contentOrigin.X, 362.5, 363.5);
            Assert.InRange(contentOrigin.Y, 19.5, 20.5);
            Assert.InRange(Math.Abs(pageOrigin.X - contentOrigin.X), 0, 0.5);
            Assert.InRange(Math.Abs(pageOrigin.Y - contentOrigin.Y), 0, 0.5);

            Point currentOrigin = Assert.IsType<Point>(currentSection.TranslatePoint(default, body));
            Point bannerOrigin = Assert.IsType<Point>(updateBanner.TranslatePoint(default, body));
            Point sourceOrigin = Assert.IsType<Point>(sourceSection.TranslatePoint(default, body));
            Point tableOrigin = Assert.IsType<Point>(versionTable.TranslatePoint(default, body));
            AssertControlGeometry(currentOrigin, currentSection.Bounds.Size, 363, 145, 1121, 74);
            AssertControlGeometry(bannerOrigin, updateBanner.Bounds.Size, 363, 247, 1121, 84);
            AssertControlGeometry(sourceOrigin, sourceSection.Bounds.Size, 363, 359, 1121, 132);
            AssertControlGeometry(tableOrigin, versionTable.Bounds.Size, 363, 555, 1121, 207);
            Assert.Equal(30, pageTitle.FontSize);
            Assert.Equal(FontWeight.SemiBold, pageTitle.FontWeight);
            Assert.Equal(18, pageSubtitle.FontSize);
            Assert.Equal(18, updateMessage.FontSize);
            Assert.Equal(new Thickness(2), updateBanner.BorderThickness);
            Assert.Equal(new CornerRadius(6), updateBanner.CornerRadius);
            Assert.Equal(new Thickness(2), sourceEditor.BorderThickness);
            Assert.Equal(new CornerRadius(6), sourceEditor.CornerRadius);
            Assert.Equal(new Thickness(2), Assert.IsType<Border>(versionTable).BorderThickness);
            Assert.Equal(64, selectedNavigation.Bounds.Height);
            Assert.Equal(18, selectedNavigation.FontSize);
            Assert.Equal(180, installUpdate.Bounds.Width);
            Assert.Equal(52, installUpdate.Bounds.Height);

            using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                _ = Directory.CreateDirectory(outputDirectory);
                string themeName = useDarkTheme ? "dark" : "light";
                string outputPath = Path.Combine(
                    outputDirectory,
                    $"settings-version-reference-1584x997-{themeName}.png");
                await using FileStream output = File.Create(outputPath);
                frame.Save(output);
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The reference rail is four uninterrupted entries and uses the approved General gear.</summary>
    [Fact]
    public void SettingsReferenceRailMatchesApprovedStructure()
    {
        var document = System.Xml.Linq.XDocument.Parse(
            ReadPresentationFile("Resources/MainWindowPageTemplates.axaml"));
        System.Xml.Linq.XElement rail = Assert.Single(
            document.Descendants(),
            element => element.Attributes().Any(
                attribute => attribute.Name.LocalName == "Name" &&
                    attribute.Value == "SettingsNavigationRail"));
        System.Xml.Linq.XElement items = Assert.Single(
            rail.Elements(),
            element => element.Name.LocalName == "StackPanel");
        System.Xml.Linq.XElement[] entries = [.. items.Elements()];

        Assert.Equal(4, entries.Length);
        Assert.All(entries, entry => Assert.Equal("RadioButton", entry.Name.LocalName));
        System.Xml.Linq.XElement generalIcon = Assert.Single(
            entries[0].Descendants(),
            element => element.Name.LocalName == "Path");
        Assert.Equal(
            "M11 2L13 3L15 2L18 5L17 7L18 9V13L17 15L18 17L15 20L13 19L11 20L8 19L6 20L3 17L4 15L3 13V9L4 7L3 5L6 2L8 3L11 2Z M11 7A4 4 0 1 0 11 15A4 4 0 1 0 11 7Z",
            (string?)generalIcon.Attribute("Data"));
    }

    /// <summary>Typography, radii, borders and internal columns remain the approved reference invariants.</summary>
    [Fact]
    public void SettingsReferenceTokensAndInternalColumnsAreLocked()
    {
        string versionPage = ReadPresentationFile("Resources/SettingsVersionPageTemplate.axaml");
        string pageTemplates = ReadPresentationFile("Resources/MainWindowPageTemplates.axaml");
        string versionStyles = ReadPresentationFile("Styles/SettingsVersionStyles.axaml");
        string buttonStyles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");

        Assert.Contains("x:Name=\"VersionPageRoot\" Spacing=\"28\"", versionPage, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"Auto,*,Auto,Auto\"", versionPage, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"*,Auto,Auto\" ColumnSpacing=\"32\"", versionPage, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"86,*,Auto,Auto\"", versionPage, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(pageTemplates + versionPage, "ColumnDefinitions=\"1.1*,1.4*,1*,Auto,38\""));
        Assert.Contains("FontSize\" Value=\"30", ExtractStyle(versionStyles, "TextBlock.versionPageTitle"), StringComparison.Ordinal);
        Assert.Contains("FontSize\" Value=\"18", ExtractStyle(versionStyles, "TextBlock.versionPageSubtitle"), StringComparison.Ordinal);
        Assert.Contains("FontSize\" Value=\"22", ExtractStyle(versionStyles, "TextBlock.versionSectionTitle"), StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"2", ExtractStyle(versionStyles, "Border.versionSourceEditor"), StringComparison.Ordinal);
        Assert.Contains("NfcCompactCornerRadius", ExtractStyle(versionStyles, "Border.versionSourceEditor"), StringComparison.Ordinal);
        Assert.Contains("MinHeight\" Value=\"84", ExtractStyle(versionStyles, "Border.versionUpdateBanner"), StringComparison.Ordinal);
        Assert.Contains("Height\" Value=\"64", ExtractStyle(buttonStyles, "Button.settingsNavItem"), StringComparison.Ordinal);
        Assert.Contains("NfcCompactCornerRadius", ExtractStyle(buttonStyles, "Button.settingsNavItem /template/ ContentPresenter#PART_ContentPresenter"), StringComparison.Ordinal);
        Assert.Contains("MinWidth\" Value=\"180", ExtractStyle(buttonStyles, "Button.versionInstallAction"), StringComparison.Ordinal);
        Assert.Contains("MinHeight\" Value=\"52", ExtractStyle(buttonStyles, "Button.versionInstallAction"), StringComparison.Ordinal);
    }

    /// <summary>The owner-approved Version Option A keeps two separate rounded actions with icons and one primary check action.</summary>
    [Fact]
    public void SettingsVersionSourceActionsMatchApprovedOptionA()
    {
        string versionPage = ReadPresentationFile("Resources/SettingsVersionPageTemplate.axaml");
        string buttonStyles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");
        var document = System.Xml.Linq.XDocument.Parse(versionPage);
        System.Xml.Linq.XElement browse = Assert.Single(
            document.Descendants(),
            element => element.Name.LocalName == "Button" &&
                (string?)element.Attribute("Command") == "{Binding Settings.BrowseUpdateSourceCommand}");
        System.Xml.Linq.XElement check = Assert.Single(
            document.Descendants(),
            element => element.Name.LocalName == "Button" &&
                (string?)element.Attribute("Command") == "{Binding Settings.CheckNowCommand}");

        Assert.Equal("semanticAction secondary versionSourceAction", (string?)browse.Attribute("Classes"));
        Assert.Equal(
            "semanticAction versionInstallAction versionSourceAction",
            (string?)check.Attribute("Classes"));
        Assert.NotNull(Assert.Single(browse.Descendants(), element => element.Name.LocalName == "Path"));
        Assert.NotNull(Assert.Single(check.Descendants(), element => element.Name.LocalName == "Path"));
        Assert.Equal(
            "{Binding Settings.BrowseSourceLabel}",
            (string?)Assert.Single(browse.Descendants(), element => element.Name.LocalName == "TextBlock")
                .Attribute("Text"));
        Assert.Equal(
            "{Binding Settings.CheckNowLabel}",
            (string?)Assert.Single(check.Descendants(), element => element.Name.LocalName == "TextBlock")
                .Attribute("Text"));
        string primarySurface = ExtractStyle(
            buttonStyles,
            "Button.versionInstallAction /template/ ContentPresenter#PART_ContentPresenter");
        Assert.Contains("NfcAccentBrush", primarySurface, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceBrush", primarySurface, StringComparison.Ordinal);
    }

    /// <summary>Every upper-right Close/Exit entry uses the dedicated true-circle control contract.</summary>
    [Fact]
    public void UpperRightCloseEntriesUseDedicatedCircularControl()
    {
        string[] closeSurfaces =
        [
            "Views/SettingsModal.axaml",
            "Views/MessageCenterModal.axaml",
            "Views/ReplaceSelectionModal.axaml",
            "Resources/MainWindowReportPanels.axaml",
        ];

        foreach (string file in closeSurfaces)
        {
            var document = System.Xml.Linq.XDocument.Parse(ReadPresentationFile(file));
            System.Xml.Linq.XElement close = Assert.Single(
                document.Descendants(),
                element => element.Name.LocalName == "Button" &&
                    (((string?)element.Attribute("Classes")) ?? string.Empty)
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Contains("closeButton", StringComparer.Ordinal));
            string[] classes = ((string?)close.Attribute("Classes") ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.Contains("closeButton", classes);
            Assert.DoesNotContain("iconButton", classes);
            Assert.False(string.IsNullOrWhiteSpace((string?)close.Attribute("AutomationProperties.Name")));
            System.Xml.Linq.XElement icon = Assert.Single(
                close.Elements(),
                element => element.Name.LocalName == "Path");
            Assert.Equal("{StaticResource NfcCloseIconGeometry}", (string?)icon.Attribute("Data"));
        }

        string styles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");
        string closeStyle = ExtractStyle(styles, "Button.closeButton");
        Assert.Contains("Width\" Value=\"40", closeStyle, StringComparison.Ordinal);
        Assert.Contains("Height\" Value=\"40", closeStyle, StringComparison.Ordinal);
        Assert.Contains(
            "NfcPillCornerRadius",
            ExtractStyle(styles, "Button.closeButton /template/ ContentPresenter#PART_ContentPresenter"),
            StringComparison.Ordinal);
        Assert.Contains("Background\" Value=\"Transparent", closeStyle, StringComparison.Ordinal);
        Assert.Contains("BorderBrush\" Value=\"Transparent", closeStyle, StringComparison.Ordinal);
        Assert.Contains(
            "BorderBrush\" Value=\"Transparent",
            ExtractStyle(styles, "Button.closeButton:pointerover /template/ ContentPresenter#PART_ContentPresenter"),
            StringComparison.Ordinal);
        Assert.Contains(
            "BorderBrush\" Value=\"Transparent",
            ExtractStyle(styles, "Button.closeButton:pressed /template/ ContentPresenter#PART_ContentPresenter"),
            StringComparison.Ordinal);
        Assert.Contains(
            "NfcAccentBorderStrongBrush",
            ExtractStyle(styles, "Button:focus-visible /template/ ContentPresenter#PART_ContentPresenter"),
            StringComparison.Ordinal);
    }

    /// <summary>The Settings surface owns focus entry, a cycle trap, Escape close and focus return.</summary>
    [AvaloniaFact]
    public async Task SettingsModalSupportsKeyboardModalLifecycle()
    {
        MainWindowViewModel viewModel = await Task.Run(
            () => PresentationTestHost.CreateViewModel());
        var launchButton = new Button { Content = "Settings" };
        var successorModalFocus = new Button { Content = "Successor modal" };
        var modal = new SettingsModal();
        var modalHost = new ContentControl { Content = modal };
        _ = modal.Bind(
            SettingsModal.IsOpenProperty,
            new Binding(nameof(MainWindowViewModel.IsSettingsModalOpen)));
        _ = modalHost.Bind(
            Visual.IsVisibleProperty,
            new Binding(nameof(MainWindowViewModel.IsSettingsModalOpen)));
        var window = new Window
        {
            DataContext = viewModel,
            Content = new Grid
            {
                Children = { launchButton, successorModalFocus, modalHost },
            },
        };
        try
        {
            window.Show();
            _ = launchButton.Focus(NavigationMethod.Tab);

            viewModel.OpenSettingsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(viewModel.IsSettingsModalOpen);
            Assert.True(modalHost.IsVisible);
            Assert.True(modal.IsOpen);
            Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation(modal));
            Assert.NotSame(launchButton, window.FocusManager?.GetFocusedElement());

            modal.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Escape,
            });
            Dispatcher.UIThread.RunJobs();

            Assert.False(viewModel.IsSettingsModalOpen);
            Assert.Same(launchButton, window.FocusManager?.GetFocusedElement());

            viewModel.OpenSettingsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(modal.IsOpen);
            Assert.NotSame(launchButton, window.FocusManager?.GetFocusedElement());

            viewModel.CloseSettingsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(launchButton, window.FocusManager?.GetFocusedElement());

            viewModel.OpenSettingsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            viewModel.Navigation.IsNavigationClearConfirmationOpen = true;
            _ = successorModalFocus.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            Assert.False(viewModel.IsSettingsModalOpen);
            Assert.Same(successorModalFocus, window.FocusManager?.GetFocusedElement());
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The production host binds lifecycle to the canonical state and disables background interaction.</summary>
    [Fact]
    public void SettingsModalProductionHostOwnsLifecycleAndShellInertness()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string codeBehind = ReadPresentationFile("MainWindow.axaml.cs");
        string modal = ReadPresentationFile("Views/SettingsModal.axaml");

        Assert.Contains("<views:SettingsModal IsOpen=\"{Binding IsSettingsModalOpen}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ApplyShellInteractionState(viewModel);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("!viewModel.IsSettingsModalOpen", codeBehind, StringComparison.Ordinal);
        Assert.Contains("shellInteractionHost.IsEnabled = interactive;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("shellInteractionHost.IsHitTestVisible = interactive;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding SettingsPreview.Title}\"", modal, StringComparison.Ordinal);
    }

    /// <summary>Launcher start is awaited from Closing, and Closed never performs a fire-and-forget handoff.</summary>
    [Fact]
    public void StableLauncherHandoffPrecedesTheFinalWindowClose()
    {
        string codeBehind = ReadPresentationFile("MainWindow.axaml.cs");
        int closing = codeBehind.IndexOf("protected override async void OnClosing", StringComparison.Ordinal);
        int handoff = codeBehind.IndexOf(
            "bool started = await TryCompleteStableLauncherHandoffAsync();",
            closing,
            StringComparison.Ordinal);
        int finalClose = codeBehind.IndexOf("Dispatcher.UIThread.Post(Close);", handoff, StringComparison.Ordinal);
        int closed = codeBehind.IndexOf("protected override void OnClosed", StringComparison.Ordinal);
        int dispose = codeBehind.IndexOf("public void Dispose()", closed, StringComparison.Ordinal);

        Assert.True(closing >= 0 && handoff > closing && finalClose > handoff);
        Assert.True(closed > finalClose && dispose > closed);
        Assert.DoesNotContain(
            "StableLauncherHandoff",
            codeBehind[closed..dispose],
            StringComparison.Ordinal);
    }

    /// <summary>Version status and destructive icons expose localized non-color-only accessible names.</summary>
    [Fact]
    public void SettingsVersionIconsUseBoundAccessibleNamesAndTooltips()
    {
        string versionPage = ReadPresentationFile("Resources/SettingsVersionPageTemplate.axaml");
        string sharedPages = ReadPresentationFile("Resources/MainWindowPageTemplates.axaml");

        Assert.Contains(
            "AutomationProperties.AccessibilityView=\"Content\"",
            versionPage,
            StringComparison.Ordinal);
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding Settings.SourceStatusText}\"",
            versionPage,
            StringComparison.Ordinal);
        Assert.Contains(
            "ToolTip.Tip=\"{Binding Settings.SourceStatusText}\"",
            versionPage,
            StringComparison.Ordinal);
        Assert.Contains(
            "Command=\"{Binding Settings.ShowVerifiedReleaseNotesCommand}\"",
            versionPage,
            StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(
                versionPage,
                "Command=\"{Binding Settings.RequestVersionPrimaryActionCommand}\""));
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding DeleteActionLabel}\"",
            sharedPages,
            StringComparison.Ordinal);
        Assert.Contains(
            "ToolTip.Tip=\"{Binding DeleteActionLabel}\"",
            sharedPages,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AutomationProperties.Name=\"Delete installed version\"",
            sharedPages,
            StringComparison.Ordinal);
    }

    private static void AssertControlGeometry(
        Point origin,
        Size size,
        double expectedX,
        double expectedY,
        double expectedWidth,
        double expectedHeight)
    {
        Assert.InRange(origin.X, expectedX - 0.5, expectedX + 0.5);
        Assert.InRange(origin.Y, expectedY - 0.5, expectedY + 0.5);
        Assert.InRange(size.Width, expectedWidth - 0.5, expectedWidth + 0.5);
        Assert.InRange(size.Height, expectedHeight - 0.5, expectedHeight + 0.5);
    }

    /// <summary>The source segment uses the approved ring and binds the shell motion preference.</summary>
    [Fact]
    public void VersionCheckingUsesDedicatedReducedMotionRingInsteadOfProgressBar()
    {
        string versionPage = ReadPresentationFile("Resources/SettingsVersionPageTemplate.axaml");

        Assert.Contains("<views:VersionCheckingIndicator", versionPage, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding Settings.IsSourceChecking}\"", versionPage, StringComparison.Ordinal);
        Assert.Contains(
            "IsReducedMotionEnabled=\"{ReflectionBinding $parent[Window].DataContext.IsReducedMotionEnabled}\"",
            versionPage,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<ProgressBar", versionPage, StringComparison.Ordinal);
    }

    /// <summary>The compact ring renders in both themes and disables time-based motion when requested.</summary>
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void VersionCheckingRingRendersForThemeAndMotionPreference(
        bool useDarkTheme,
        bool reducedMotion)
    {
        ThemeVariant theme = useDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        Assert.True(Avalonia.Application.Current!.TryGetResource("NfcAccentBrush", theme, out object? indicator));
        Assert.True(Avalonia.Application.Current.TryGetResource("NfcAccentBorderBrush", theme, out object? track));
        var ring = new VersionCheckingIndicator
        {
            Width = 18,
            Height = 18,
            IndicatorBrush = Assert.IsType<IBrush>(indicator, exactMatch: false),
            TrackBrush = Assert.IsType<IBrush>(track, exactMatch: false),
            IsReducedMotionEnabled = reducedMotion,
        };
        var host = new Window
        {
            Width = 40,
            Height = 40,
            RequestedThemeVariant = theme,
            Content = ring,
        };
        try
        {
            host.Show();
            host.Measure(new Size(40, 40));
            host.Arrange(new Rect(0, 0, 40, 40));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Assert.Equal(
                !reducedMotion,
                VersionCheckingIndicator.ShouldAnimate(
                    isAttached: true,
                    isVisible: true,
                    reducedMotion));
            using Avalonia.Media.Imaging.Bitmap? frame = host.GetLastRenderedFrame();
            Assert.NotNull(frame);
        }
        finally
        {
            host.Close();
        }
    }

    private static VersionManagementSnapshot CreateApprovedSettingsReferenceSnapshot()
    {
        const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        ManagedAppVersion active = ManagedAppVersion.Parse("0.10.5");
        ManagedAppVersion installed = ManagedAppVersion.Parse("0.9.15");
        ManagedVersionAdmission[] admissions =
        [
            new(active, "identity-0.10.5", hash),
            new(installed, "identity-0.9.15", hash),
        ];
        VersionManagerState state = VersionManagerState.Create(
            @"\\novatek\firmware-tools\nvt-fw-combiner",
            active,
            installed,
            admissions,
            pendingActivation: null,
            failedActivationVersion: null,
            retentionReviewDue: false);
        ManagedVersionInventory inventory = ManagedVersionInventory.Create(
        [
            new(active, "identity-0.10.5", ManagedVersionIntegrity.Healthy, null, true, false),
            new(installed, "identity-0.9.15", ManagedVersionIntegrity.Healthy, null, false, true),
        ]);
        var available = new UpdateCatalogVersionSnapshot(
            ManagedAppVersion.Parse("0.10.6"),
            DateTimeOffset.Parse(
                "2026-08-21T00:00:00Z",
                System.Globalization.CultureInfo.InvariantCulture),
            new("packages/NvtFwCombiner-v0.10.6-win-x64.zip"),
            42,
            hash,
            hash,
            "Version 0.10.6 release notes");
        var currentCatalog = new UpdateCatalogVersionSnapshot(
            active,
            DateTimeOffset.Parse(
                "2026-08-19T00:00:00Z",
                System.Globalization.CultureInfo.InvariantCulture),
            new("packages/NvtFwCombiner-v0.10.5-win-x64.zip"),
            42,
            hash,
            hash,
            "Version 0.10.5 release notes");
        var installedCatalog = new UpdateCatalogVersionSnapshot(
            installed,
            DateTimeOffset.Parse(
                "2026-03-12T00:00:00Z",
                System.Globalization.CultureInfo.InvariantCulture),
            new("packages/NvtFwCombiner-v0.9.15-win-x64.zip"),
            42,
            hash,
            hash,
            "Version 0.9.15 release notes");
        return new(
            state,
            inventory,
            new UpdateCatalogSnapshot([available, currentCatalog, installedCatalog]),
            new VerifiedUpdateCandidate(available.Version, available.Identity, available.ReleaseNotes),
            VersionSourceStatus.Connected,
            CatalogIssue: null,
            Generation: 1,
            ShouldPromptForUpdate: false,
            VersionManagerStateLoadIssue.None);
    }
}
