using System.Xml.Linq;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Keeps inactive pages and large modals out of the Home visual tree until first use.</summary>
    [Fact]
    public void MainWindowDefersInactivePageAndModalContent()
    {
        var shell = XDocument.Parse(ReadPresentationFile("MainWindow.axaml"));
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

        Assert.Contains("ApplyDeferredShellContent(viewModel);", codeBehind, StringComparison.Ordinal);
    }

    /// <summary>Starts low-priority page warm-up only after the Home window is open.</summary>
    [Fact]
    public void MainWindowWarmsCommonPagesAfterFirstFrameWithoutLoadingModals()
    {
        string lifecycle = ReadPresentationFile("MainWindow.axaml.cs");
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
            lifecycle.IndexOf("WarmCanonicalCapabilities", StringComparison.Ordinal));
        Assert.Contains("Task.Run(", lifecycle, StringComparison.Ordinal);
        Assert.Contains("WarmCanonicalCapabilities(startupCancellation)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("viewModel.PublishCanonicalCatalogState()", lifecycle, StringComparison.Ordinal);
        int warm = lifecycle.IndexOf("await Task.Run(", StringComparison.Ordinal);
        int publish = lifecycle.IndexOf(
            "viewModel.PublishCanonicalCatalogState()",
            warm,
            StringComparison.Ordinal);
        int loadingComplete = lifecycle.IndexOf(
            "_catalogLoading.Complete();",
            publish,
            StringComparison.Ordinal);
        int successfulWarmup = lifecycle.IndexOf("return true;", loadingComplete, StringComparison.Ordinal);
        Assert.True(warm >= 0 && warm < publish);
        Assert.True(publish < loadingComplete);
        Assert.True(loadingComplete < successfulWarmup);
        Assert.True(
            lifecycle.IndexOf("TryWarmCanonicalCatalogAsync(viewModel, startupCancellation)", StringComparison.Ordinal) <
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

    /// <summary>Catalog warm-up uses the reusable foreground surface without fabricating percentage progress.</summary>
    [Fact]
    public void CatalogWarmupUsesAccessibleRetryableForegroundLoadingSurface()
    {
        string surface = ReadPresentationFile("Views/ForegroundLoadingSurface.axaml");
        string surfaceCode = ReadPresentationFile("Views/ForegroundLoadingSurface.axaml.cs");
        string shell = ReadPresentationFile("MainWindow.axaml");
        string lifecycle = ReadPresentationFile("MainWindow.axaml.cs");
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
        Assert.Contains("Background=\"{DynamicResource NfcModalScrimBrush}\"", surface, StringComparison.Ordinal);
        Assert.Contains("<ProgressBar", surface, StringComparison.Ordinal);
        Assert.Contains("Maximum=\"1\"", surface, StringComparison.Ordinal);
        Assert.Contains("IsIndeterminate=\"{Binding ShouldAnimate}\"", surface, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding Progress}\"", surface, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsRunning}\"", surface, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding CanRetry}\"", surface, StringComparison.Ordinal);
        Assert.Contains("behaviors:FocusOnRevealBehavior.IsEnabled=\"True\"", surface, StringComparison.Ordinal);
        Assert.Equal(2, detailText.Length);
        Assert.Contains(detailText, element => (string?)element.Attribute("IsVisible") == "{Binding IsRunning}");
        Assert.Contains(
            detailText,
            element => element.Ancestors().Any(ancestor =>
                (string?)ancestor.Attribute("IsVisible") == "{Binding HasFailed}"));
        Assert.Contains("RetryRequested", surfaceCode, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CatalogLoadingSurfaceHost\"", shell, StringComparison.Ordinal);
        Assert.Contains("RetryRequested=\"CatalogLoadingSurface_OnRetryRequested\"", shell, StringComparison.Ordinal);
        Assert.Contains("_catalogLoading.Begin(", lifecycle, StringComparison.Ordinal);
        Assert.Contains("_catalogLoading.Complete();", lifecycle, StringComparison.Ordinal);
        Assert.Contains("_catalogLoading.Fail(", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CatalogLoadingSurfaceHost.DataContext = _catalogLoading;", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CatalogLoadingSurfaceHost.Content = _catalogLoading;", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportProgress(", lifecycle, StringComparison.Ordinal);
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
