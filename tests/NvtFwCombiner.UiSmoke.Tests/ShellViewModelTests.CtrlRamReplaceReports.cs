using System.Text.Json;
using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies CtrlRAM Replace exposes physical input slots and reports generated postbuild commands.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewReportsPostbuildCommandTrace()
    {
        using var fixtures = CtrlRamReplaceFixtureManifest.LoadIfPresent();
        Assert.NotNull(fixtures);
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            "nt51927-2chip-self-20260705");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "2";
        OpenReplace(viewModel, "CtrlRAM");

        FirmwareSlotViewModel regionSlot = viewModel.ReplaceSlots.Single(slot =>
            slot.Title == "Normal CtrlRAM (Master)");
        Assert.True(regionSlot.IsOptional);
        Assert.Contains("CtrlRAM", regionSlot.Title, StringComparison.Ordinal);

        fixtures.SetBaseSlot(viewModel, fixtureCase);
        viewModel.SetSlotFile(regionSlot.SlotId, fixtures.ReplacementPathFor(fixtureCase, regionSlot.SlotId));

        Assert.True(viewModel.PreviewReplaceCommand.CanExecute(null));

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.LoadedReport.HasStepOperations);
        ReportLineViewModel postbuild = Assert.Single(GetCommandOperations(viewModel.LoadedReport));
        Assert.Equal(10, postbuild.RuntimeCommands.Count);
        Assert.Contains(postbuild.Facts, fact =>
            fact.Label == "Processor" &&
            fact.Value.Contains("nfc.nt51927.ctrlram-postbuild-v1", StringComparison.Ordinal));
        Assert.All(postbuild.RuntimeCommands, command =>
            Assert.Contains("Combiner.exe", command.ArgumentListEvidence, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment => segment.IsChanged);
    }

    /// <summary>Verifies one CtrlRAM Replace run can select and report multiple region replacements.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewReportsMultipleSelectedRegions()
    {
        using var fixtures = CtrlRamReplaceFixtureManifest.LoadIfPresent();
        Assert.NotNull(fixtures);
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            "nt51927-3chip-self-20260705");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        OpenReplace(viewModel, "CtrlRAM");

        fixtures.SetBaseSlot(viewModel, fixtureCase);

        // The verified FWConfig may choose the base image's branch. This fixture deliberately
        // exercises the owner-selected three-chip branch afterwards.
        viewModel.SelectedNumber = "3";
        FirmwareSlotViewModel normalRight = viewModel.ReplaceSlots.Single(slot => slot.Title == "Normal CtrlRAM (Slave R)");
        FirmwareSlotViewModel vn = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Shared)");
        viewModel.SetSlotFile(normalRight.SlotId, fixtures.ReplacementPathFor(fixtureCase, normalRight.SlotId));
        viewModel.SetSlotFile(vn.SlotId, fixtures.ReplacementPathFor(fixtureCase, vn.SlotId));

        Assert.Equal("2 / 8 targets selected", viewModel.ReplaceSelectionCountLabel);
        Assert.Contains(viewModel.ReplaceSelectionRows, row => row.Title == "Normal CtrlRAM (Slave R)");
        Assert.Contains(viewModel.ReplaceSelectionRows, row => row.Title == "VN CtrlRAM (Shared)");
        Assert.True(viewModel.PreviewReplaceCommand.CanExecute(null));

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        ReportLineViewModel postbuild = Assert.Single(GetCommandOperations(viewModel.LoadedReport));
        Assert.Equal(13, postbuild.RuntimeCommands.Count);
        Assert.Contains(postbuild.RuntimeCommands, command =>
            command.ArgumentListEvidence.Contains("Normal_Ctrlram_R.bin", StringComparison.Ordinal));
        Assert.Contains(postbuild.RuntimeCommands, command =>
            command.ArgumentListEvidence.Contains("Normal_Ctrlram_L.bin", StringComparison.Ordinal));
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "Normal CtrlRAM (Slave R)" &&
            segment.RangeLabel == "0x207D0-0x237CF (len 0x3000)");
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "VN CtrlRAM (Slave L)" &&
            segment.RangeLabel == "0x2EBD0-0x3022F (len 0x1660)");
    }

    /// <summary>Verifies CtrlRAM Replace can preview a golden-backed VN self replacement with traceable region naming.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewAcceptsGoldenBackedVnSelfReplacement()
    {
        using var fixtures = CtrlRamReplaceFixtureManifest.LoadIfPresent();
        Assert.NotNull(fixtures);
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            "nt51927-3chip-self-20260705");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        OpenReplace(viewModel, "CtrlRAM");

        fixtures.SetBaseSlot(viewModel, fixtureCase);
        viewModel.SelectedNumber = "3";
        FirmwareSlotViewModel vn = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Shared)");
        Assert.Contains("VN_Ctrlram.bin", vn.Description, StringComparison.Ordinal);
        Assert.Contains("VN CtrlRAM (Master): max 5728 bytes", vn.Description, StringComparison.Ordinal);
        Assert.Contains("VN CtrlRAM (Slave L): max 5728 bytes", vn.Description, StringComparison.Ordinal);
        viewModel.SetSlotFile(vn.SlotId, fixtures.ReplacementPathFor(fixtureCase, vn.SlotId));

        Assert.True(viewModel.PreviewReplaceCommand.CanExecute(null));

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.HasLoadedReport);
        ReportLineViewModel postbuild = Assert.Single(GetCommandOperations(viewModel.LoadedReport));
        Assert.Equal(13, postbuild.RuntimeCommands.Count);
        Assert.Contains(postbuild.RuntimeCommands, command =>
            command.ArgumentListEvidence.Contains("VN_Ctrlram.bin", StringComparison.Ordinal));
        Assert.Contains(viewModel.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "VN CtrlRAM (Slave L)" &&
            segment.RangeLabel == "0x2EBD0-0x3022F (len 0x1660)");
        Assert.Contains(viewModel.ReplaceCoverageGroups, group => group.Title == "Slave L");
    }

    /// <summary>Verifies an exact V2 CtrlRAM replacement runs through the real postbuild path.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewSelfReplacementRunsPostbuild()
    {
        using var fixtures = CtrlRamReplaceFixtureManifest.LoadIfPresent();
        Assert.NotNull(fixtures);
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            "nt51927-3chip-self-20260705");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";
        OpenReplace(viewModel, "CtrlRAM");

        fixtures.SetBaseSlot(viewModel, fixtureCase);
        viewModel.SelectedNumber = "3";
        FirmwareSlotViewModel vnSlot = viewModel.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Shared)");
        viewModel.SetSlotFile(vnSlot.SlotId, fixtures.ReplacementPathFor(fixtureCase, vnSlot.SlotId));

        Assert.True(viewModel.PreviewReplaceCommand.CanExecute(null));

        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.HasLoadedReport);
        ReportLineViewModel postbuild = Assert.Single(GetCommandOperations(viewModel.LoadedReport));
        Assert.Equal(13, postbuild.RuntimeCommands.Count);
        Assert.Contains(postbuild.RuntimeCommands, command =>
            command.ArgumentListEvidence.Contains("VN_Ctrlram.bin", StringComparison.Ordinal));
        using var reportDocument = JsonDocument.Parse(viewModel.LoadedReportJson);
        AssertNoUnexpectedOutputDifferenceIssue(reportDocument.RootElement);
        JsonElement[] differences = [.. reportDocument.RootElement.GetProperty("OutputDifferences").EnumerateArray()];
        Assert.All(differences, difference =>
        {
            Assert.True(difference.GetProperty("IsAccepted").GetBoolean());
            Assert.True(difference.GetProperty("Classification").GetString() is
                OutputDifferenceClassifications.DeclaredReplacement or
                OutputDifferenceClassifications.PostbuildCrcHeader);
            string evidence = difference.GetProperty("Evidence").GetString()!;
            if (difference.GetProperty("Classification").GetString() == OutputDifferenceClassifications.DeclaredReplacement)
            {
                Assert.StartsWith("replace-vn-", evidence, StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains("postbuild-threechip", evidence, StringComparison.Ordinal);
            }
        });
        Assert.Contains(differences, difference =>
            difference.GetProperty("Classification").GetString() == OutputDifferenceClassifications.DeclaredReplacement);
        Assert.Contains(differences, difference =>
            difference.GetProperty("Classification").GetString() == OutputDifferenceClassifications.PostbuildCrcHeader);
        Assert.All(differences, difference => Assert.True(difference.TryGetProperty("Semantic", out _)));
        Assert.All(
            differences.Where(difference =>
                difference.GetProperty("Classification").GetString() == OutputDifferenceClassifications.PostbuildCrcHeader),
            difference =>
            {
                JsonElement semantic = difference.GetProperty("Semantic");
                Assert.Equal("other-documented-region", semantic.GetProperty("CategoryId").GetString());
                Assert.Equal("postbuild-copy", semantic.GetProperty("SubjectId").GetString());
                Assert.Equal("Header / CRC refresh", semantic.GetProperty("SubjectLabel").GetString());
            });
        Assert.All(
            differences.Where(difference =>
                difference.GetProperty("Classification").GetString() == OutputDifferenceClassifications.DeclaredReplacement),
            difference =>
            {
                JsonElement semantic = difference.GetProperty("Semantic");
                Assert.Equal("replacement-data", semantic.GetProperty("CategoryId").GetString());
                Assert.Equal("VN CtrlRAM", semantic.GetProperty("SubjectLabel").GetString());
            });
    }
}
