using System.Xml.Linq;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Presentation.Avalonia;

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
            "SettingsPageHost",
            "HexEditorPageHost",
            "ReplacePageHost",
            "MergePageHost",
            "ReportToastHost",
            "ReplaceSelectionModalHost",
            "CtrlRamFirmwareVersionModalHost",
            "AbAFlashCodeDeliveryPromptModalHost",
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
            "SettingsPageHost",
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
        string semanticAction = ExtractStyle(buttonStyles, "Button.semanticAction");
        string semanticActionPresenter = ExtractStyle(
            buttonStyles,
            "Button.semanticAction /template/ ContentPresenter#PART_ContentPresenter");
        string secondary = ExtractStyle(buttonStyles, "Button.secondary");
        string secondaryHover = ExtractStyle(
            buttonStyles,
            "Button.secondary:pointerover /template/ ContentPresenter#PART_ContentPresenter");
        string secondaryPressed = ExtractStyle(
            buttonStyles,
            "Button.secondary:pressed /template/ ContentPresenter#PART_ContentPresenter");
        string secondaryFocus = ExtractStyle(
            buttonStyles,
            "Button.secondary:focus-visible /template/ ContentPresenter#PART_ContentPresenter");
        string secondaryDisabled = ExtractStyle(
            buttonStyles,
            "Button.secondary:disabled /template/ ContentPresenter#PART_ContentPresenter");
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
        var shellDocument = XDocument.Parse(shell);
        XElement optionalCancelButton = Assert.Single(shellDocument.Descendants(), element =>
            element.Name.LocalName == "Button" &&
            (string?)element.Attribute("Click") == "OptionalPreloadCancelButton_OnClick");
        XElement optionalRetryButton = Assert.Single(shellDocument.Descendants(), element =>
            element.Name.LocalName == "Button" &&
            (string?)element.Attribute("Click") == "OptionalPreloadRetryButton_OnClick");
        XElement optionalSkipButton = Assert.Single(shellDocument.Descendants(), element =>
            element.Name.LocalName == "Button" &&
            (string?)element.Attribute("Click") == "OptionalPreloadSkipButton_OnClick");

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
        Assert.Contains("MinHeight\" Value=\"34\"", semanticAction, StringComparison.Ordinal);
        Assert.Contains("HorizontalContentAlignment\" Value=\"Center\"", semanticAction, StringComparison.Ordinal);
        Assert.Contains("VerticalContentAlignment\" Value=\"Center\"", semanticAction, StringComparison.Ordinal);
        Assert.Contains("NfcFontSize13", semanticAction, StringComparison.Ordinal);
        Assert.Contains("FocusAdorner\" Value=\"{x:Null}\"", semanticAction, StringComparison.Ordinal);
        Assert.Contains("NfcPillCornerRadius", semanticActionPresenter, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceBrush", secondary, StringComparison.Ordinal);
        Assert.Contains("NfcAccentSurfaceSubtleBrush", secondaryHover, StringComparison.Ordinal);
        Assert.Contains("NfcAccentBorderBrush", secondaryHover, StringComparison.Ordinal);
        Assert.Contains("NfcAccentSurfaceBrush", secondaryPressed, StringComparison.Ordinal);
        Assert.Contains("NfcAccentBorderStrongBrush", secondaryPressed, StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"2\"", secondaryFocus, StringComparison.Ordinal);
        Assert.Contains("NfcAccentBorderStrongBrush", secondaryFocus, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceSubtleBrush", secondaryDisabled, StringComparison.Ordinal);
        Assert.Contains("NfcBorderMutedBrush", secondaryDisabled, StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"1\"", secondaryDisabled, StringComparison.Ordinal);
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
        Assert.Contains("enabled => ShellInteractionHost.IsEnabled = enabled", lifecycle, StringComparison.Ordinal);
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
}
