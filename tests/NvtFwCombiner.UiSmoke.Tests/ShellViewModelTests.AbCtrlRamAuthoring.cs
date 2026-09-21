using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class CtrlRamWorkflowTests
{
    /// <summary>Real UI inspection and output confirmation preserve the selected banks and optional versions.</summary>
    [Theory]
    [InlineData(AbCtrlRamBankSelection.A, "a-bank")]
    [InlineData(AbCtrlRamBankSelection.B, "b-bank")]
    [InlineData(AbCtrlRamBankSelection.Both, "a-bank")]
    [InlineData(AbCtrlRamBankSelection.Both, "b-bank")]
    [InlineData(AbCtrlRamBankSelection.Both, null)]
    public async Task AbCtrlRamOutputConfirmationBuildsSelectedBanks(AbCtrlRamBankSelection selection, string? editedBank)
    {
        using TempWorkspace workspace = TempWorkspace.Create("ui-ab-ctrlram-build");
        MainWindowViewModel viewModel = await CreateAbCtrlRamReadyAsync(selection);
        byte[] reference = File.ReadAllBytes(AbCtrlRamReferencePath);
        byte[] nf = File.ReadAllBytes(AbCtrlRamNfPath);
        Assert.False(viewModel.Replace.HasMemoryLayoutDisplayError);
        Assert.NotEmpty(viewModel.Replace.ReplaceCoverageSegments);
        Assert.Equal(0x80000, viewModel.Replace.ReplaceCoverageSegments.Max(static segment => segment.RangeEndExclusive));
        Assert.Equal(0, viewModel.Replace.ReplaceCoverageSegments.Min(static segment => segment.RangeStart));
        Assert.Equal(0x80000, viewModel.Replace.ReplaceCoverageSegments.Sum(static segment => segment.RangeEndExclusive - segment.RangeStart));
        foreach (int bankStart in new[] { 0, 0x40000 })
        {
            bool selected = selection == AbCtrlRamBankSelection.Both ||
                (bankStart == 0 ? selection == AbCtrlRamBankSelection.A : selection == AbCtrlRamBankSelection.B);
            MemoryCoverageSegmentViewModel[] bank = [.. viewModel.Replace.ReplaceCoverageSegments.Where(segment =>
                segment.RangeStart >= bankStart && segment.RangeEndExclusive <= bankStart + 0x40000)];
            Assert.NotEmpty(bank);
            if (!selected)
            {
                Assert.All(bank, static segment => Assert.Equal(MemoryWorkflowDisposition.Kept, segment.Disposition));
            }
            else
            {
                MemoryCoverageSegmentViewModel nfCoverage = Assert.Single(bank, segment =>
                    segment.RangeStart <= bankStart + 0x1FC00 && segment.RangeEndExclusive > bankStart + 0x1FC00);
                Assert.Equal("replace-ctrlram-nf", nfCoverage.SourceSlotId);
                Assert.Equal("NF CtrlRAM", nfCoverage.SourceLabel);
                Assert.True(Assert.Single(bank, segment =>
                    segment.RangeStart <= bankStart + 0x2E000 && segment.RangeEndExclusive > bankStart + 0x2E000).IsSelectedForWrite);
            }
        }
        Assert.Equal(CapabilityEvidenceStatus.ContractOnly, viewModel.Replace.SelectedReplaceWorkflowReadiness!.EvidenceStatus);
        Assert.False(viewModel.Replace.IsSelectedReplaceModeGoldenVerified);
        Assert.True(viewModel.Replace.IsSelectedReplaceModeEvidenceGated);
        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.Equal(selection, Assert.IsType<AbCtrlRamDraftState>(viewModel.Replace.CurrentCtrlRamDraft).Banks);
        await viewModel.Replace.BuildReplaceCommand.ExecuteAsync(null);
        Assert.True(viewModel.OutputDelivery.IsOpen);
        Assert.Equal(selection == AbCtrlRamBankSelection.Both ? 2 : 1, viewModel.Replace.AbCtrlRamVersionEditors.Count);
        if (editedBank is not null)
        {
            CtrlRamFirmwareVersionEditorViewModel editor = viewModel.Replace.AbCtrlRamVersionEditors.Single(item => item.BankId == editedBank);
            Assert.True(editor.CanEdit);
            editor.EditCommand.Execute(null);
            editor.VersionText = "2A";
            editor.SubVersionText = "0C";
        }
        string output = workspace.PathFor("ab-output.bin");
        await viewModel.OutputDelivery.ConfirmLooseAsync(output, null, false, false);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(File.Exists(output));
        byte[] actual = File.ReadAllBytes(output);
        Assert.Equal(reference.Length, actual.Length);
        foreach (int start in new[] { 0, 0x40000 })
        {
            bool selected = selection == AbCtrlRamBankSelection.Both ||
                (start == 0 ? selection == AbCtrlRamBankSelection.A : selection == AbCtrlRamBankSelection.B);
            if (!selected)
            {
                Assert.Equal(reference.AsSpan(start, 0x40000).ToArray(), actual.AsSpan(start, 0x40000).ToArray());
            }
            else
            {
                Assert.Equal(nf, actual.AsSpan(start + 0x1FC00, nf.Length).ToArray());
            }
            Assert.Equal(reference.AsSpan(start + 0x7164, 12).ToArray(), actual.AsSpan(start + 0x7164, 12).ToArray());
            Assert.True(FirmwareConfigMetadataReader.TryReadBackup(reference.AsSpan(start, 0x40000), out FirmwareConfigMetadata before));
            Assert.True(FirmwareConfigMetadataReader.TryReadBackup(actual.AsSpan(start, 0x40000), out FirmwareConfigMetadata after));
            bool edited = editedBank == (start == 0 ? "a-bank" : "b-bank");
            Assert.Equal(edited ? 0x2A : before.FirmwareVersion, after.FirmwareVersion);
            Assert.Equal(edited ? 0x0C : before.FirmwareSubVersion, after.FirmwareSubVersion);
        }
        Assert.False(viewModel.OutputDelivery.IsOpen);
    }

    /// <summary>Bank editors, cancelled changes and inspected modal leases belong to one page instance.</summary>
    [Fact]
    public async Task AbCtrlRamEditorsAreIndependentAndCancelledOrStaleEditsDoNotBuild()
    {
        MainWindowViewModel viewModel = await CreateAbCtrlRamReadyAsync(AbCtrlRamBankSelection.Both);
        MainWindowViewModel other = PresentationTestHost.CreateViewModel();
        Assert.True(other.Replace.IsStandardCtrlRamReference);
        Assert.True(await viewModel.Replace.RequestCtrlRamBuildSettingsAsync());
        CtrlRamFirmwareVersionEditorViewModel a = viewModel.Replace.AbCtrlRamVersionEditors[0];
        CtrlRamFirmwareVersionEditorViewModel b = viewModel.Replace.AbCtrlRamVersionEditors[1];
        b.EditCommand.Execute(null);
        b.VersionText = "GG";
        b.SubVersionText = "12";
        Assert.False((await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(TestContext.Current.CancellationToken)).Succeeded);
        Assert.True(b.HasValidation);
        Assert.False(a.HasValidation);
        b.VersionText = "34";
        (bool succeeded, CtrlRamAuthoringDraftState? edit) = await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(TestContext.Current.CancellationToken);
        Assert.True(succeeded);
        AbCtrlRamDraftState draft = Assert.IsType<AbCtrlRamDraftState>(edit);
        Assert.Null(draft.AVersion);
        Assert.Equal(new CtrlRamFirmwareVersionDraftState(0x34, 0x12), draft.BVersion);
        viewModel.OutputDelivery.CancelCommand.Execute(null);
        Assert.Null(Assert.IsType<AbCtrlRamDraftState>(viewModel.Replace.CurrentCtrlRamDraft).BVersion);
        Assert.True(await viewModel.Replace.RequestCtrlRamBuildSettingsAsync());
        Assert.All(viewModel.Replace.AbCtrlRamVersionEditors, static item => Assert.True(item.IsPreserveSelected));
        Task change = viewModel.Replace.SelectCtrlRamBanksCommand.ExecuteAsync(AbCtrlRamBankSelection.A);
        Assert.False(viewModel.Replace.CanBuildReplace);
        Assert.False(await viewModel.Replace.IsCtrlRamFirmwareVersionBuildConfirmationCurrentAsync(TestContext.Current.CancellationToken));
        Assert.False((await viewModel.Replace.TryCreateCtrlRamFirmwareVersionEditAsync(TestContext.Current.CancellationToken)).Succeeded);
        await change;
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
        Assert.True(await viewModel.Replace.RequestCtrlRamBuildSettingsAsync());
        Assert.Equal("a-bank", Assert.Single(viewModel.Replace.AbCtrlRamVersionEditors).BankId);
        Assert.True(other.Replace.IsStandardCtrlRamReference);
    }

    /// <summary>AB remains explicit and unavailable axes never inherit a Standard accepted build.</summary>
    [Theory]
    [InlineData("NT51950", "single")]
    [InlineData("NT51932", "single")]
    public async Task AbCtrlRamUnsupportedAxesStayClosed(string icId, string number)
    {
        MainWindowViewModel viewModel = await CreateAbCtrlRamReadyAsync(AbCtrlRamBankSelection.Both);
        viewModel.WorkflowSession.SelectedIc = icId;
        viewModel.WorkflowSession.SelectedNumber = number;
        Assert.Equal(number, viewModel.WorkflowSession.SelectedNumber);
        Assert.True(viewModel.Replace.IsAbCtrlRamReference);
        Assert.False(viewModel.Replace.AbCtrlRamReadiness.IsAvailable);
        Assert.False(viewModel.Replace.SelectAbCtrlRamReferenceCommand.CanExecute(null));
        Assert.False(viewModel.Replace.CanBuildReplace);
        Assert.False(await viewModel.Replace.TryOpenCtrlRamFirmwareVersionModalAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>An earlier A result cannot publish after A to B to A, even when values match again.</summary>
    [Fact]
    public async Task AbCtrlRamRapidBankAbaPublishesOnlyCurrentInspection()
    {
        PresentationHostServices services = PresentationTestHost.CreateServices("ui-smoke");
        var authoring = new RecordingCtrlRamAuthoring(services.Composition.CtrlRamAuthoring);
        services = WithCtrlRamAuthoring(services, authoring);
        var inspection = new DelayedPathFirmwareInspection(services.Composition.FirmwareInspection, AbCtrlRamNfPath);
        var viewModel = new MainWindowViewModel("ui-smoke", "ui-smoke", ShellLanguage.English, services, inspection);
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
        ConfigureAbCtrlRamPage(viewModel);
        await viewModel.Replace.SelectCtrlRamDraftAsync(new AbCtrlRamDraftState(AbCtrlRamBankSelection.A));
        await viewModel.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.ReplaceBase, AbCtrlRamReferencePath, TestContext.Current.CancellationToken);
        Task first = viewModel.WorkflowSession.SetSlotFileAsync("replace-ctrlram-nf", AbCtrlRamNfPath, TestContext.Current.CancellationToken);
        await inspection.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Task second = viewModel.Replace.SelectCtrlRamBanksCommand.ExecuteAsync(AbCtrlRamBankSelection.B);
        Task third = viewModel.Replace.SelectCtrlRamBanksCommand.ExecuteAsync(AbCtrlRamBankSelection.A);
        Assert.False(viewModel.Replace.CanBuildReplace);
        inspection.Release();
        await Task.WhenAll(first, second, third).WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
        Assert.Equal(1, authoring.AdoptInspectedBatchCalls);
        Assert.Equal(AbCtrlRamBankSelection.A, Assert.IsType<AbCtrlRamDraftState>(authoring.SingleSuccessfulAdoption.Snapshot.DraftState).Banks);
    }
}

public abstract partial class ShellViewModelTestBase
{
    private protected static string AbCtrlRamReferencePath => CanonicalGoldenTestData.ArtifactPath(
        "ab-merge", "NT51929", "expected-output", "t05-d06");

    private protected static string AbCtrlRamNfPath
    {
        get
        {
            JsonElement fixture = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", "nt51929-fw200-single-auto-prj-594-20260717");
            return CanonicalGoldenTestData.ArtifactPath(CanonicalGoldenTestData.Artifact(fixture, "postbuild-nf-ctrlram"));
        }
    }

    private protected static void ConfigureAbCtrlRamPage(MainWindowViewModel viewModel)
    {
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.WorkflowSession.SelectedNumber = "single";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
    }

    private protected static async Task<MainWindowViewModel> CreateAbCtrlRamReadyAsync(AbCtrlRamBankSelection selection)
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        ConfigureAbCtrlRamPage(viewModel);
        await viewModel.Replace.SelectAbCtrlRamReferenceCommand.ExecuteAsync(null);
        Assert.True(viewModel.Replace.IsCtrlRamBothBanksSelected);
        await viewModel.Replace.SelectCtrlRamBanksCommand.ExecuteAsync(selection);
        await viewModel.WorkflowSession.SetSlotFileAsync(CompositionSlotIds.ReplaceBase, AbCtrlRamReferencePath, TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync("replace-ctrlram-nf", AbCtrlRamNfPath, TestContext.Current.CancellationToken);
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);
        Assert.Equal(WorkflowInspectionAttemptState.Succeeded, viewModel.Replace.Inspection.State);
        return viewModel;
    }
}
