using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Exercises firmware Drop actions through the real Avalonia event boundary.</summary>
public sealed class FirmwareDropProcessSmokeTests
{
    /// <summary>Zero or multiple dropped files preserve the accepted immutable slot session.</summary>
    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task FirmwareDropRequiresExactlyOneLocalFileWithoutReplacingSelection(
        int droppedFileCount)
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-drop-cardinality");
        JsonElement goldenCase = golden.CaseByIc("51926");
        MainWindowViewModel viewModel = await Task.Run(() =>
        {
            MainWindowViewModel prepared = PresentationTestHost.CreateViewModel();
            prepared.WorkflowSession.SelectedIc = "NT51926";
            prepared.ShowMergeCommand.Execute(null);
            prepared.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
            golden.CopyInputFilesToMergeSlots(prepared, workspace, goldenCase);
            return prepared;
        }, TestContext.Current.CancellationToken);
        FirmwareSlotViewModel slot = viewModel.Merge.MergeDpSlot;
        string acceptedPath = Assert.IsType<string>(slot.FilePath);
        FirmwareInspectionSnapshot acceptedProjection =
            Assert.IsType<FirmwareInspectionSnapshot>(slot.CurrentInspectionProjection);
        AuthoringRevision acceptedRevision = viewModel.Merge.StandardMergeAuthoringRevision;
        Task acceptedTask = viewModel.Merge.Inspection.ActiveTask;
        FirmwareSlotFactViewModel[] acceptedFacts = [.. slot.FirmwareFacts];
        var card = new FirmwareSlotCard { BrowseLabel = "Browse", DataContext = slot };
        var window = new Window { DataContext = viewModel, Content = card };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Border dropZone = Assert.Single(
                card.GetVisualDescendants().OfType<Border>(),
                candidate => Equals(candidate.Tag, slot.SlotId));
            string[] rejectedPaths = [.. Enumerable.Range(0, droppedFileCount)
                .Select(index => workspace.Write($"rejected-{index}.bin", [0xA5]))
            ];
            using var transfer = new DataTransfer();
            DragEventArgs drop = await CreateDropEventAsync(
                window.StorageProvider,
                dropZone,
                rejectedPaths,
                transfer);

            dropZone.RaiseEvent(drop);
            await Dispatcher.UIThread.InvokeAsync(static () => { });

            Assert.Equal(acceptedPath, slot.FilePath);
            Assert.Same(acceptedProjection, slot.CurrentInspectionProjection);
            Assert.Equal(acceptedRevision, viewModel.Merge.StandardMergeAuthoringRevision);
            Assert.Same(acceptedTask, viewModel.Merge.Inspection.ActiveTask);
            Assert.Equal(acceptedFacts, slot.FirmwareFacts);
            Assert.True(viewModel.Merge.PreviewMergeCommand.CanExecute(null));
            Assert.True(viewModel.Reports.HasReportToast);
            Assert.Equal("File not selected", viewModel.Reports.ShellToastTitle);
            Assert.Equal("Drop exactly one local file.", viewModel.Reports.ReportToastText);
            ShellTextResources chinese = ShellTextResources.For(ShellLanguage.ChineseTraditional);
            Assert.Equal("未選取檔案", chinese.FileDropRejectedTitle);
            Assert.Equal("請一次拖放一個本機檔案。", chinese.FileDropRejectedDetail);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Exactly one local file continues through the existing slot-selection path.</summary>
    [AvaloniaFact]
    public async Task FirmwareDropAcceptsOneLocalFile()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-drop-single");
        JsonElement goldenCase = golden.CaseByIc("51926");
        string sourcePath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input"));
        string droppedPath = workspace.PathFor("dp-input.bin");
        File.Copy(sourcePath, droppedPath);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
        FirmwareSlotViewModel slot = viewModel.Merge.MergeDpSlot;
        var card = new FirmwareSlotCard { BrowseLabel = "Browse", DataContext = slot };
        var window = new Window { DataContext = viewModel, Content = card };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Border dropZone = Assert.Single(
                card.GetVisualDescendants().OfType<Border>(),
                candidate => Equals(candidate.Tag, slot.SlotId));
            using var transfer = new DataTransfer();
            DragEventArgs drop = await CreateDropEventAsync(
                window.StorageProvider,
                dropZone,
                [droppedPath],
                transfer);

            dropZone.RaiseEvent(drop);
            await viewModel.Merge.Inspection.ActiveTask.WaitAsync(
                TimeSpan.FromSeconds(15),
                TestContext.Current.CancellationToken);

            Assert.Equal(droppedPath, slot.FilePath);
            Assert.False(viewModel.Reports.HasReportToast);
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task<DragEventArgs> CreateDropEventAsync(
        IStorageProvider storageProvider,
        Border source,
        IReadOnlyList<string> paths,
        DataTransfer transfer)
    {
        foreach (string path in paths)
        {
            IStorageFile file = Assert.IsType<IStorageFile>(
                await storageProvider.TryGetFileFromPathAsync(new Uri(path)),
                exactMatch: false);
            var item = new DataTransferItem();
            item.SetFile(file);
            transfer.Add(item);
        }

        return new DragEventArgs(
            DragDrop.DropEvent,
            transfer,
            source,
            default,
            KeyModifiers.None);
    }
}
