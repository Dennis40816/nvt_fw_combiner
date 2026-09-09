using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Pure preview failures cannot change canonical input health or Build admission.</summary>
public sealed class CtrlRamMemoryDisplayFailureTests
{
    /// <summary>Fault injection changes only display ranges, never inspection or authoring results.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidDisplayBindingDoesNotBlockValidCascadeInputs(bool dark)
    {
        using var workspace = TempWorkspace.Create("ctrlram-display-failure");
        // The shipped 951 has no alternate Replace mode. The Dark case uses the
        // existing test-only retained DP policy solely to exercise mode cleanup.
        PresentationHostServices original = await CreateServicesAsync(workspace, useRetainedDpReplacePolicy: dark);
        bool failDisplay = true;
        var inspection = new DelegatingFirmwareInspection(original.Composition.FirmwareInspection,
            displayProjector: display => !failDisplay ? display : display with
            {
                Regions = [.. display.Regions.Select(region => region.Role == CtrlRamRegionRole.DiffDlm
                    ? region with { Length = region.Length - 1 } : region)],
            });
        var services = new PresentationHostServices(original.Composition.WithFirmwareInspection(inspection),
            original.FileReveal, original.SupportMatrix, original.SystemInformation,
            original.SystemDiagnosticsExporter, original.RawBinaryEditorFileSessions,
            original.CanonicalCatalogLoader, original.ExternalEnvironmentLoader, original.LocalFiles);
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default)
        { Width = 1440, Height = 1040, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            await AwaitHistoryReadyAsync(window);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            await CtrlRamCascadeMemoryLayoutTests.LoadCascadeAsync(shell, "NT51951");
            Assert.Equal(WorkflowInspectionAttemptState.Succeeded, shell.Replace.Inspection.State);
            Assert.False(shell.Replace.ReplaceBaseSlot.IsSemanticStateError);
            Assert.True(shell.Replace.CanBuildReplace, shell.Replace.ReplaceReadinessStatus);
            AssertPreviewUnavailable(shell.Replace);
            Border warning = Assert.Single(window.GetVisualDescendants().OfType<Border>(),
                control => control.Name == "ReplaceMemoryDisplayWarning");
            Assert.True(warning.IsEffectivelyVisible);
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(warning));
            CtrlRamCascadeMemoryLayoutTests.Capture(window, "preview-unavailable-" + (dark ? "dark" : "light"));
            Assert.True(warning.Bounds.Width > 200);
            shell.SelectedLanguage = "Traditional Chinese";
            Assert.Equal("無法顯示 Memory Layout", shell.Replace.Text.MemoryLayoutUnavailableTitle);
            Assert.True(shell.Replace.CanBuildReplace);
            CtrlRamCascadeMemoryLayoutTests.Capture(window, "preview-unavailable-" + (dark ? "dark" : "light") + "-zh");
            shell.SelectedLanguage = "English";

            string basePath = shell.Replace.ReplaceBaseSlot.FilePath!;
            Dictionary<string, string> inputPaths = shell.Replace.ReplaceSlots
                .Where(slot => slot.SlotKind == FirmwareSlotKind.CtrlRam)
                .ToDictionary(slot => slot.SlotId, slot => slot.FilePath!);
            failDisplay = false;
            await shell.WorkflowSession.SetSlotFileAsync("replace-base", basePath, TestContext.Current.CancellationToken);
            Assert.False(shell.Replace.HasMemoryLayoutDisplayError);
            Assert.NotEmpty(shell.Replace.CtrlRamFocusLanes);
            Assert.True(shell.Replace.CanBuildReplace);

            failDisplay = true;
            await shell.WorkflowSession.SetSlotFileAsync("replace-base", basePath, TestContext.Current.CancellationToken);
            AssertPreviewUnavailable(shell.Replace);
            Assert.True(shell.Replace.CanBuildReplace);
            await shell.WorkflowSession.ClearSlotFileAsync("replace-base", TestContext.Current.CancellationToken);
            Assert.False(shell.Replace.HasMemoryLayoutDisplayError);
            Assert.False(shell.Replace.CanBuildReplace);

            await shell.WorkflowSession.SetSlotFileAsync("replace-base", workspace.Write("invalid-base.bin", new byte[8]),
                TestContext.Current.CancellationToken);
            Assert.True(shell.Replace.ReplaceBaseSlot.IsSemanticStateError);
            Assert.False(shell.Replace.CanBuildReplace);
            await shell.WorkflowSession.SetSlotFileAsync("replace-base", basePath, TestContext.Current.CancellationToken);
            foreach ((string slot, string path) in inputPaths)
            {
                await shell.WorkflowSession.SetSlotFileAsync(slot, path, TestContext.Current.CancellationToken);
            }
            AssertPreviewUnavailable(shell.Replace);
            if (dark)
            {
                Assert.Contains(ExperienceIds.DpReplace, shell.Replace.ReplaceModeChoices);
                shell.Replace.SelectedReplaceMode = ExperienceIds.DpReplace;
                Assert.Equal(ExperienceIds.DpReplace, shell.Replace.SelectedReplaceMode);
                Assert.False(shell.Replace.HasMemoryLayoutDisplayError);
            }
            Assert.Empty(shell.Reports.ReportHistoryEntries);
        }
        finally { await CloseAndFlushAsync(window); }
    }

    private static void AssertPreviewUnavailable(ReplacePresentationViewModel viewModel)
    {
        Assert.True(viewModel.HasMemoryLayoutDisplayError);
        Assert.Empty(viewModel.ReplaceMemoryRangeLabel);
        Assert.Empty(viewModel.ReplaceMemoryRows);
        Assert.Empty(viewModel.ReplaceCoverageSegments);
        Assert.Empty(viewModel.CoverageDetails.VisibleRows);
        Assert.Empty(viewModel.ReplaceCoverageGroups);
        Assert.Empty(viewModel.CtrlRamFocusLanes);
        Assert.Empty(viewModel.CtrlRamOverview);
        Assert.NotEmpty(viewModel.CtrlRamRegions);
        Assert.Equal(3, viewModel.ReplaceSlots.Count(slot => slot.SlotKind == FirmwareSlotKind.CtrlRam && slot.HasFile));
    }
}
