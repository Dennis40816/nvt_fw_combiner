using System.Text.Json;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>The global progress surface names the active Preview or Build in the selected language.</summary>
    [Theory]
    [InlineData(ShellLanguage.English, "Preview in progress", "Build in progress")]
    [InlineData(ShellLanguage.ChineseTraditional, "正在預覽", "正在建立")]
    public async Task CompositionProgressNamesTheActiveAction(
        ShellLanguage language,
        string previewLabel,
        string buildLabel)
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-run-progress");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create(language);
        viewModel.SelectedIc = "NT51926";
        golden.CopyInputFilesToMergeSlots(viewModel, workspace, goldenCase);
        List<string> activeLabels = [];
        bool wasInProgress = false;
        int labelNotifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.RunProgressAccessibleLabel))
            {
                labelNotifications++;
            }

            if (args.PropertyName == nameof(MainWindowViewModel.IsRunInProgress))
            {
                if (!wasInProgress && viewModel.IsRunInProgress)
                {
                    activeLabels.Add(viewModel.RunProgressAccessibleLabel);
                }

                wasInProgress = viewModel.IsRunInProgress;
            }
        };

        await viewModel.PreviewMergeCommand.ExecuteAsync(null);
        await viewModel.BuildStandardMergeAsync(workspace.PathFor("output.bin"));

        Assert.Equal([previewLabel, buildLabel], activeLabels);
        Assert.Equal(2, labelNotifications);
        Assert.False(viewModel.IsRunInProgress);
    }
}
