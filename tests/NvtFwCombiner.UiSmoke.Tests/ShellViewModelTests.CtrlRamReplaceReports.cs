using System.Text.Json;
using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class CtrlRamExternalGoldenTests
{
    /// <summary>Verifies CtrlRAM Replace exposes physical input slots and reports generated postbuild commands.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewReportsPostbuildCommandTrace()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            "nt51927-2chip-self-20260705");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "2";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.CtrlRamReplace);

        FirmwareSlotViewModel regionSlot = viewModel.Replace.ReplaceSlots.Single(slot =>
            slot.Title == "Normal CtrlRAM (Master)");
        Assert.True(regionSlot.IsOptional);
        Assert.Contains("CtrlRAM", regionSlot.Title, StringComparison.Ordinal);

        CanonicalCtrlRamTestData.SetBaseSlot(viewModel, fixtureCase);
        await CurrentInspection(viewModel).ActiveTask;
        Assert.Equal(
            WorkflowInspectionAttemptState.Succeeded,
            viewModel.Replace.Inspection.State);
        viewModel.SetSlotFile(regionSlot.SlotId, CanonicalCtrlRamTestData.ReplacementPathFor(fixtureCase, regionSlot.SlotId));
        AssertInspectionTerminal(viewModel.Replace.Inspection);

        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));

        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(viewModel.Reports.HasLoadedReport);
        Assert.True(viewModel.Reports.LoadedReport.HasStepOperations);
        ReportLineViewModel postbuild = Assert.Single(GetCommandOperations(viewModel.Reports.LoadedReport));
        Assert.Equal(10, postbuild.RuntimeCommands.Count);
        Assert.Contains(postbuild.Facts, fact =>
            fact.Label == "Processor" &&
            fact.Value.Contains("nfc.nt51927.ctrlram-postbuild-v1", StringComparison.Ordinal));
        Assert.All(postbuild.RuntimeCommands, command =>
            Assert.Contains("Combiner.exe", command.ArgumentListEvidence, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(viewModel.Replace.ReplaceCoverageSegments, segment => segment.IsChanged);
        Assert.Contains(viewModel.Replace.ReplaceCoverageSegments, segment =>
            segment.ChangeLabel == "Will replace");
    }

    /// <summary>Verifies one CtrlRAM Replace run can select and report multiple region replacements.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewReportsMultipleSelectedRegions()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            "nt51927-3chip-self-20260705");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "3";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.CtrlRamReplace);

        CanonicalCtrlRamTestData.SetBaseSlot(viewModel, fixtureCase);

        // The verified FWConfig may choose the base image's branch. This fixture deliberately
        // exercises the owner-selected three-chip branch afterwards.
        viewModel.WorkflowSession.SelectedNumber = "3";
        await CurrentInspection(viewModel).ActiveTask;
        FirmwareSlotViewModel normalRight = viewModel.Replace.ReplaceSlots.Single(slot => slot.Title == "Normal CtrlRAM (Slave R)");
        FirmwareSlotViewModel vn = viewModel.Replace.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Shared)");
        Assert.Equal("VN_Ctrlram.bin · 3 regions", vn.Description);
        Assert.Equal(3, vn.CtrlRamDescriptionFacts!.TargetRegionCount);
        Assert.Equal(3, vn.CtrlRamDescriptionFacts.Sections.Count);
        viewModel.SetSlotFile(normalRight.SlotId, CanonicalCtrlRamTestData.ReplacementPathFor(fixtureCase, normalRight.SlotId));
        viewModel.SetSlotFile(vn.SlotId, CanonicalCtrlRamTestData.ReplacementPathFor(fixtureCase, vn.SlotId));

        Assert.Equal("2 / 8 targets selected", viewModel.Replace.ReplaceSelectionCountLabel);
        Assert.Contains(viewModel.Replace.ReplaceSelectionRows, row => row.Title == "Normal CtrlRAM (Slave R)");
        Assert.Contains(viewModel.Replace.ReplaceSelectionRows, row => row.Title == "VN CtrlRAM (Shared)");
        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));

        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        ReportLineViewModel postbuild = Assert.Single(GetCommandOperations(viewModel.Reports.LoadedReport));
        Assert.Equal(13, postbuild.RuntimeCommands.Count);
        Assert.Contains(postbuild.RuntimeCommands, command =>
            command.ArgumentListEvidence.Contains("Normal_Ctrlram_R.bin", StringComparison.Ordinal));
        Assert.Contains(postbuild.RuntimeCommands, command =>
            command.ArgumentListEvidence.Contains("Normal_Ctrlram_L.bin", StringComparison.Ordinal));
        Assert.Contains(viewModel.Replace.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "Normal CtrlRAM (Slave R)" &&
            segment.RangeLabel == "0x207D0-0x237CF (len 0x3000)");
        Assert.Contains(viewModel.Replace.ReplaceCoverageSegments, segment =>
            segment.SourceLabel == "VN CtrlRAM (Slave L)" &&
            segment.RangeLabel == "0x2EBD0-0x3022F (len 0x1660)");
    }

    /// <summary>Verifies CtrlRAM Replace can preview a golden-backed VN self replacement with traceable region naming.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewAcceptsGoldenBackedVnSelfReplacement()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            "nt51927-3chip-self-20260705");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "3";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.CtrlRamReplace);

        CanonicalCtrlRamTestData.SetBaseSlot(viewModel, fixtureCase);
        viewModel.WorkflowSession.SelectedNumber = "3";
        await CurrentInspection(viewModel).ActiveTask;
        FirmwareSlotViewModel vn = viewModel.Replace.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Shared)");
        Assert.Equal("VN_Ctrlram.bin · 3 regions", vn.Description);
        Assert.Equal(3, vn.CtrlRamDescriptionFacts!.TargetRegionCount);
        Assert.Equal(3, vn.CtrlRamDescriptionFacts.Sections.Count);
        Assert.Contains(vn.CtrlRamDescriptionFacts.Sections, section =>
            section.DisplayName == "VN CtrlRAM (Master)" && section.MaximumLength == 5728);
        Assert.Contains(vn.CtrlRamDescriptionFacts.Sections, section =>
            section.DisplayName == "VN CtrlRAM (Slave L)" && section.MaximumLength == 5728);
        viewModel.SetSlotFile(vn.SlotId, CanonicalCtrlRamTestData.ReplacementPathFor(fixtureCase, vn.SlotId));

        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));

        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(viewModel.Reports.HasLoadedReport);
        ReportLineViewModel postbuild = Assert.Single(GetCommandOperations(viewModel.Reports.LoadedReport));
        Assert.Equal(13, postbuild.RuntimeCommands.Count);
        Assert.Contains(postbuild.RuntimeCommands, command =>
            command.ArgumentListEvidence.Contains("VN_Ctrlram.bin", StringComparison.Ordinal));
        MemoryCoverageGroupViewModel common = Assert.Single(
            viewModel.Replace.ReplaceCoverageGroups,
            group => group.RegionGroup == ReplaceRegionGroup.Common);
        MemoryCoverageLogicalItemViewModel logicalVn = Assert.Single(
            common.Items,
            item => item.DisplayId == $"slot:{vn.SlotId}");
        Assert.Equal("VN CtrlRAM", logicalVn.SourceLabel);
        Assert.Contains(logicalVn.Ranges, range =>
            range.RegionGroupLabel == "Slave L" &&
            range.RangeLabel == "0x2EBD0-0x3022F (len 0x1660)");
    }

    /// <summary>Verifies an exact V2 CtrlRAM replacement runs through the real postbuild path.</summary>
    [Fact]
    public async Task CtrlRamReplacePreviewSelfReplacementRunsPostbuild()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            "nt51927-3chip-self-20260705");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "3";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.CtrlRamReplace);

        CanonicalCtrlRamTestData.SetBaseSlot(viewModel, fixtureCase);
        viewModel.WorkflowSession.SelectedNumber = "3";
        await CurrentInspection(viewModel).ActiveTask;
        FirmwareSlotViewModel vnSlot = viewModel.Replace.ReplaceSlots.Single(slot => slot.Title == "VN CtrlRAM (Shared)");
        viewModel.SetSlotFile(vnSlot.SlotId, CanonicalCtrlRamTestData.ReplacementPathFor(fixtureCase, vnSlot.SlotId));

        Assert.True(viewModel.Replace.PreviewReplaceCommand.CanExecute(null));

        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(viewModel.Reports.HasLoadedReport);
        ReportLineViewModel postbuild = Assert.Single(GetCommandOperations(viewModel.Reports.LoadedReport));
        Assert.Equal(13, postbuild.RuntimeCommands.Count);
        Assert.Contains(postbuild.RuntimeCommands, command =>
            command.ArgumentListEvidence.Contains("VN_Ctrlram.bin", StringComparison.Ordinal));
        using var reportDocument = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
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
                Assert.Equal("tp-flash-header", semantic.GetProperty("CategoryId").GetString());
                Assert.StartsWith(
                    "nt51927-header:",
                    semantic.GetProperty("SubjectId").GetString(),
                    StringComparison.Ordinal);
                Assert.Contains(
                    "CRC",
                    semantic.GetProperty("SubjectLabel").GetString(),
                    StringComparison.Ordinal);
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
