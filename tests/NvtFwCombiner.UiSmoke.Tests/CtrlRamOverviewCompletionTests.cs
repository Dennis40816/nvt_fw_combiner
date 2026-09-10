using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Real IC inputs expose canonical outer firmware context without changing CtrlRAM targets.</summary>
public sealed class CtrlRamOverviewCompletionTests
{
    /// <summary>The family alias and DP-container ICs show both TP and DP when a full image is loaded.</summary>
    [AvaloniaTheory]
    [InlineData("NT51919", "nt51929-fw200-single-auto-prj-594-20260717", "postbuild-nf-ctrlram")]
    [InlineData("NT51950", "nt51950-fw200-single-auto-prj-676-20260717", "postbuild-nf-ctrlram")]
    [InlineData("NT51951", "nt51951-fw200-single-auto-prj-695-20260718", "nf-ctrlram-input")]
    public async Task FullFlashInputsShowTpAndDpContext(string ic, string caseId, string nfArtifact)
    {
        using var workspace = TempWorkspace.Create("ctrlram-overview-completion");
        PresentationHostServices services = await CreateServicesAsync(workspace, useRetainedDpReplacePolicy: false);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default)
        { Width = 1440, Height = 1040, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            JsonElement fixture = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", caseId);
            string PathFor(string id)
            {
                return CanonicalGoldenTestData.ArtifactPath(fixture.GetProperty("artifacts").EnumerateArray()
                    .Single(artifact => artifact.GetProperty("artifactId").GetString() == id));
            }
            // NT51919 uses its owner-approved NT51929 fact-scoped input alias, not a new Golden claim.
            UiLaunchOptions options = UiLaunchOptions.Parse(["--workflow", "ctrlram-replace", "--ic", ic,
                "--ic-num", "single", "--base", PathFor("expected-output"),
                "--ctrlram", "replace-ctrlram-nf=" + PathFor(nfArtifact)]);
            Assert.Empty(options.Issues);
            await MainWindow.ApplyCtrlRamLaunchAsync(shell, options.CtrlRam!, TestContext.Current.CancellationToken);
            if (shell.WorkflowSession.IsFirmwareIcMismatchModalOpen)
            {
                Assert.Equal("NT51919", ic);
                shell.WorkflowSession.DismissFirmwareIcMismatchCommand.Execute(null);
            }
            Assert.Equal(WorkflowInspectionAttemptState.Succeeded, shell.Replace.Inspection.State);
            Assert.True(shell.Replace.CanBuildReplace);
            Assert.False(shell.Replace.HasMemoryLayoutDisplayError);
            CtrlRamCascadeMemoryLayoutTests.Capture(window, ic + "-single-overview");
            Assert.Contains(shell.Replace.CtrlRamOverview, section => section.ContentRole == MemoryContentRole.Tp);
            Assert.Contains(shell.Replace.CtrlRamOverview, section => section.ContentRole == MemoryContentRole.Dp);
            Assert.Equal(3, shell.Replace.CtrlRamOverview.Count);
            Assert.Equal(ic == "NT51919" ? 1 : 2, shell.Replace.CtrlRamOverview.Count(section => section.ContentRole == MemoryContentRole.Dp));
            Assert.All(shell.Replace.CtrlRamOverview.Where(section => section.ContentRole == MemoryContentRole.Dp),
                section => Assert.Equal("DP", section.DisplayTitle));
            Assert.Contains(shell.Replace.CtrlRamFocusLanes, lane => lane.Title == "Master");
            Assert.DoesNotContain(shell.Replace.CtrlRamFocusLanes, lane => lane.Title == "Common");
            MemoryFocusLaneViewModel master = Assert.Single(shell.Replace.CtrlRamFocusLanes, lane => lane.Title == "Master");
            Assert.All(master.Ranges, range => Assert.Equal(ReplaceRegionGroup.Common, range.RegionGroup));
            Assert.Equal("M", master.PositionLabel);
            Assert.Contains(shell.Replace.ReplaceSlotGroups, group => group.Title == "Common");
            // Capturing is optional in CI; realization must not depend on its
            // NFC_VISUAL_OUTPUT_DIR-gated render loop.
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            _ = CtrlRamMemoryLayoutTests.OpenLane(window, master);
            CtrlRamCascadeMemoryLayoutTests.Capture(window, ic + "-single-master-hover");
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), block =>
                block.IsEffectivelyVisible && block.Text == "Master");
            foreach (MemoryContentRole role in new[] { MemoryContentRole.Dp, MemoryContentRole.Tp })
            {
                Control target = window.GetVisualDescendants().OfType<Control>().First(control =>
                    control.Focusable && control.Classes.Contains("memoryExplorerSlice") &&
                    !control.Classes.Contains("memoryLocalSlice") && control.DataContext is MemoryCoverageSegmentViewModel segment && segment.ContentRole == role);
                Assert.True(target.Focus(NavigationMethod.Tab));
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                CtrlRamCascadeMemoryLayoutTests.Capture(window, ic + "-direct-" + role);
                Border card = Assert.Single(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemorySliceCard");
                Assert.Same(target.DataContext, card.DataContext);
                Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), control => control.Name == "MemoryLocalView");
            }
            shell.SelectedLanguage = "Traditional Chinese";
            window.RequestedThemeVariant = ThemeVariant.Dark;
            CtrlRamCascadeMemoryLayoutTests.Capture(window, ic + "-single-overview-dark-zh");
            Assert.All(shell.Replace.CtrlRamOverview.Where(section => section.ContentRole == MemoryContentRole.Dp),
                section => Assert.Equal("DP", section.DisplayTitle));
            Assert.Contains(shell.Replace.CtrlRamFocusLanes, lane => lane.Title == "主 IC");
            Assert.DoesNotContain(shell.Replace.CtrlRamFocusLanes, lane => lane.Title == "共用");
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            _ = CtrlRamMemoryLayoutTests.OpenLane(window, Assert.Single(shell.Replace.CtrlRamFocusLanes, lane => lane.Title == "主 IC"));
            CtrlRamCascadeMemoryLayoutTests.Capture(window, ic + "-single-master-hover-dark-zh");
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), block =>
                block.IsEffectivelyVisible && block.Text == "主 IC");
            Assert.True(shell.Replace.CanBuildReplace);
            Assert.Empty(shell.Reports.ReportHistoryEntries);
        }
        finally { await CloseAndFlushAsync(window); }
    }
}
