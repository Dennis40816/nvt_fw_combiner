using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class GeneralWorkflowTests
{
    /// <summary>General Merge command state and Memory Layout consume the same canonical admission result.</summary>
    [Fact]
    public async Task GeneralMergeCanonicalAdmissionBlocksInvalidAndOverlappingMappings()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-merge-admission");
        string firstPath = workspace.Write("first.bin", [0x10, 0x11, 0x12, 0x13]);
        string secondPath = workspace.Write("second.bin", [0x20, 0x21, 0x22, 0x23]);
        DelayedGeneralAuthoring? delayedAuthoring = null;
        using var uiThread = new UiThreadTestContext();
        try
        {
            await uiThread.InvokeAsync(async () =>
            {
                MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel(inner =>
                    delayedAuthoring = new DelayedGeneralAuthoring(inner));
                viewModel.ShowMergeCommand.Execute(null);
                viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
                viewModel.Merge.GeneralMergeOutputLength = "0x20";

                GeneralMergeMappingViewModel first = Assert.Single(viewModel.Merge.GeneralMergeMappings);
                await viewModel.WorkflowSession.SetSlotFileAsync(
                    first.MappingId,
                    firstPath,
                    TestContext.Current.CancellationToken);
                Assert.False(viewModel.Merge.PreviewMergeCommand.CanExecute(null));

                var publicationThreads = new List<int>();
                first.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(GeneralMappingRowViewModel.AcceptedFileStamp) &&
                        first.AcceptedFileStamp is not null)
                    {
                        publicationThreads.Add(Environment.CurrentManagedThreadId);
                    }
                };
                first.Length = "0x4";
                await delayedAuthoring!.FirstPreparationStarted.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
                first.Length = "0x04";
                delayedAuthoring.ReleaseFirstPreparation();
                await viewModel.Merge.Inspection.ActiveTask.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
                Assert.True(viewModel.Merge.PreviewMergeCommand.CanExecute(null));
                AssertInspectionTerminal(viewModel.Merge.Inspection);
                Assert.NotEmpty(publicationThreads);
                Assert.All(publicationThreads, threadId => Assert.Equal(uiThread.ThreadId, threadId));

                viewModel.Merge.AddGeneralMergeMappingCommand.Execute(null);
                GeneralMergeMappingViewModel second = viewModel.Merge.GeneralMergeMappings[1];
                second.TargetStartAddress = "0x2";
                second.Length = "0x4";
                await viewModel.WorkflowSession.SetSlotFileAsync(
                    second.MappingId,
                    secondPath,
                    TestContext.Current.CancellationToken);
                await viewModel.Merge.Inspection.ActiveTask;

                Assert.False(viewModel.Merge.PreviewMergeCommand.CanExecute(null));
                MemoryCoverageSegmentViewModel overlap = Assert.Single(
                    viewModel.Merge.MergeCoverageSegments,
                    segment => segment.SourceLabel == "Overlap error");
                Assert.Equal("0x00002-0x00003 (len 0x2)", overlap.RangeLabel);
                Assert.Contains(first.MappingId, overlap.Detail, StringComparison.Ordinal);
                Assert.Contains(second.MappingId, overlap.Detail, StringComparison.Ordinal);
            });
        }
        finally
        {
            delayedAuthoring?.ReleaseFirstPreparation();
        }
    }

    private sealed class DelayedGeneralAuthoring(IGeneralAuthoring inner) : IGeneralAuthoring
    {
        private readonly TaskCompletionSource _firstPreparationStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstPreparation =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _preparationCount;

        internal Task FirstPreparationStarted => _firstPreparationStarted.Task;

        public GeneralAuthoringAdmissionResult GetMergeAdmission(
            string icId,
            GeneralMergeDraftState draft)
        {
            return inner.GetMergeAdmission(icId, draft);
        }

        public GeneralAuthoringAdmissionResult? GetReplaceAdmission(
            string icId,
            long referenceCapacity,
            GeneralMappingDraftState mappingDraft)
        {
            return inner.GetReplaceAdmission(icId, referenceCapacity, mappingDraft);
        }

        public string GetDefaultOutputLength(string icId)
        {
            return inner.GetDefaultOutputLength(icId);
        }

        public string GetDefaultOutputFillByte(string icId)
        {
            return inner.GetDefaultOutputFillByte(icId);
        }

        public async ValueTask<GeneralAuthoringSessionPreparation> PrepareMergeSessionAsync(
            AuthoringSessionState session,
            string icId,
            GeneralMergeDraftState draft,
            CancellationToken cancellationToken,
            IProgress<AuthoringInspectionProgress>? progress = null)
        {
            if (Interlocked.Increment(ref _preparationCount) == 1)
            {
                _firstPreparationStarted.SetResult();
                await _releaseFirstPreparation.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return await inner.PrepareMergeSessionAsync(
                session,
                icId,
                draft,
                cancellationToken,
                progress).ConfigureAwait(false);
        }

        public ValueTask<GeneralAuthoringSessionPreparation> PrepareReplaceSessionAsync(
            AuthoringSessionState session,
            string icId,
            string number,
            string referencePath,
            GeneralMappingDraftState draft,
            CancellationToken cancellationToken,
            IProgress<AuthoringInspectionProgress>? progress = null)
        {
            return inner.PrepareReplaceSessionAsync(
                session,
                icId,
                number,
                referencePath,
                draft,
                cancellationToken,
                progress);
        }

        internal void ReleaseFirstPreparation()
        {
            _ = _releaseFirstPreparation.TrySetResult();
        }
    }

    /// <summary>General Replace rejects overlap before Preview and marks only the exact intersection.</summary>
    [Fact]
    public async Task GeneralReplaceCanonicalAdmissionBlocksOverlappingMappings()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace-admission");
        string basePath = workspace.Write("base.bin", new byte[0x40000]);
        string firstPath = workspace.Write("first.bin", [0x10, 0x11, 0x12, 0x13]);
        string secondPath = workspace.Write("second.bin", [0x20, 0x21, 0x22, 0x23]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, ExperienceIds.GeneralReplace);
        viewModel.SetSlotFile("replace-base", basePath);

        GeneralReplaceMappingViewModel first = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        first.TargetStartAddress = "0x100";
        first.Length = "0x4";
        viewModel.SetSlotFile(first.MappingId, firstPath);
        viewModel.Replace.AddGeneralReplaceMappingCommand.Execute(null);
        GeneralReplaceMappingViewModel second = viewModel.Replace.GeneralReplaceMappings[1];
        second.TargetStartAddress = "0x102";
        second.Length = "0x4";
        viewModel.SetSlotFile(second.MappingId, secondPath);
        await viewModel.Replace.Inspection.ActiveTask;

        Assert.False(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
        Assert.True(first.HasAuthoringIssue);
        Assert.True(second.HasAuthoringIssue);
        Assert.Contains(first.MappingId, first.AuthoringIssueMessage, StringComparison.Ordinal);
        Assert.Contains(second.MappingId, first.AuthoringIssueMessage, StringComparison.Ordinal);
        Assert.Contains("[0x102, 0x104)", first.AuthoringIssueMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(
            viewModel.Replace.ReplaceCoverageSegments,
            segment => segment.SourceLabel == "Overlap error");
    }

    /// <summary>General Replace exposes canonical inline source kinds and validates their payload shape.</summary>
    [Theory]
    [InlineData(GeneralMappingSourceKind.HexOverwrite, "A55A", "0x2", true)]
    [InlineData(GeneralMappingSourceKind.HexOverwrite, "A55A", "0x3", false)]
    [InlineData(GeneralMappingSourceKind.HexFill, "FF", "0x4", true)]
    [InlineData(GeneralMappingSourceKind.HexFill, "FFFF", "0x4", false)]
    public async Task GeneralReplaceInlineSourcesUseCanonicalAdmission(
        GeneralMappingSourceKind sourceKind,
        string value,
        string length,
        bool expectedAdmitted)
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace-inline");
        string basePath = workspace.Write("base.bin", new byte[0x40000]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, ExperienceIds.GeneralReplace);
        viewModel.SetSlotFile("replace-base", basePath);

        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.SelectedSource = mapping.SourceOptions.Single(option => option.Kind == sourceKind);
        mapping.TargetStartAddress = "0x100";
        mapping.Length = length;
        mapping.InlineValue = value;
        await viewModel.Replace.Inspection.ActiveTask;

        Assert.Equal(!expectedAdmitted, mapping.HasAuthoringIssue);
        Assert.Equal(WorkflowInspectionAttemptState.Failed, viewModel.Replace.Inspection.State);
        Assert.True(viewModel.Replace.Inspection.Loading.CanRetry);
        Assert.Equal("Waiting for Base BIN", viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Equal(sourceKind == GeneralMappingSourceKind.HexFill ? "FILL" : "HEX", mapping.SourceKindIcon);
    }

    /// <summary>Malformed Start + Length remains visible beside the exact mapping id.</summary>
    [Fact]
    public void GeneralReplaceInvalidRangeKeepsCanonicalAuthoringDiagnostic()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-replace-invalid-range");
        string basePath = workspace.Write("base.bin", new byte[0x40000]);
        string inputPath = workspace.Write("mapping.bin", [0xA5, 0x5A]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, ExperienceIds.GeneralReplace);
        viewModel.SetSlotFile("replace-base", basePath);

        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "invalid";
        mapping.Length = "0x2";
        viewModel.SetSlotFile(mapping.MappingId, inputPath);

        Assert.True(mapping.HasAuthoringIssue);
        Assert.Contains(mapping.MappingId, mapping.AuthoringIssueMessage, StringComparison.Ordinal);
        Assert.Contains("non-negative hexadecimal", mapping.AuthoringIssueMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(
            viewModel.Replace.ReplaceMemoryRows,
            row => row.ActionLabel == "Error");
        Assert.False(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));
    }
}
