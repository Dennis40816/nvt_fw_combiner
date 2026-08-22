using System.Xml.Linq;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Keeps inactive pages and large modals out of the Home visual tree until first use.</summary>
    [Fact]
    public void MainWindowDefersInactivePageAndModalContent()
    {
        var shell = XDocument.Parse(ReadPresentationFile("MainWindow.axaml"));
        var reportModal = XDocument.Parse(ReadPresentationFile("Views/ReportModal.axaml"));
        string codeBehind = ReadPresentationFile("MainWindow.axaml.cs");
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        string[] deferredHosts =
        [
            "DeviceContextHost",
            "HomePageHost",
            "SettingsModalHost",
            "HexEditorPageHost",
            "ReplacePageHost",
            "MergePageHost",
            "ReportToastHost",
            "OutputDeliveryConfirmationModalHost",
            "ReplaceSelectionModalHost",
            "WorkflowContextSetupModalHost",
            "FirmwareIcMismatchModalHost",
            "NavigationClearConfirmationModalHost",
            "MessageCenterModalHost",
            "ReportModalHost",
            "BuildCompletedModalHost",
        ];

        foreach (string hostName in deferredHosts)
        {
            XElement host = Assert.Single(
                shell.Descendants(),
                element => element.Attribute(x + "Name")?.Value == hostName);
            Assert.Null(host.Attribute("Content"));
            Assert.Contains($"LoadContent({hostName},", codeBehind, StringComparison.Ordinal);
        }

        string[] reportResources =
        [
            "MainWindowReportTemplates.axaml",
            "MainWindowReportChangeTemplates.axaml",
            "MainWindowReportInputTemplates.axaml",
            "MainWindowReportOperationTemplates.axaml",
            "MainWindowReportHistoryTemplates.axaml",
            "MainWindowReportAuditTemplates.axaml",
            "MainWindowReportPanels.axaml",
        ];
        foreach (string resourceName in reportResources)
        {
            Assert.DoesNotContain(
                shell.Descendants(),
                element => element.Attribute("Source")?.Value.EndsWith(resourceName, StringComparison.Ordinal) == true);
            _ = Assert.Single(
                reportModal.Descendants(),
                element => element.Attribute("Source")?.Value.EndsWith(resourceName, StringComparison.Ordinal) == true);
        }

        Assert.Contains("ApplyDeferredShellContent(viewModel);", codeBehind, StringComparison.Ordinal);
    }

    /// <summary>Starts low-priority page warm-up only after the Home window is open.</summary>
    [Fact]
    public void MainWindowWarmsCommonPagesAfterFirstFrameWithoutLoadingModals()
    {
        string lifecycle = ReadPresentationFile("MainWindow.axaml.cs");
        string preloadSession = ReadPresentationFile("ShellPreloadSession.cs");
        string warmup = ReadPresentationFile("MainWindow.StartupWarmup.cs");
        string[] warmedHosts =
        [
            "DeviceContextHost",
            "ReplacePageHost",
            "MergePageHost",
            "SettingsModalHost",
            "HexEditorPageHost",
        ];

        Assert.True(
            lifecycle.IndexOf("main-window.opened", StringComparison.Ordinal) <
            lifecycle.IndexOf("_preloadSession.RunCatalogAsync", StringComparison.Ordinal));
        Assert.Contains(
            "loader.LoadAsync(token).WithCancellation(token)",
            preloadSession,
            StringComparison.Ordinal);
        Assert.Contains("viewModel.PublishCanonicalCatalogState", lifecycle, StringComparison.Ordinal);
        Assert.Contains("_preloadSession.RunOptionalStagesAsync(", lifecycle, StringComparison.Ordinal);
        int launch = preloadSession.IndexOf(
            "PresentationObserver.Invoke(work.ApplyLaunchPage);",
            StringComparison.Ordinal);
        Assert.True(launch >= 0);
        int concurrent = preloadSession.IndexOf("await Task.WhenAll(", launch, StringComparison.Ordinal);
        int history = preloadSession.IndexOf("StartOptionalAsync(HistoryStageId", StringComparison.Ordinal);
        int report = preloadSession.IndexOf("StartOptionalAsync(ReportStageId", history, StringComparison.Ordinal);
        Assert.True(launch < concurrent);
        Assert.True(history >= 0 && history < report);
        Assert.Contains("StartOptionalAsync(DiagnosticsStageId", preloadSession, StringComparison.Ordinal);
        Assert.Contains("StartOptionalAsync(ExternalEnvironmentStageId", preloadSession, StringComparison.Ordinal);
        Assert.Contains("StartOptionalAsync(ViewsStageId", preloadSession, StringComparison.Ordinal);
        Assert.Contains("viewModel.MessageCenter.RefreshAfterStartupAsync", lifecycle, StringComparison.Ordinal);
        Assert.Contains("viewModel.Settings.Refresh(viewModel.Text)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("Deferred shell warm-up did not complete", lifecycle, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.Background", warmup, StringComparison.Ordinal);
        Assert.Contains("$\"{traceStage}.started\"", warmup, StringComparison.Ordinal);
        Assert.Contains("$\"{traceStage}.ready\"", warmup, StringComparison.Ordinal);
        Assert.Contains("host.ContentTemplate?.Build(dataContext)", warmup, StringComparison.Ordinal);
        Assert.Contains("host.Content = content", warmup, StringComparison.Ordinal);
        Assert.Contains("WaitForRunIdleAsync(viewModel.RunSession", warmup, StringComparison.Ordinal);
        Assert.Contains("if (runSession.IsRunInProgress)", warmup, StringComparison.Ordinal);
        Assert.Contains("runSession.PropertyChanged += handler", warmup, StringComparison.Ordinal);
        foreach (string hostName in warmedHosts)
        {
            Assert.Contains($"{hostName},", warmup, StringComparison.Ordinal);
        }

        Assert.Contains("(ReplacePageHost, viewModel.Replace,", warmup, StringComparison.Ordinal);
        Assert.Contains("(MergePageHost, viewModel.Merge,", warmup, StringComparison.Ordinal);

        Assert.DoesNotContain("ReportModalHost,", warmup, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportToastHost,", warmup, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareIcMismatchModalHost,", warmup, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigationClearConfirmationModalHost,", warmup, StringComparison.Ordinal);
        Assert.DoesNotContain("MessageCenterModalHost,", warmup, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", warmup, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.", warmup, StringComparison.Ordinal);
    }

    /// <summary>The required shell preload exposes exact stage progress, cancellation, retry, and bounded identity.</summary>
    [Fact]
    public void CatalogWarmupUsesAccessibleRetryableForegroundLoadingSurface()
    {
        string surface = ReadPresentationFile("Views/ForegroundLoadingSurface.axaml");
        string status = ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml");
        string shell = ReadPresentationFile("MainWindow.axaml");
        string lifecycle = ReadPresentationFile("MainWindow.axaml.cs");
        string preloadSession = ReadPresentationFile("ShellPreloadSession.cs");
        string focusBehavior = ReadPresentationFile("Behaviors/FocusOnRevealBehavior.cs");
        string buttonStyles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");
        string windowStyles = ReadPresentationFile("Styles/MainWindowStyles.axaml");
        string globalButton = ExtractStyle(buttonStyles, "Button");
        string semanticAction = ExtractStyle(buttonStyles, "Button.semanticAction");
        string semanticActionDisabled = ExtractStyle(
            buttonStyles,
            "Button.semanticAction:disabled");
        string semanticActionFocus = ExtractStyle(
            buttonStyles,
            "Button:focus-visible /template/ ContentPresenter#PART_ContentPresenter");
        string secondary = ExtractStyle(buttonStyles, "Button.secondary");
        string secondaryHover = ExtractStyle(
            buttonStyles,
            "Button.secondary:pointerover /template/ ContentPresenter#PART_ContentPresenter");
        string secondaryPressed = ExtractStyle(
            buttonStyles,
            "Button.secondary:pressed /template/ ContentPresenter#PART_ContentPresenter");
        string secondaryDisabled = ExtractStyle(
            buttonStyles,
            "Button.secondary:disabled /template/ ContentPresenter#PART_ContentPresenter");
        string dangerHover = ExtractStyle(
            buttonStyles,
            "Button.danger:pointerover /template/ ContentPresenter#PART_ContentPresenter");
        string dangerPressed = ExtractStyle(
            buttonStyles,
            "Button.danger:pressed /template/ ContentPresenter#PART_ContentPresenter");
        string navigation = ExtractStyle(windowStyles, "ToggleButton.nav");
        string navigationPresenter = ExtractStyle(
            windowStyles,
            "ToggleButton.nav /template/ ContentPresenter#PART_ContentPresenter");
        string navigationHover = ExtractStyle(
            windowStyles,
            "ToggleButton.nav:pointerover /template/ ContentPresenter#PART_ContentPresenter");
        string navigationPressed = ExtractStyle(
            windowStyles,
            "ToggleButton.nav:pressed /template/ ContentPresenter#PART_ContentPresenter");
        string navigationCheckedHover = ExtractStyle(
            windowStyles,
            "ToggleButton.nav:checked:pointerover /template/ ContentPresenter#PART_ContentPresenter");
        string navigationCheckedPressed = ExtractStyle(
            windowStyles,
            "ToggleButton.nav:checked:pressed /template/ ContentPresenter#PART_ContentPresenter");
        string navigationDisabled = ExtractStyle(
            windowStyles,
            "ToggleButton.nav:disabled /template/ ContentPresenter#PART_ContentPresenter");
        string navigationFocus = ExtractStyle(
            windowStyles,
            "ToggleButton.nav:focus-visible /template/ ContentPresenter#PART_ContentPresenter");
        int openedStart = lifecycle.IndexOf(
            "protected override async void OnOpened",
            StringComparison.Ordinal);
        int sessionStart = lifecycle.IndexOf(
            "private async Task RunStartupPreloadAsync",
            openedStart,
            StringComparison.Ordinal);
        int retryStart = lifecycle.IndexOf(
            "private Task RetryStartupPreloadAsync",
            sessionStart,
            StringComparison.Ordinal);
        int propertyChangedStart = lifecycle.IndexOf(
            "private void ViewModel_OnPropertyChanged",
            retryStart,
            StringComparison.Ordinal);
        string opened = lifecycle[openedStart..sessionStart];
        string retry = lifecycle[retryStart..propertyChangedStart];
        var surfaceDocument = XDocument.Parse(status);
        XElement statusTemplate = Assert.Single(surfaceDocument.Descendants(), element =>
            element.Name.LocalName == "DataTemplate" &&
            element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" &&
                attribute.Value == "ForegroundLoadingStatusTemplate"));
        XElement[] detailText =
        [
            .. statusTemplate.Descendants()
                .Where(element =>
                    element.Name.LocalName == "TextBlock" &&
                    (string?)element.Attribute("Text") == "{Binding Detail}"),
        ];
        XElement cancelButton = Assert.Single(statusTemplate.Descendants(), element =>
            element.Name.LocalName == "Button" &&
            (string?)element.Attribute("Command") == "{Binding CancelCommand}");
        XElement retryButton = Assert.Single(statusTemplate.Descendants(), element =>
            element.Name.LocalName == "Button" &&
            (string?)element.Attribute("Command") == "{Binding RetryCommand}");
        var buttonStylesDocument = XDocument.Parse(buttonStyles);
        XElement semanticButtonTheme = Assert.Single(buttonStylesDocument.Descendants(), element =>
            element.Name.LocalName == "ControlTheme" &&
            element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" &&
                attribute.Value == "NfcSemanticButtonTheme"));
        XElement semanticButtonPresenter = Assert.Single(semanticButtonTheme.Descendants(), element =>
            element.Name.LocalName == "ContentPresenter" &&
            element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" &&
                attribute.Value == "PART_ContentPresenter"));
        var shellDocument = XDocument.Parse(shell);
        XElement[] navigationButtons =
        [
            .. shellDocument.Descendants().Where(element =>
                element.Name.LocalName == "ToggleButton" &&
                (string?)element.Attribute("Classes") == "nav"),
        ];
        XElement optionalCancelButton = Assert.Single(shellDocument.Descendants(), element =>
            element.Name.LocalName == "Button" &&
            (string?)element.Attribute("Click") == "OptionalPreloadCancelButton_OnClick");
        XElement optionalRetryButton = Assert.Single(shellDocument.Descendants(), element =>
            element.Name.LocalName == "Button" &&
            (string?)element.Attribute("Click") == "OptionalPreloadRetryButton_OnClick");
        XElement optionalSkipButton = Assert.Single(shellDocument.Descendants(), element =>
            element.Name.LocalName == "Button" &&
            (string?)element.Attribute("Click") == "OptionalPreloadSkipButton_OnClick");
        XElement optionalPreloadStatusHost = Assert.Single(shellDocument.Descendants(), element =>
            element.Name.LocalName == "Border" &&
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" &&
                attribute.Value == "OptionalPreloadStatusHost"));
        XElement optionalPreloadExpander = Assert.Single(
            optionalPreloadStatusHost.Descendants(),
            element => element.Name.LocalName == "Expander");

        Assert.Contains("x:DataType=\"vm:ForegroundLoadingState\"", surface, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", status, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding AccessibleStatus}\"", surface, StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(surface, "AutomationProperties.Name=\"{Binding AccessibleStatus}\""));
        Assert.Contains("Background=\"{DynamicResource NfcModalScrimBrush}\"", surface, StringComparison.Ordinal);
        Assert.Contains("ContentTemplate=\"{DynamicResource ForegroundLoadingStatusTemplate}\"", surface, StringComparison.Ordinal);
        Assert.Contains("<ProgressBar", status, StringComparison.Ordinal);
        Assert.Contains("Maximum=\"1\"", status, StringComparison.Ordinal);
        Assert.Contains("IsIndeterminate=\"{Binding ShouldAnimate}\"", status, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding Progress}\"", status, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ProgressPercentLabel}\"", status, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding HasDeterminateProgress}\"", status, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsRunning}\"", status, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding CanRetry}\"", status, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding CanCancel}\"", status, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding RetryCommand}\"", status, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CancelCommand}\"", status, StringComparison.Ordinal);
        Assert.Equal("semanticAction secondary", (string?)cancelButton.Attribute("Classes"));
        Assert.Equal("semanticAction primary", (string?)retryButton.Attribute("Classes"));
        Assert.Equal("Button", (string?)semanticButtonTheme.Attribute("TargetType"));
        Assert.Null(semanticButtonTheme.Attribute("BasedOn"));
        Assert.Equal("{TemplateBinding Background}", (string?)semanticButtonPresenter.Attribute("Background"));
        Assert.Equal("{TemplateBinding BorderBrush}", (string?)semanticButtonPresenter.Attribute("BorderBrush"));
        Assert.Equal("{TemplateBinding BorderThickness}", (string?)semanticButtonPresenter.Attribute("BorderThickness"));
        Assert.Equal("{TemplateBinding Padding}", (string?)semanticButtonPresenter.Attribute("Padding"));
        Assert.Equal("{TemplateBinding Foreground}", (string?)semanticButtonPresenter.Attributes()
            .Single(attribute => attribute.Name.LocalName == "TextElement.Foreground"));
        Assert.Contains(
            "Theme\" Value=\"{StaticResource NfcSemanticButtonTheme}",
            globalButton,
            StringComparison.Ordinal);
        Assert.Contains("MinHeight\" Value=\"34\"", semanticAction, StringComparison.Ordinal);
        Assert.Contains("HorizontalContentAlignment\" Value=\"Center\"", semanticAction, StringComparison.Ordinal);
        Assert.Contains("VerticalContentAlignment\" Value=\"Center\"", semanticAction, StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"2\"", semanticAction, StringComparison.Ordinal);
        Assert.Contains("NfcFontSize13", semanticAction, StringComparison.Ordinal);
        Assert.Contains("FocusAdorner\" Value=\"{x:Null}\"", globalButton, StringComparison.Ordinal);
        Assert.Contains("Opacity\" Value=\"1\"", semanticActionDisabled, StringComparison.Ordinal);
        Assert.Equal("{DynamicResource NfcCompactCornerRadius}", (string?)semanticButtonPresenter.Attribute("CornerRadius"));
        Assert.Contains("BorderThickness\" Value=\"2\"", semanticActionFocus, StringComparison.Ordinal);
        Assert.Contains("NfcAccentBorderStrongBrush", semanticActionFocus, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceBrush", secondary, StringComparison.Ordinal);
        Assert.Contains("NfcBorderBrush", secondary, StringComparison.Ordinal);
        Assert.Contains("NfcAccentSurfaceBrush", secondaryHover, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", secondaryHover, StringComparison.Ordinal);
        Assert.Contains("NfcSecondaryActionPressedBrush", secondaryPressed, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", secondaryPressed, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceSubtleBrush", secondaryDisabled, StringComparison.Ordinal);
        Assert.Contains("NfcBorderMutedBrush", secondaryDisabled, StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"2\"", secondaryDisabled, StringComparison.Ordinal);
        Assert.Contains("NfcDangerSurfaceMutedBrush", dangerHover, StringComparison.Ordinal);
        Assert.Contains("NfcDangerBorderStrongBrush", dangerHover, StringComparison.Ordinal);
        Assert.Contains("NfcCriticalSurfaceBrush", dangerPressed, StringComparison.Ordinal);
        Assert.Contains("NfcCriticalBorderBrush", dangerPressed, StringComparison.Ordinal);
        Assert.True(
            buttonStyles.IndexOf("Button.danger:pointerover", StringComparison.Ordinal) >
            buttonStyles.IndexOf("Button.iconButton:pointerover", StringComparison.Ordinal));
        Assert.All(
            ReadPresentationXamlFiles()
                .Select(XDocument.Parse)
                .SelectMany(document => document.Descendants())
                .Where(element => ((string?)element.Attribute("Classes") ?? string.Empty)
                    .Contains("danger", StringComparison.Ordinal)),
            button => Assert.Contains("semanticAction", (string?)button.Attribute("Classes")));
        Assert.Contains("FocusAdorner\" Value=\"{x:Null}\"", navigation, StringComparison.Ordinal);
        Assert.Contains("CornerRadius\" Value=\"0\"", navigationPresenter, StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"0,0,0,2\"", navigationPresenter, StringComparison.Ordinal);
        Assert.Contains("NfcTextSecondaryBrush", navigationPresenter, StringComparison.Ordinal);
        Assert.Contains("Background\" Value=\"Transparent\"", navigationHover, StringComparison.Ordinal);
        Assert.Contains("BorderBrush\" Value=\"Transparent\"", navigationHover, StringComparison.Ordinal);
        Assert.Contains("Background\" Value=\"Transparent\"", navigationPressed, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", navigationPressed, StringComparison.Ordinal);
        Assert.Contains("NfcAccentBrush", navigationCheckedHover, StringComparison.Ordinal);
        Assert.Contains("NfcAccentBorderStrongBrush", navigationCheckedPressed, StringComparison.Ordinal);
        Assert.Contains("NfcTextDisabledBrush", navigationDisabled, StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"0,0,0,2\"", navigationFocus, StringComparison.Ordinal);
        Assert.Contains("NfcAccentBorderStrongBrush", navigationFocus, StringComparison.Ordinal);
        Assert.Equal(3, navigationButtons.Length);
        Assert.All(
            navigationButtons.SelectMany(button => button.Descendants().Where(element => element.Name.LocalName == "TextBlock")),
            text => Assert.Null(text.Attribute("Foreground")));
        Assert.Contains("behaviors:FocusOnRevealBehavior.IsEnabled=\"True\"", surface, StringComparison.Ordinal);
        Assert.Contains("Focusable=\"True\"", surface, StringComparison.Ordinal);
        Assert.Contains("AttachedToVisualTree += Control_OnAttachedToVisualTree", focusBehavior, StringComparison.Ordinal);
        Assert.Contains("QueueFocus(control);", focusBehavior, StringComparison.Ordinal);
        _ = Assert.Single(detailText);
        Assert.DoesNotContain("RetryRequested", surface, StringComparison.Ordinal);
        Assert.DoesNotContain("CancelRequested", surface, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CatalogLoadingSurfaceHost\"", shell, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ShellInteractionHost\"", shell, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OptionalPreloadStatusHost\"", shell, StringComparison.Ordinal);
        Assert.Equal(
            "{ReflectionBinding AccessibleStatus}",
            (string?)optionalPreloadExpander.Attribute("AutomationProperties.Name"));
        Assert.Equal("Polite", (string?)optionalPreloadExpander.Attribute("AutomationProperties.LiveSetting"));
        Assert.Equal("OptionalPreloadFocusTarget", optionalPreloadExpander.Attributes()
            .Single(attribute => attribute.Name.LocalName == "Name").Value);
        Assert.Equal("True", (string?)optionalPreloadExpander.Attribute("Focusable"));
        Assert.Equal("False", (string?)optionalPreloadExpander.Attribute("KeyboardNavigation.IsTabStop"));
        Assert.Contains(
            "Selector=\"Expander#OptionalPreloadFocusTarget /template/ ToggleButton\"",
            windowStyles,
            StringComparison.Ordinal);
        Assert.Contains(
            "Property=\"AutomationProperties.Name\" Value=\"{ReflectionBinding $parent[Expander].(AutomationProperties.Name)}\"",
            windowStyles,
            StringComparison.Ordinal);
        Assert.Contains("SummaryStage.PositionLabel", shell, StringComparison.Ordinal);
        Assert.Contains("SummaryStage.ProgressLabel", shell, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{ReflectionBinding AccessibleStatus}\"", shell, StringComparison.Ordinal);
        string optionalStatus = shell[
            shell.IndexOf("x:Name=\"OptionalPreloadStatusHost\"", StringComparison.Ordinal)..shell.IndexOf("x:Name=\"ReportToastHost\"", StringComparison.Ordinal)];
        Assert.Equal(1, CountOccurrences(optionalStatus, "AutomationProperties.LiveSetting=\"Polite\""));
        Assert.Contains("ItemsSource=\"{ReflectionBinding Stages}\"", shell, StringComparison.Ordinal);
        Assert.Contains("IsIndeterminate=\"{ReflectionBinding IsIndeterminate}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Text=\"{ReflectionBinding WorkLabel}\"", shell, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{ReflectionBinding RetryAccessibleLabel}\"", shell, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{ReflectionBinding SkipAccessibleLabel}\"", shell, StringComparison.Ordinal);
        Assert.Contains("Click=\"OptionalPreloadRetryButton_OnClick\"", shell, StringComparison.Ordinal);
        Assert.Contains("Click=\"OptionalPreloadSkipButton_OnClick\"", shell, StringComparison.Ordinal);
        Assert.Contains("Click=\"OptionalPreloadCancelButton_OnClick\"", shell, StringComparison.Ordinal);
        Assert.Equal("semanticAction secondary", (string?)optionalCancelButton.Attribute("Classes"));
        Assert.Equal("semanticAction secondary", (string?)optionalRetryButton.Attribute("Classes"));
        Assert.Equal("semanticAction secondary", (string?)optionalSkipButton.Attribute("Classes"));
        string[] semanticRoles = ["secondary", "danger", "action", "primary", "iconButton"];
        foreach (string role in semanticRoles)
        {
            _ = ExtractStyle(
                buttonStyles,
                $"Button.{role}:pointerover /template/ ContentPresenter#PART_ContentPresenter");
            _ = ExtractStyle(
                buttonStyles,
                $"Button.{role}:pressed /template/ ContentPresenter#PART_ContentPresenter");
            Assert.Contains(
                $"Button.{role}:disabled /template/ ContentPresenter#PART_ContentPresenter",
                buttonStyles,
                StringComparison.Ordinal);
        }
        foreach (XElement button in ReadPresentationXamlFiles()
                     .Select(XDocument.Parse)
                     .SelectMany(document => document.Descendants())
                     .Where(element => element.Name.LocalName == "Button"))
        {
            string[] classes = ((string?)button.Attribute("Classes") ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (classes.Any(semanticRoles.Contains))
            {
                Assert.Contains("semanticAction", classes);
            }
        }
        Assert.Contains("<Grid.KeyBindings>", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("<Window.KeyBindings>", shell, StringComparison.Ordinal);
        Assert.Contains("_preloadLoading = new(RetryStartupPreloadAsync, CancelStartupAsync);", lifecycle, StringComparison.Ordinal);
        Assert.Contains("_preloadLoading.Begin(", lifecycle, StringComparison.Ordinal);
        Assert.Contains("loading.Complete();", lifecycle, StringComparison.Ordinal);
        Assert.Contains("loading.Fail(", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CatalogLoadingSurfaceHost.DataContext = _preloadLoading;", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CatalogLoadingSurfaceHost.Content = _preloadLoading;", lifecycle, StringComparison.Ordinal);
        Assert.Contains("_preloadSession.RunCatalogAsync(", lifecycle, StringComparison.Ordinal);
        Assert.Contains("retry: _preloadSession.CatalogStage.CurrentAttempt is not null", lifecycle, StringComparison.Ordinal);
        Assert.Contains("ShellPreloadAttemptIdentity", preloadSession, StringComparison.Ordinal);
        Assert.Contains("PreviousAttempt", preloadSession, StringComparison.Ordinal);
        Assert.Contains("DrainTimeout = TimeSpan.FromSeconds(5)", preloadSession, StringComparison.Ordinal);
        Assert.Contains("OptionalWorkerBudget = 2", preloadSession, StringComparison.Ordinal);
        Assert.Contains("CurrentAttempt?.Identity != attempt.Identity", lifecycle, StringComparison.Ordinal);
        Assert.Contains(
            "_hostServices.CanonicalCatalogLoader",
            lifecycle,
            StringComparison.Ordinal);
        Assert.Contains("WithCancellation(token)", preloadSession, StringComparison.Ordinal);
        Assert.Contains("update.Result is { }", preloadSession, StringComparison.Ordinal);
        Assert.Contains("ValidateProgress(update.Progress", preloadSession, StringComparison.Ordinal);
        Assert.Contains("completed without a terminal result", preloadSession, StringComparison.Ordinal);
        Assert.Contains("if (!reload.Succeeded)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.Render", lifecycle, StringComparison.Ordinal);
        Assert.Contains("ShellInteractionHost.IsEnabled = false", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CommitRequiredStagePresentation(", lifecycle, StringComparison.Ordinal);
        Assert.Contains("_isStartupShellEnabled = enabled;", lifecycle, StringComparison.Ordinal);
        Assert.Contains("ApplyShellInteractionState(viewModel);", lifecycle, StringComparison.Ordinal);
        Assert.Contains("_isStartupShellEnabled,", lifecycle, StringComparison.Ordinal);
        Assert.Contains("bool interactive = isStartupShellEnabled &&", lifecycle, StringComparison.Ordinal);
        Assert.Contains("!viewModel.IsSettingsModalOpen &&", lifecycle, StringComparison.Ordinal);
        Assert.Contains("!viewModel.OutputDelivery.IsOpen", lifecycle, StringComparison.Ordinal);
        Assert.Contains("setShellEnabled(succeeded);", lifecycle, StringComparison.Ordinal);
        Assert.Contains("presentLoadingState();", lifecycle, StringComparison.Ordinal);
        Assert.Contains("HomeNavigationButton.Focus(NavigationMethod.Tab)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("_preloadSession.CancelAndDrainAsync()", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("reloadCatalog", opened, StringComparison.Ordinal);
        Assert.Contains("RunStartupPreloadAsync(", retry, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemInformation.Refresh(", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("WarmCanonicalCapabilities", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("Stopwatch", lifecycle, StringComparison.Ordinal);
        Assert.Contains(
            typeof(PresentationHostServices).GetConstructors(),
                constructor => constructor.GetParameters().Length == 9 &&
                constructor.GetParameters()[^2].ParameterType ==
                    typeof(Application.ExternalTools.IExternalProcessorEnvironmentLoader) &&
                constructor.GetParameters()[^1].ParameterType ==
                    typeof(ILocalFileStore));
    }

    /// <summary>The clear confirmation identifies the pending route visually and to assistive technology.</summary>
    [Fact]
    public void NavigationClearConfirmationBindsPendingRoute()
    {
        string modal = ReadPresentationFile("Views/NavigationClearConfirmationModal.axaml");

        Assert.Contains("Text=\"{Binding NavigationClearRoute}\"", modal, StringComparison.Ordinal);
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding NavigationClearRoute}\"",
            modal,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding NavigationPath}\"", modal, StringComparison.Ordinal);
    }

    /// <summary>The matrix detail tooltip owns its width so its wider content is not clipped.</summary>
    [Fact]
    public void SupportMatrixTooltipOwnsItsContentWidth()
    {
        var templates = XDocument.Parse(
            ReadPresentationFile("Resources/MainWindowPageTemplates.axaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement cellTemplate = Assert.Single(
            templates.Descendants(),
            element =>
                element.Name.LocalName == "DataTemplate" &&
                (string?)element.Attribute(x + "Key") == "SupportMatrixCellTemplate");
        XElement toolTip = Assert.Single(
            cellTemplate.Descendants(),
            element => element.Name.LocalName == "ToolTip");
        XElement content = Assert.Single(toolTip.Elements());

        Assert.Equal("560", (string?)toolTip.Attribute("Width"));
        Assert.Null(content.Attribute("Width"));
    }

    /// <summary>The shell names the report action by its purpose while preserving localized guidance.</summary>
    [Fact]
    public void ShellReportLoaderUsesLocalizedReportLabel()
    {
        var english = ShellTextResources.For(ShellLanguage.English);
        var traditionalChinese = ShellTextResources.For(ShellLanguage.ChineseTraditional);
        string shell = ReadPresentationFile("MainWindow.axaml");

        Assert.Equal("Load Report", english.LoadJsonLabel);
        Assert.Equal("Load a report", english.LoadJsonTooltip);
        Assert.Equal("載入報告", traditionalChinese.LoadJsonLabel);
        Assert.Equal("載入報告", traditionalChinese.LoadJsonTooltip);
        Assert.Contains("Text=\"{Binding Text.LoadJsonLabel}\"", shell, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding Text.LoadJsonTooltip}\"", shell, StringComparison.Ordinal);
    }
}
