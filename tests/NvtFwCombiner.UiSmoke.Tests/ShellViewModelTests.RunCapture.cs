using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class RunAndHexEditorTests
{
    /// <summary>Standard Merge executes the accepted snapshot captured before the dispatcher yields.</summary>
    [Fact]
    public async Task StandardMergePreviewKeepsSnapshotWhenSelectionChangesBeforeWorkerStarts()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nfc-standard-run-capture");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        golden.CopyInputFilesToMergeSlots(viewModel, workspace, golden.CaseByIc("51926"));
        using var uiThread = new UiThreadTestContext();

        await uiThread.InvokeAsync(async () =>
        {
            Assert.True(viewModel.Merge.PreviewMergeCommand.CanExecute(null));

            Task preview = viewModel.Merge.PreviewMergeCommand.ExecuteAsync(null);
            viewModel.WorkflowSession.SelectedIc = "NT51927";
            await preview;
            await viewModel.Merge.Inspection.ActiveTask;

            Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
            Assert.Equal("NT51926", viewModel.Reports.LoadedReport.IcId);
            Assert.Equal("NT51927", viewModel.WorkflowSession.SelectedIc);
            Assert.False(viewModel.RunSession.IsRunInProgress);
        });
    }

    /// <summary>Completion activity retains the request's IC when the user changes the current context.</summary>
    [Fact]
    public async Task CompletionActivityKeepsCapturedIcAfterSelectionChanges()
    {
        using var workspace = TempWorkspace.Create("nfc-activity-run-capture");
        string sourcePath = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]);
        PresentationHostServices services = PresentationTestHost.CreateServices("1.1.10-test");
        MainWindowViewModel viewModel = PresentationTestHost.PublishCanonicalCatalog(
            services, ShellViewModelFactory.Create(services, ShellLanguage.English));
        using var uiThread = new UiThreadTestContext();

        await uiThread.InvokeAsync(async () =>
        {
            viewModel.ShowMergeCommand.Execute(null);
            viewModel.WorkflowSession.SelectedIc = "NT51926";
            viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
            viewModel.Merge.GeneralMergeOutputLength = "0x10";
            GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.Merge.GeneralMergeMappings);
            mapping.SourceStartAddress = "0x0";
            mapping.TargetStartAddress = "0x4";
            mapping.Length = "0x4";
            await viewModel.WorkflowSession.SetSlotFileAsync(
                mapping.MappingId, sourcePath, TestContext.Current.CancellationToken);

            Task preview = viewModel.Merge.PreviewMergeCommand.ExecuteAsync(null);
            viewModel.WorkflowSession.SelectedIc = "NT51927";
            await preview;
            await viewModel.Merge.Inspection.ActiveTask;

            Assert.Equal("NT51926", viewModel.Reports.LoadedReport.IcId);
            SystemActivityEntry completed = Assert.Single(services.SystemInformation.Activity,
                entry => entry.Code == SystemActivityCodes.PreviewCompleted);
            Assert.Equal(ExperienceIds.GeneralMerge, completed.SubjectId);
            Assert.Equal("NT51926", completed.ContextId);
        });
    }
}
