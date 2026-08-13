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
        int warm = lifecycle.IndexOf(
            "_preloadSession.RunCatalogAsync(",
            StringComparison.Ordinal);
        int reloadAccepted = lifecycle.IndexOf(
            "if (!reload.Succeeded)",
            warm,
            StringComparison.Ordinal);
        int successfulWarmup = lifecycle.IndexOf("return true;", reloadAccepted, StringComparison.Ordinal);
        int successfulTerminal = preloadSession.IndexOf(
            "if (!result.Succeeded)",
            StringComparison.Ordinal);
        int publicationStart = preloadSession.IndexOf(
            "Set(identity, ShellPreloadAttemptState.Running, 1)",
            successfulTerminal,
            StringComparison.Ordinal);
        int publish = preloadSession.IndexOf(
            "await apply(token)",
            publicationStart,
            StringComparison.Ordinal);
        int catalogPublished = preloadSession.IndexOf(
            "ShellPreloadAttemptState.Succeeded",
            publish,
            StringComparison.Ordinal);
        Assert.True(warm >= 0);
        Assert.True(warm < reloadAccepted);
        Assert.True(publicationStart < publish);
        Assert.True(publish < catalogPublished);
        Assert.True(reloadAccepted < successfulWarmup);
        Assert.True(
            lifecycle.IndexOf("RunRequiredPreloadAsync(", StringComparison.Ordinal) <
            lifecycle.IndexOf("ApplyLaunchPage(viewModel, _launchOptions.Page)", StringComparison.Ordinal));
        Assert.True(
            lifecycle.IndexOf("ApplyLaunchPage(viewModel, _launchOptions.Page)", StringComparison.Ordinal) <
            lifecycle.IndexOf("await ApplyDeferredLaunchOptionsAsync", StringComparison.Ordinal));
        Assert.True(
            lifecycle.IndexOf("await ApplyDeferredLaunchOptionsAsync", StringComparison.Ordinal) <
            lifecycle.IndexOf("await viewModel.MessageCenter.RefreshAfterStartupAsync", StringComparison.Ordinal));
        Assert.True(
            lifecycle.IndexOf("await viewModel.MessageCenter.RefreshAfterStartupAsync", StringComparison.Ordinal) <
            lifecycle.IndexOf("viewModel.Settings.Refresh(viewModel.Text)", StringComparison.Ordinal));
        Assert.Contains("Deferred shell warm-up did not complete", lifecycle, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.Background", warmup, StringComparison.Ordinal);
        Assert.Contains("$\"{traceStage}.started\"", warmup, StringComparison.Ordinal);
        Assert.Contains("$\"{traceStage}.ready\"", warmup, StringComparison.Ordinal);
        Assert.Contains("host.ContentTemplate?.Build(dataContext)", warmup, StringComparison.Ordinal);
        Assert.Contains("host.Content = content", warmup, StringComparison.Ordinal);
        Assert.Contains("IsRunInProgress: true", warmup, StringComparison.Ordinal);
        foreach (string hostName in warmedHosts)
        {
            Assert.Contains($"{hostName},", warmup, StringComparison.Ordinal);
        }

        Assert.Contains(
            "ReplacePageHost,\n            viewModel.Replace,",
            warmup,
            StringComparison.Ordinal);
        Assert.Contains(
            "MergePageHost,\n            viewModel.Merge,",
            warmup,
            StringComparison.Ordinal);

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
        string surfaceCode = ReadPresentationFile("Views/ForegroundLoadingSurface.axaml.cs");
        string shell = ReadPresentationFile("MainWindow.axaml");
        string lifecycle = ReadPresentationFile("MainWindow.axaml.cs");
        string preloadSession = ReadPresentationFile("ShellPreloadSession.cs");
        string focusBehavior = ReadPresentationFile("Behaviors/FocusOnRevealBehavior.cs");
        int openedStart = lifecycle.IndexOf(
            "protected override async void OnOpened",
            StringComparison.Ordinal);
        int sessionStart = lifecycle.IndexOf(
            "private async Task ContinueStartupAsync",
            openedStart,
            StringComparison.Ordinal);
        int retryStart = lifecycle.IndexOf(
            "private async void CatalogLoadingSurface_OnRetryRequested",
            sessionStart,
            StringComparison.Ordinal);
        int propertyChangedStart = lifecycle.IndexOf(
            "private void ViewModel_OnPropertyChanged",
            retryStart,
            StringComparison.Ordinal);
        string opened = lifecycle[openedStart..sessionStart];
        string retry = lifecycle[retryStart..propertyChangedStart];
        var surfaceDocument = XDocument.Parse(surface);
        XElement[] detailText =
        [
            .. surfaceDocument.Descendants()
                .Where(element =>
                    element.Name.LocalName == "TextBlock" &&
                    (string?)element.Attribute("Text") == "{Binding Detail}"),
        ];

        Assert.Contains("x:DataType=\"vm:ForegroundLoadingState\"", surface, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", surface, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding AccessibleStatus}\"", surface, StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(surface, "AutomationProperties.Name=\"{Binding AccessibleStatus}\""));
        Assert.Contains("Background=\"{DynamicResource NfcModalScrimBrush}\"", surface, StringComparison.Ordinal);
        Assert.Contains("<ProgressBar", surface, StringComparison.Ordinal);
        Assert.Contains("Maximum=\"1\"", surface, StringComparison.Ordinal);
        Assert.Contains("IsIndeterminate=\"{Binding ShouldAnimate}\"", surface, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding Progress}\"", surface, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ProgressPercentLabel}\"", surface, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding HasDeterminateProgress}\"", surface, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsRunning}\"", surface, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding CanRetry}\"", surface, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding CanCancel}\"", surface, StringComparison.Ordinal);
        Assert.Contains("behaviors:FocusOnRevealBehavior.IsEnabled=\"True\"", surface, StringComparison.Ordinal);
        Assert.Contains("Focusable=\"True\"", surface, StringComparison.Ordinal);
        Assert.Contains("AttachedToVisualTree += Control_OnAttachedToVisualTree", focusBehavior, StringComparison.Ordinal);
        Assert.Contains("QueueFocus(control);", focusBehavior, StringComparison.Ordinal);
        Assert.Equal(2, detailText.Length);
        Assert.Contains(detailText, element => (string?)element.Attribute("IsVisible") == "{Binding IsRunning}");
        Assert.Contains(
            detailText,
            element => element.Ancestors().Any(ancestor =>
                (string?)ancestor.Attribute("IsVisible") == "{Binding HasFailed}"));
        Assert.Contains("RetryRequested", surfaceCode, StringComparison.Ordinal);
        Assert.Contains("CancelRequested", surfaceCode, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CatalogLoadingSurfaceHost\"", shell, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ShellInteractionHost\"", shell, StringComparison.Ordinal);
        Assert.Contains("<Grid.KeyBindings>", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("<Window.KeyBindings>", shell, StringComparison.Ordinal);
        Assert.Contains("RetryRequested=\"CatalogLoadingSurface_OnRetryRequested\"", shell, StringComparison.Ordinal);
        Assert.Contains("CancelRequested=\"CatalogLoadingSurface_OnCancelRequested\"", shell, StringComparison.Ordinal);
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
        Assert.Contains("update.Result is { } terminalResult", preloadSession, StringComparison.Ordinal);
        Assert.Contains("double next = update.Progress ??", preloadSession, StringComparison.Ordinal);
        Assert.Contains("completed without a terminal result", preloadSession, StringComparison.Ordinal);
        Assert.Contains("if (!reload.Succeeded)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.Render", lifecycle, StringComparison.Ordinal);
        Assert.Contains("ShellInteractionHost.IsEnabled = false", lifecycle, StringComparison.Ordinal);
        Assert.Contains("ShellInteractionHost.IsEnabled = true", lifecycle, StringComparison.Ordinal);
        Assert.Contains("HomeNavigationButton.Focus(NavigationMethod.Tab)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("_preloadSession.CancelAndDrainAsync()", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("reloadCatalog", opened, StringComparison.Ordinal);
        Assert.Contains("ContinueStartupAsync(", retry, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemInformation.Refresh(", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("WarmCanonicalCapabilities", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("Stopwatch", lifecycle, StringComparison.Ordinal);
        Assert.Contains(
            typeof(PresentationHostServices).GetConstructors(),
                constructor => constructor.GetParameters().Length == 8 &&
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
