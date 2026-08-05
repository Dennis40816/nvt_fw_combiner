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
            lifecycle.IndexOf("PrimeDeferredCatalogs", StringComparison.Ordinal));
        Assert.Contains("Task.Run(", lifecycle, StringComparison.Ordinal);
        Assert.Contains("WarmCanonicalCapabilities(cancellationToken)", warmup, StringComparison.Ordinal);
        Assert.True(
            lifecycle.IndexOf("await catalogWarmup", StringComparison.Ordinal) <
            lifecycle.IndexOf("viewModel.Settings.Refresh(viewModel.Text)", StringComparison.Ordinal));
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

        Assert.DoesNotContain("ReportModalHost,", warmup, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportToastHost,", warmup, StringComparison.Ordinal);
        Assert.DoesNotContain("FirmwareIcMismatchModalHost,", warmup, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigationClearConfirmationModalHost,", warmup, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", warmup, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.", warmup, StringComparison.Ordinal);
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
