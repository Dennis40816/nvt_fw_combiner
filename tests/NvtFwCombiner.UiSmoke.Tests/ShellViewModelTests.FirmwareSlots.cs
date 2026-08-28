using System.Text.Json;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>Every authorable Standard Merge IC retains the owner-approved FlashCode naming contract.</summary>
    [Fact]
    public void EveryAuthorableStandardMergeIcUsesCanonicalGoldenOutputName()
    {
        CapabilityProfileSummary[] profiles =
        [
            .. TestProjection.GetStandardMergeProfileSummaries(),
        ];

        Assert.NotEmpty(profiles);
        Assert.All(profiles, profile => Assert.Equal(
            "{ic}_FlashCode_D{dp-version}T{tp-version}_{date}.bin",
            profile.DefaultOutputFileName));
        Assert.Equal(
            profiles.Length,
            profiles.Select(static profile => profile.IcId).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Legacy and CMI readers display hexadecimal DP minor version D identically.</summary>
    [Fact]
    public void DpMinorHexDigitDisplaysConsistentlyAcrossMetadataSources()
    {
        var legacy = new FirmwareInspectionSnapshot(
            null,
            null,
            new DpVersionMetadata("000D"),
            null,
            null,
            null)
        {
            ArtifactClassification = CreateArtifactClassification(CompiledFirmwareArtifactKind.FlashCode),
        };
        var cmi = new FirmwareInspectionSnapshot(
            null,
            null,
            null,
            new CmiDpCodeMetadata(0x00, 0x0D, 0, 0),
            null,
            null)
        {
            ArtifactClassification = CreateArtifactClassification(CompiledFirmwareArtifactKind.FlashCode),
        };

        FirmwareSlotFactViewModel legacyFact = Assert.Single(UiCompositionRunner.GetDpFirmwareSlotFacts(legacy));
        FirmwareSlotFactViewModel cmiFact = Assert.Single(UiCompositionRunner.GetDpFirmwareSlotFacts(cmi));

        Assert.Equal(new FirmwareSlotFactViewModel("DP Version", "D00-0D"), legacyFact);
        Assert.Equal(legacyFact, cmiFact);
    }

    /// <summary>Verifies slot completion retains required and optional semantics for XAML state selectors.</summary>
    [Fact]
    public void FirmwareSlotCompletionToneHighlightsOnlyRequiredInputs()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-slot-tone");
        FirmwareSlotViewModel required = new("merge-dp", "DP BIN", "Display payload", FirmwareSlotKind.Dp);

        Assert.False(required.IsOptional);
        Assert.False(required.HasFile);
        Assert.True(required.IsGuidanceVisible);
        Assert.Equal(FirmwareSlotKind.Dp, required.SlotKind);
        Assert.Equal("DP BIN", required.SlotIconTooltip);
        AssertIconGeometry(required);
        Assert.Equal("No BIN selected", required.DisplayName);
        Assert.Equal(string.Empty, required.DisplayDetail);
        Assert.Equal("Required", required.RequirementLabel);

        required.FilePath = workspace.PathFor("dp.bin").Replace('\\', '/');

        Assert.True(required.HasFile);
        Assert.False(required.IsGuidanceVisible);
        Assert.Equal("dp.bin", required.DisplayName);
        Assert.Equal(required.FilePath.Replace('/', '\\'), required.DisplayDetail);
        AssertIconGeometry(required);
        Assert.Equal("Required", required.RequirementLabel);

        FirmwareSlotViewModel optional = new(
            "merge-ldc",
            "LDC BIN",
            "Optional payload",
            FirmwareSlotKind.Dp,
            isOptional: true);

        Assert.True(optional.IsOptional);
        Assert.Equal(FirmwareSlotKind.Dp, optional.SlotKind);
        AssertIconGeometry(optional);
        Assert.Equal("Optional", optional.RequirementLabel);

        optional.FilePath = workspace.PathFor("ld.bin");

        Assert.True(optional.HasFile);
        Assert.Equal("Optional", optional.RequirementLabel);
    }

    /// <summary>Verifies slot type icons distinguish DP, TP, CtrlRAM and base BIN inputs.</summary>
    [Fact]
    public void FirmwareSlotTypeIconsExposeInputCategories()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);

        Assert.Contains(viewModel.Merge.MergeSlots, slot =>
            slot.Title == "DP BIN" &&
            slot.SlotKind == FirmwareSlotKind.Dp &&
            HasDrawableIcon(slot));
        Assert.Contains(viewModel.Merge.MergeSlots, slot =>
            slot.Title == "TP BIN" &&
            slot.SlotKind == FirmwareSlotKind.Tp &&
            HasDrawableIcon(slot));
        Assert.Equal(FirmwareSlotKind.Base, viewModel.Replace.ReplaceBaseSlot.SlotKind);
        AssertIconGeometry(viewModel.Replace.ReplaceBaseSlot);
        Assert.Equal("Reference firmware input", viewModel.Replace.ReplaceBaseSlot.SlotIconTooltip);

        OpenReplace(viewModel, ExperienceIds.DpReplace);

        Assert.Contains(viewModel.Replace.ReplaceSlots, slot =>
            slot.SlotId == "replace-dp" &&
            slot.SlotKind == FirmwareSlotKind.Dp &&
            HasDrawableIcon(slot));

        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        Assert.All(
            viewModel.Replace.ReplaceSlots.Where(slot => !ReferenceEquals(slot, viewModel.Replace.ReplaceBaseSlot)),
            slot =>
            {
                Assert.Equal(FirmwareSlotKind.CtrlRam, slot.SlotKind);
                Assert.Equal("CtrlRAM BIN", slot.SlotIconTooltip);
                AssertIconGeometry(slot);
            });
    }

    /// <summary>Verifies a full FlashCode base exposes both DP and TP facts instead of treating it as TP-only.</summary>
    [Fact]
    public async Task BaseFirmwareSlotShowsFwConfigFacts()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateProductViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        using var golden = StandardMergeGoldenManifest.Load();
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));

        viewModel.SetSlotFile("replace-base", basePath);
        await CurrentInspection(viewModel).ActiveTask;

        Assert.True(viewModel.Replace.ReplaceBaseSlot.HasFirmwareFacts);
        Assert.Contains(viewModel.Replace.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "DP Version" &&
            fact.Value == "D01-00");
        Assert.Contains(viewModel.Replace.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "Jira Index" &&
            fact.Value == "AUTO_PRJ-597");
        Assert.Contains(viewModel.Replace.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "Common FW Version" && fact.Value == "1.4.1");
        Assert.Contains(viewModel.Replace.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "TP Version" &&
            fact.Value == "T01-00");
        Assert.Contains(viewModel.Replace.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "PID" && fact.Value == "0x5102");
        Assert.DoesNotContain(viewModel.Replace.ReplaceBaseSlot.FirmwareFacts, fact => fact.Label == "Refresh");
    }

    /// <summary>An erased DP region is not presented as a meaningful Unknown DP version.</summary>
    [Fact]
    public async Task UniformInvalidBaseDpRegionDoesNotPublishDpVersionFact()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-uniform-invalid-dp-fact");
        string basePath = workspace.Write(
            "erased-flash.bin",
            [.. Enumerable.Repeat((byte)0xFF, 0x40000)]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, ExperienceIds.DpReplace);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            basePath,
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(
            viewModel.Replace.ReplaceBaseSlot.FirmwareFacts,
            fact => fact.Label == "DP Version");
    }

    /// <summary>Verifies NT51951 DPCMI waits for its canonical TP FirmwareConfig prerequisite.</summary>
    [Fact]
    public async Task BaseFirmwareSlotMarksDpUnknownWithoutTpMetadata()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-base-dp-only-facts");
        byte[] bytes = [.. Enumerable.Repeat((byte)0xFF, 0x80000)];
        bytes[0x05016] = 0x40;
        bytes[0x05017] = 0xCC;
        bytes[0x05018] = 0x02;
        string basePath = workspace.Write("nt51951-no-nvt-backup.bin", bytes);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51951";
        viewModel.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;

        viewModel.SetSlotFile("replace-base", basePath);
        await CurrentInspection(viewModel).ActiveTask;

        Assert.Contains(viewModel.Replace.ReplaceBaseSlot.FirmwareFacts, fact =>
            fact.Label == "DP Version" &&
            fact.Value == "Unknown" &&
            fact.IsUnknown);
        Assert.DoesNotContain(viewModel.Replace.ReplaceBaseSlot.FirmwareFacts, fact => fact.Label == "Jira Index");
        Assert.DoesNotContain(viewModel.Replace.ReplaceBaseSlot.FirmwareFacts, fact => fact.Label is "TP Version" or "Common FW Version" or "PID");
        Assert.Equal("nt51951-ctrlram-replace.bin", viewModel.Replace.ReplaceOutputFileName);

        viewModel.SelectedLanguage = "Traditional Chinese";

        FirmwareSlotFactViewModel localizedUnknown = Assert.Single(
            viewModel.Replace.ReplaceBaseSlot.FirmwareFacts,
            static fact => fact.IsUnknown);
        Assert.Equal("未知", localizedUnknown.Value);
        Assert.Equal("未知", localizedUnknown.StateLabel);
        Assert.Contains("無法解碼 metadata", localizedUnknown.StateDetail, StringComparison.Ordinal);
        Assert.Contains("DP", localizedUnknown.StateAutomationText, StringComparison.Ordinal);
        Assert.Contains("未知", localizedUnknown.StateAutomationText, StringComparison.Ordinal);
    }

    /// <summary>Verifies DP BIN slots expose gen_flash DP version facts and mark missing evidence.</summary>
    [Fact]
    public async Task DpFirmwareSlotShowsGenFlashVersionOrTodo()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement nt51926 = golden.CaseByIc("51926");
        string dpPath = golden.ManifestPath(nt51926.GetProperty("inputs").GetProperty("dp-input"));
        string tpPath = golden.ManifestPath(nt51926.GetProperty("inputs").GetProperty("tp-input"));

        viewModel.SetSlotFile("merge-dp", dpPath);
        viewModel.SetSlotFile("merge-tp", tpPath);
        await CurrentInspection(viewModel).ActiveTask;

        FirmwareSlotViewModel dpSlot = viewModel.Merge.MergeSlots.Single(slot => slot.SlotId == "merge-dp");
        Assert.Contains(dpSlot.FirmwareFacts, fact =>
            fact.Label == "DP Version" &&
            fact.Value == "D01-00" &&
            !fact.IsWarning);
        Assert.Contains(dpSlot.FirmwareFacts, fact =>
            fact.Label == "Jira Index" &&
            fact.Value == "AUTO_PRJ-597" &&
            !fact.IsWarning);
        Assert.Matches(
            "^NT51926_FlashCode_D0100T0100_[0-9]{8}\\.bin$",
            viewModel.Merge.MergeOutputFileName);

        viewModel.WorkflowSession.SelectedIc = "NT51950";
        Assert.Equal(
            "{ic}_FlashCode_D{dp-version}T{tp-version}_{date}.bin",
            viewModel.Merge.MergeOutputFileName);
        Assert.DoesNotContain("NT51926", viewModel.Merge.MergeOutputFileName, StringComparison.Ordinal);
        JsonElement nt51950 = golden.CaseByIc("51950");
        string nt51950DpPath = golden.ManifestPath(nt51950.GetProperty("inputs").GetProperty("dp-input"));
        string nt51950TpPath = golden.ManifestPath(nt51950.GetProperty("inputs").GetProperty("tp-input"));
        viewModel.SetSlotFile("merge-dp", nt51950DpPath);
        viewModel.SetSlotFile("merge-tp", nt51950TpPath);
        await CurrentInspection(viewModel).ActiveTask;

        dpSlot = viewModel.Merge.MergeSlots.Single(slot => slot.SlotId == "merge-dp");
        Assert.Contains(dpSlot.FirmwareFacts, fact =>
            fact.Label == "DP Version" &&
            fact.Value == "DCC-00" &&
            !fact.IsWarning);
        Assert.Contains(dpSlot.FirmwareFacts, fact =>
            fact.Label == "Jira Index" &&
            fact.Value == "AUTO_PRJ-576" &&
            !fact.IsWarning);
        Assert.Matches(
            "^NT51950_FlashCode_DCC00T0400_[0-9]{8}\\.bin$",
            viewModel.Merge.MergeOutputFileName);
    }

    /// <summary>DP-only Standard Merge explains that canonical DP metadata is waiting for TP.</summary>
    [Fact]
    public async Task StandardMergeDpFactExplainsTypedTpPrerequisite()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement nt51950 = golden.CaseByIc("51950");
        string dpPath = golden.ManifestPath(nt51950.GetProperty("inputs").GetProperty("dp-input"));
        string tpPath = golden.ManifestPath(nt51950.GetProperty("inputs").GetProperty("tp-input"));
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";

        viewModel.SetSlotFile("merge-dp", dpPath);
        await CurrentInspection(viewModel).ActiveTask;

        FirmwareSlotViewModel dpSlot = viewModel.Merge.MergeSlots.Single(slot => slot.SlotId == "merge-dp");
        FirmwareSlotFactViewModel pending = Assert.Single(dpSlot.FirmwareFacts);
        Assert.Equal("DP Version", pending.Label);
        Assert.Equal("Waiting for TP BIN", pending.Value);
        Assert.True(pending.IsPendingInput);
        Assert.Contains("TP BIN", pending.StateDetail, StringComparison.Ordinal);
        Assert.Contains("Waiting for TP BIN", pending.StateAutomationText, StringComparison.Ordinal);

        viewModel.SelectedLanguage = "Traditional Chinese";

        pending = Assert.Single(dpSlot.FirmwareFacts);
        Assert.Equal("等待 TP BIN", pending.Value);
        Assert.True(pending.IsPendingInput);
        Assert.Contains("TP BIN", pending.StateDetail, StringComparison.Ordinal);
        Assert.Contains("等待 TP BIN", pending.StateAutomationText, StringComparison.Ordinal);

        viewModel.SetSlotFile("merge-tp", tpPath);
        await CurrentInspection(viewModel).ActiveTask;

        Assert.DoesNotContain(dpSlot.FirmwareFacts, static fact => fact.IsPendingInput || fact.IsUnknown);
        Assert.Contains(dpSlot.FirmwareFacts, static fact => fact.Label == "DP Version" && fact.Value == "DCC-00");
        Assert.Contains(dpSlot.FirmwareFacts, static fact => fact.Label == "Jira Index" && fact.Value == "AUTO_PRJ-576");
    }

    /// <summary>Output naming publishes unknown at selection start, latest completion, and no stale result.</summary>
    [Fact]
    public async Task OutputFileNamePublishesInspectionSnapshotLifecycle()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-output-name-snapshot");
        string dpPath = workspace.Write("dp.bin", [0x01]);
        string stalePath = workspace.Write("stale.bin", [0x02]);
        string currentPath = workspace.Write("current.bin", [0x03]);
        using var reselectionStarted = new ManualResetEventSlim();
        using var releaseReselection = new ManualResetEventSlim();
        using var staleStarted = new ManualResetEventSlim();
        using var releaseStale = new ManualResetEventSlim();
        bool blockInitialReselection = false;
        string initialVersion = "0101";
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        {
            string path = inputs.Single().Path;
            if (string.Equals(path, dpPath, StringComparison.Ordinal) && blockInitialReselection)
            {
                reselectionStarted.Set();
                releaseReselection.Wait(TestContext.Current.CancellationToken);
            }
            else if (string.Equals(path, stalePath, StringComparison.Ordinal))
            {
                staleStarted.Set();
                releaseStale.Wait(TestContext.Current.CancellationToken);
            }

            string version = string.Equals(path, stalePath, StringComparison.Ordinal)
                ? "0303"
                : string.Equals(path, currentPath, StringComparison.Ordinal)
                    ? "0404"
                    : initialVersion;
            return
            [
                .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    DpInspection(version))),
            ];
        });
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        await viewModel.WorkflowSession.SetSlotFileAsync("merge-dp", dpPath, TestContext.Current.CancellationToken);
        const string expectedTemplate = "{ic}_FlashCode_D{dp-version}T{tp-version}_{date}.bin";
        Assert.Equal(expectedTemplate, viewModel.Merge.StandardMergeOutputFileName);
        var notifications = new List<string>();
        viewModel.Merge.PropertyChanged += (_, args) =>
        {
            if (string.Equals(
                    args.PropertyName,
                    nameof(MergePresentationViewModel.StandardMergeOutputFileName),
                    StringComparison.Ordinal))
            {
                notifications.Add(viewModel.Merge.StandardMergeOutputFileName);
            }
        };

        initialVersion = "0202";
        blockInitialReselection = true;
        Task reselection = viewModel.WorkflowSession.SetSlotFileAsync(
            "merge-dp",
            dpPath,
            TestContext.Current.CancellationToken);
        Assert.True(reselectionStarted.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Contains(expectedTemplate, notifications);
        Assert.Equal(expectedTemplate, viewModel.Merge.StandardMergeOutputFileName);

        releaseReselection.Set();
        await reselection;
        Assert.Contains(expectedTemplate, notifications);
        Assert.Equal(expectedTemplate, viewModel.Merge.StandardMergeOutputFileName);

        notifications.Clear();
        Task stale = viewModel.WorkflowSession.SetSlotFileAsync(
            "merge-dp",
            stalePath,
            TestContext.Current.CancellationToken);
        Assert.True(staleStarted.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Task current = viewModel.WorkflowSession.SetSlotFileAsync(
            "merge-dp",
            currentPath,
            TestContext.Current.CancellationToken);
        Assert.Equal(expectedTemplate, viewModel.Merge.StandardMergeOutputFileName);
        try
        {
            Assert.False(current.IsCompleted);
        }
        finally
        {
            releaseStale.Set();
        }
        await Task.WhenAll(stale, current);

        Assert.Equal(expectedTemplate, viewModel.Merge.StandardMergeOutputFileName);
        FirmwareSlotViewModel currentSlot = viewModel.Merge.MergeSlots.Single(slot => slot.SlotId == "merge-dp");
        Assert.Contains(currentSlot.FirmwareFacts, fact => fact.Value == "D04-04");
        Assert.DoesNotContain(currentSlot.FirmwareFacts, fact => fact.Value == "D03-03");
    }

    /// <summary>An in-place BIN replacement is re-inspected before its output name is consumed.</summary>
    [Fact]
    public async Task OutputFileNameRefreshRejectsSamePathStaleInspection()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-output-name-identity-refresh");
        string path = workspace.Write("dp.bin", [0x01]);
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        [
            .. inputs.Select(input =>
            {
                byte version = File.ReadAllBytes(input.Path)[0];
                return new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    DpInspection($"{version:X2}{version:X2}"));
            }),
        ]);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        await viewModel.WorkflowSession.SetSlotFileAsync("merge-dp", path, TestContext.Current.CancellationToken);
        FirmwareSlotViewModel dp = viewModel.Merge.MergeSlots.Single(slot => slot.SlotId == "merge-dp");
        Assert.Contains(dp.FirmwareFacts, fact => fact.Value == "D01-01");

        File.WriteAllBytes(path, [0x02, 0x03]);
        await viewModel.WorkflowSession.RefreshSelectedMergeFirmwareInspectionsAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(dp.FirmwareFacts, fact => fact.Value == "D02-02");
    }

    /// <summary>A Build refresh reads only the active workflow's selected firmware.</summary>
    [Fact]
    public async Task OutputFileNameRefreshIsScopedToActiveWorkflow()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-output-name-workflow-refresh");
        string mergePath = workspace.Write("merge-dp.bin", [0x01]);
        string replacePath = workspace.Write("replace-base.bin", [0x02]);
        var inspectedSlotIds = new List<string>();
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        {
            inspectedSlotIds.AddRange(inputs.Select(static input => input.InspectionId));
            return
            [
                .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    DpInspection($"{File.ReadAllBytes(input.Path)[0]:X2}01"))),
            ];
        });
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        await viewModel.WorkflowSession.SetSlotFileAsync("merge-dp", mergePath, TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync("replace-base", replacePath, TestContext.Current.CancellationToken);
        inspectedSlotIds.Clear();

        await viewModel.WorkflowSession.RefreshSelectedMergeFirmwareInspectionsAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["merge-dp"], inspectedSlotIds);
    }

    /// <summary>An unstable refresh removes the prior same-path output-name projection.</summary>
    [Fact]
    public async Task OutputFileNameRefreshDoesNotRetainRejectedProjection()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-output-name-rejected-refresh");
        byte[] bytes = new byte[0x40000];
        bytes[0] = 0x01;
        string path = workspace.Write("dp.bin", bytes);
        bool mutateDuringRefresh = false;
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        [
            .. inputs.Select(input =>
            {
                byte version = File.ReadAllBytes(input.Path)[0];
                if (mutateDuringRefresh)
                {
                    using var stream = new FileStream(input.Path, FileMode.Append, FileAccess.Write, FileShare.Read);
                    stream.WriteByte(0xFF);
                }

                return new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    DpInspection($"{version:X2}{version:X2}"));
            }),
        ]);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        await viewModel.WorkflowSession.SetSlotFileAsync("merge-dp", path, TestContext.Current.CancellationToken);
        Assert.Equal(
            "{ic}_FlashCode_D{dp-version}T{tp-version}_{date}.bin",
            viewModel.Merge.StandardMergeOutputFileName);
        Assert.Equal("Waiting for TP BIN", viewModel.Merge.MergeMemoryRangeLabel);

        mutateDuringRefresh = true;
        await viewModel.WorkflowSession.RefreshSelectedMergeFirmwareInspectionsAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            "{ic}_FlashCode_D{dp-version}T{tp-version}_{date}.bin",
            viewModel.Merge.StandardMergeOutputFileName);
        Assert.Equal("DP BIN needs attention", viewModel.Merge.MergeMemoryRangeLabel);
        Assert.Equal(
            FirmwareSlotSemanticState.Error,
            viewModel.Merge.MergeSlots.Single(slot => slot.SlotId == "merge-dp").SemanticState);
    }

    /// <summary>Verifies an unobserved DP size keeps the concise DP/Jira slot badge set.</summary>
    [Fact]
    public void DpFirmwareSlotKeepsCmiSizeDiagnosticsOutOfBadges()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement nt51926 = golden.CaseByIc("51926");
        string sourcePath = golden.ManifestPath(nt51926.GetProperty("inputs").GetProperty("dp-input"));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-cmi-dp-size");
        byte[] oversizedDp = [.. File.ReadAllBytes(sourcePath), 0x00];
        string oversizedPath = workspace.Write("nt51926-unexpected-size.bin", oversizedDp);

        viewModel.SetSlotFile("merge-dp", oversizedPath);

        FirmwareSlotViewModel dpSlot = viewModel.Merge.MergeSlots.Single(slot => slot.SlotId == "merge-dp");
        Assert.Contains(dpSlot.FirmwareFacts, fact =>
            fact.Label == "Jira Index" &&
            fact.Value == "AUTO_PRJ-597" &&
            !fact.IsWarning);
        Assert.DoesNotContain(dpSlot.FirmwareFacts, fact => fact.Label == "DP size");
    }

    /// <summary>Verifies profile size diagnostics do not create an additional DP card badge.</summary>
    [Fact]
    public void DpFirmwareSlotKeepsProfileSizeDiagnosticsOutOfBadges()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51923";
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement nt51923 = golden.CaseByIc("51923");
        string sourcePath = golden.ManifestPath(nt51923.GetProperty("inputs").GetProperty("dp-input"));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-profile-dp-size");
        byte[] oversizedDp = [.. File.ReadAllBytes(sourcePath), 0x00];
        string oversizedPath = workspace.Write("nt51923-unexpected-size.bin", oversizedDp);

        viewModel.SetSlotFile("merge-dp", oversizedPath);

        FirmwareSlotViewModel dpSlot = viewModel.Merge.MergeSlots.Single(slot => slot.SlotId == "merge-dp");
        Assert.Contains(dpSlot.FirmwareFacts, fact =>
            fact.Label == "DP Version" &&
            fact.Value == "D81-00" &&
            !fact.IsWarning);
        Assert.DoesNotContain(dpSlot.FirmwareFacts, fact => fact.Label == "DP size");
    }
}
