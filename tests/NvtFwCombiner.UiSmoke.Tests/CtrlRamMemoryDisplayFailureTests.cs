using System.Text.Json;
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
        // NT51951 validates the cascade preview. NT51926 provides the two surviving Replace modes.
        PresentationHostServices original = await CreateServicesAsync(workspace);
        bool failDisplay = true;
        bool failFirstRegion = false;
        var inspection = new DelegatingFirmwareInspection(original.Composition.FirmwareInspection,
            displayProjector: display => !failDisplay ? display : display with
            {
                Regions = [.. display.Regions.Select((region, index) => region.Role == CtrlRamRegionRole.DiffDlm || (failFirstRegion && index == 0)
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
                shell.WorkflowSession.SelectedIc = "NT51926";
                shell.WorkflowSession.SelectedNumber = "single";
                shell.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;
                failFirstRegion = true;
                JsonElement singleCase = CanonicalGoldenTestData.LoadDirectCase(
                    "ctrlram-replace", "nt51926-fw200-single-auto-prj-597-20260718");
                string singleBase = CanonicalGoldenTestData.ArtifactPath(singleCase.GetProperty("artifacts").EnumerateArray()
                    .Single(artifact => artifact.GetProperty("artifactId").GetString() == "expected-output"));
                string singleSource = CanonicalGoldenTestData.ArtifactPath(singleCase.GetProperty("artifacts").EnumerateArray()
                    .Single(artifact => artifact.GetProperty("artifactId").GetString() == "normal-ctrlram-input"));
                await shell.WorkflowSession.SetSlotFileAsync("replace-base", singleBase, TestContext.Current.CancellationToken);
                await shell.WorkflowSession.SetSlotFileAsync("replace-ctrlram-normal", singleSource, TestContext.Current.CancellationToken);
                Assert.Equal("NT51926", shell.WorkflowSession.SelectedIc);
                Assert.Equal(ExperienceIds.CtrlRamReplace, shell.Replace.SelectedReplaceMode);
                Assert.True(shell.Replace.CanBuildReplace, shell.Replace.ReplaceReadinessStatus);
                Assert.True(shell.Replace.HasMemoryLayoutDisplayError);
                Assert.Contains(ExperienceIds.GeneralReplace, shell.Replace.ReplaceModeChoices);
                shell.Replace.SelectedReplaceMode = ExperienceIds.GeneralReplace;
                Assert.Equal(ExperienceIds.GeneralReplace, shell.Replace.SelectedReplaceMode);
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
        Assert.Empty(viewModel.ReplaceCoverageGroups);
        Assert.Empty(viewModel.CtrlRamFocusLanes);
        Assert.Empty(viewModel.CtrlRamOverview);
        Assert.NotEmpty(viewModel.CtrlRamRegions);
        Assert.Equal(3, viewModel.ReplaceSlots.Count(slot => slot.SlotKind == FirmwareSlotKind.CtrlRam && slot.HasFile));
    }
}
