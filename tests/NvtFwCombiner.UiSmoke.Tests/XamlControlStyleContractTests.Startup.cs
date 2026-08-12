using System.Xml.Linq;
using NvtFwCombiner.Application.Capabilities;
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
        string catalogCoordinator = ReadPresentationFile("CanonicalCatalogStartupCoordinator.cs");
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
            lifecycle.IndexOf("CanonicalCatalogStartupCoordinator.LoadAndApplyAsync", StringComparison.Ordinal));
        Assert.Contains(
            "loader.LoadAsync(cancellationToken).WithCancellation(cancellationToken)",
            catalogCoordinator,
            StringComparison.Ordinal);
        Assert.Contains("viewModel.PublishCanonicalCatalogState();", lifecycle, StringComparison.Ordinal);
        int warm = lifecycle.IndexOf(
            "CanonicalCatalogStartupCoordinator.LoadAndApplyAsync(",
            StringComparison.Ordinal);
        int reloadAccepted = lifecycle.IndexOf(
            "if (!reload.Succeeded)",
            warm,
            StringComparison.Ordinal);
        int loadingComplete = lifecycle.IndexOf(
            "CompleteCatalogLoading();",
            reloadAccepted,
            StringComparison.Ordinal);
        int successfulWarmup = lifecycle.IndexOf("return true;", loadingComplete, StringComparison.Ordinal);
        int successfulTerminal = catalogCoordinator.IndexOf(
            "if (!result.Succeeded)",
            StringComparison.Ordinal);
        int publicationStart = catalogCoordinator.IndexOf(
            "CanonicalCatalogStartupPhase.ApplyingState",
            successfulTerminal,
            StringComparison.Ordinal);
        int publish = catalogCoordinator.IndexOf(
            "applyValidatedState(cancellationToken)",
            publicationStart,
            StringComparison.Ordinal);
        int catalogPublished = catalogCoordinator.IndexOf(
            "CanonicalCatalogStartupPhase.Ready",
            publish,
            StringComparison.Ordinal);
        Assert.True(warm >= 0);
        Assert.True(warm < reloadAccepted);
        Assert.True(reloadAccepted < loadingComplete);
        Assert.True(publicationStart < publish);
        Assert.True(publish < catalogPublished);
        Assert.True(loadingComplete < successfulWarmup);
        Assert.True(
            lifecycle.IndexOf("TryWarmCanonicalCatalogAsync(", StringComparison.Ordinal) <
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

    /// <summary>Catalog warm-up combines continuous activity with source-owned route progress on the reusable foreground surface.</summary>
    [Fact]
    public void CatalogWarmupUsesAccessibleRetryableForegroundLoadingSurface()
    {
        string surface = ReadPresentationFile("Views/ForegroundLoadingSurface.axaml");
        string surfaceCode = ReadPresentationFile("Views/ForegroundLoadingSurface.axaml.cs");
        string shell = ReadPresentationFile("MainWindow.axaml");
        string lifecycle = ReadPresentationFile("MainWindow.axaml.cs");
        string coordinator = ReadPresentationFile("CanonicalCatalogStartupCoordinator.cs");
        string focusBehavior = ReadPresentationFile("Behaviors/FocusOnRevealBehavior.cs");
        int openedStart = lifecycle.IndexOf(
            "protected override async void OnOpened",
            StringComparison.Ordinal);
        int coordinatorStart = lifecycle.IndexOf(
            "private async Task ContinueStartupAsync",
            openedStart,
            StringComparison.Ordinal);
        int retryStart = lifecycle.IndexOf(
            "private async void CatalogLoadingSurface_OnRetryRequested",
            coordinatorStart,
            StringComparison.Ordinal);
        int propertyChangedStart = lifecycle.IndexOf(
            "private void ViewModel_OnPropertyChanged",
            retryStart,
            StringComparison.Ordinal);
        string opened = lifecycle[openedStart..coordinatorStart];
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
        Assert.Contains("x:Name=\"CatalogLoadingSurfaceHost\"", shell, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ShellInteractionHost\"", shell, StringComparison.Ordinal);
        Assert.Contains("<Grid.KeyBindings>", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("<Window.KeyBindings>", shell, StringComparison.Ordinal);
        Assert.Contains("RetryRequested=\"CatalogLoadingSurface_OnRetryRequested\"", shell, StringComparison.Ordinal);
        Assert.Contains("_catalogLoading.Begin(", lifecycle, StringComparison.Ordinal);
        Assert.Contains("_catalogLoading.Complete();", lifecycle, StringComparison.Ordinal);
        Assert.Contains("_catalogLoading.Fail(", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CatalogLoadingSurfaceHost.DataContext = _catalogLoading;", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CatalogLoadingSurfaceHost.Content = _catalogLoading;", lifecycle, StringComparison.Ordinal);
        Assert.Contains("DispatchedProgress = 0.1", coordinator, StringComparison.Ordinal);
        Assert.Contains("RouteProgressRange = 0.7", coordinator, StringComparison.Ordinal);
        Assert.Contains("ApplyingProgress = 0.9", coordinator, StringComparison.Ordinal);
        Assert.Contains("CanonicalCatalogStartupCoordinator.LoadAndApplyAsync(", lifecycle, StringComparison.Ordinal);
        Assert.Contains("int attempt = ++_catalogLoadingAttempt", lifecycle, StringComparison.Ordinal);
        Assert.Contains("ApplyCatalogProgress(", lifecycle, StringComparison.Ordinal);
        Assert.Contains("attempt != currentAttempt", lifecycle, StringComparison.Ordinal);
        Assert.Contains("!double.IsFinite(progress.Value)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("RouteProgressRange * sourceProgress", coordinator, StringComparison.Ordinal);
        Assert.Contains("Math.Floor(", coordinator, StringComparison.Ordinal);
        Assert.Contains(
            "_hostServices.CanonicalCatalogLoader",
            lifecycle,
            StringComparison.Ordinal);
        Assert.Contains("WithCancellation(cancellationToken)", coordinator, StringComparison.Ordinal);
        Assert.Contains("update.Result is { } terminalUpdate", coordinator, StringComparison.Ordinal);
        Assert.Contains("double sourceProgress = update.Progress ??", coordinator, StringComparison.Ordinal);
        Assert.Contains("completed without a terminal result", coordinator, StringComparison.Ordinal);
        Assert.Contains("if (!reload.Succeeded)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.Render", lifecycle, StringComparison.Ordinal);
        Assert.Contains("ShellInteractionHost.IsEnabled = false", lifecycle, StringComparison.Ordinal);
        Assert.Contains("ShellInteractionHost.IsEnabled = true", lifecycle, StringComparison.Ordinal);
        Assert.Contains("HomeNavigationButton.Focus(NavigationMethod.Tab)", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("reloadCatalog", opened, StringComparison.Ordinal);
        Assert.Contains("ContinueStartupAsync(", retry, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemInformation.Refresh(", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("WarmCanonicalCapabilities", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("Stopwatch", lifecycle, StringComparison.Ordinal);
        Assert.Contains(
            typeof(PresentationHostServices).GetConstructors(),
                constructor => constructor.GetParameters().Length == 7 &&
                constructor.GetParameters()[^1].ParameterType ==
                    typeof(ICanonicalCapabilityCatalogLoader));
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
