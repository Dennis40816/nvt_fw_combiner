using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// Verifies that desktop-style Preview and Build consume one explicitly
/// accepted General selected-file draft until Reload/Rebind replaces it.
/// </summary>
public sealed class GeneralSelectedFileExecutionLifecycleTests
{
    /// <summary>The canonical preparation path rejects a missing file before accepting a session.</summary>
    [Fact]
    public async Task CanonicalPreparationRejectsUnboundSelectedFile()
    {
        var unboundDraft = new GeneralMappingDraftState(
        [
            new GeneralMappingDraftRow(
                "mapping-1",
                ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File(@"C:\does-not-exist\source.bin"),
                new ByteRange(0, 1),
                CompositionAddressSpaceIds.OutputImage,
                new ByteRange(0, 1),
                OverlapPolicy.Reject,
                alignment: 1,
                "Unbound content snapshot test."),
        ]);

        var session = new AuthoringSessionState(
            ExperienceIds.GeneralMerge);
        var progress = new RecordingAuthoringInspectionProgress();
        GeneralAuthoringSessionPreparation prepared =
            await BootstrapTestHost.Services.GeneralAuthoring
                .PrepareMergeSessionAsync(
                session,
                    "NT51950",
                    GeneralMergeAuthoringUseCase.CreateDraft(
                        ResolveGeneralMergeInitializer("0x10"),
                        unboundDraft),
                    TestContext.Current.CancellationToken,
                    progress);

        Assert.False(prepared.Succeeded);
        Assert.Equal(
            [new AuthoringInspectionProgress(0, 1), new AuthoringInspectionProgress(1, 1)],
            progress.Updates);
        Assert.Null(prepared.AcceptedSession);
        Assert.False(File.Exists(@"C:\does-not-exist\source.bin"));
    }

    /// <summary>
    /// General Merge Preview and Build reuse the immutable bytes accepted by one session.
    /// </summary>
    [Fact]
    public async Task GeneralMergeBuildReusesAcceptedBytesAfterSelectedPathChanges()
    {
        using var workspace = TempWorkspace.Create("nfc-general-merge-content-lifecycle");
        string sourcePath = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]);
        AuthoringMappingState[] inputs =
        [
            GeneralAuthoringMappingUseCase.CreateGeneralMergeAuthoringState(
                "mapping-1", sourcePath, "0x0", "0x0", "0x4"),
        ];
        GeneralMergeDraftState pending = GeneralTestDraftFactory.CreateMergeDraft("0x10", inputs);
        var session = new AuthoringSessionState(
            ExperienceIds.GeneralMerge);
        ActiveSessionSnapshot acceptedSession = await AcceptGeneralMergeSessionAsync(
            session, "NT51950", pending);
        GeneralMergeDraftState accepted = Assert.IsType<GeneralMergeDraftState>(
            acceptedSession.DraftState);
        ResolvedCapability capability =
            Assert.IsType<ResolvedCapability>(
                acceptedSession.GetAcceptedCapability(AuthoringDerivedResultKind.Validation));
        CompositionRunResult preview =
            await BootstrapTestHost.Services.CompositionExecution.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    acceptedSession,
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    build: false),
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken);

        Assert.True(preview.Succeeded, CompositionRunReportJson.Serialize(preview));
        Assert.Same(capability, preview.ResolvedCapability);
        GeneralMappingDraftState acceptedDraft =
            Assert.IsType<GeneralMappingDraftState>(
                preview.AcceptedGeneralMappingDraft);
        GeneralMappingDraftRow acceptedRow = Assert.Single(acceptedDraft.Rows);
        FileStamp acceptedStamp = Assert.IsType<FileStamp>(
            acceptedRow.Source.AcceptedFileStamp);
        await File.WriteAllBytesAsync(
            sourcePath,
            [0x10, 0x11, 0x99, 0x13],
            TestContext.Current.CancellationToken);
        string outputPath = workspace.PathFor("output.bin");

        CompositionRunResult build =
            await BootstrapTestHost.Services.CompositionExecution.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    acceptedSession,
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    build: true,
                    outputPath: outputPath),
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken);

        Assert.True(build.Succeeded, CompositionRunReportJson.Serialize(build));
        Assert.Equal(
            [0x10, 0x11, 0x12, 0x13, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            await File.ReadAllBytesAsync(
                outputPath,
                TestContext.Current.CancellationToken));
        AssertReportInputHash(
            build,
            Path.GetFileName(sourcePath),
            acceptedStamp.Sha256);

        GeneralAuthoringSessionPreparation rebound =
            await BootstrapTestHost.Services.GeneralAuthoring
                .PrepareMergeSessionAsync(
                    session,
                    "NT51950",
                    pending,
                    TestContext.Current.CancellationToken);
        Assert.True(
            rebound.Succeeded,
            string.Join(" | ", rebound.Issues.Select(static issue => issue.Message)));
        Assert.Same(
            capability,
            rebound.AcceptedSession!.GetAcceptedCapability(
                AuthoringDerivedResultKind.Validation));
        Assert.Equal(
            FileStamp.FromBytes([0x10, 0x11, 0x99, 0x13]),
            Assert.Single(rebound.AcceptedSession.Slots).FileStamp);
    }

    /// <summary>High-level General Replace preparation publishes one exact accepted session.</summary>
    [Fact]
    public async Task GeneralReplacePreparationPublishesOneAcceptedSession()
    {
        using var workspace = TempWorkspace.Create("nfc-general-replace-preparation");
        string basePath = workspace.Write("base.bin", new byte[0x40000]);
        string sourcePath = workspace.Write("source.bin", [0xA5, 0x5A]);
        GeneralMappingDraftState draft = GeneralTestDraftFactory.CreateReplaceDraft(
        [
            GeneralAuthoringMappingUseCase.CreateGeneralReplaceAuthoringState(
                "general-map-1",
                GeneralMappingSourceKind.FileArtifact,
                sourcePath,
                "0x3E020",
                "0x2"),
        ]);
        var session = new AuthoringSessionState(ExperienceIds.GeneralReplace);
        var progress = new RecordingAuthoringInspectionProgress();

        GeneralAuthoringSessionPreparation prepared =
            await BootstrapTestHost.Services.GeneralAuthoring
                .PrepareReplaceSessionAsync(
                session,
                "NT51926",
                IcNumberSelectionTokens.SingleChip,
                basePath,
                draft,
                TestContext.Current.CancellationToken,
                progress);

        Assert.True(
            prepared.Succeeded,
            string.Join(" | ", prepared.Issues.Select(static issue => issue.Message)));
        Assert.Equal(
            [
                new AuthoringInspectionProgress(0, 2),
                new AuthoringInspectionProgress(1, 2),
                new AuthoringInspectionProgress(2, 2),
            ],
            progress.Updates);
        ResolvedCapability initialCapability = Assert.IsType<ResolvedCapability>(
            prepared.AcceptedSession!.GetAcceptedCapability(
                AuthoringDerivedResultKind.Validation));
        InputArtifactBinding plannedSource = Assert.Single(
            initialCapability.GeneralExecutionPlan!.InputBindings,
            static binding => StringComparer.Ordinal.Equals(
                binding.BindingId,
                "general-map-1"));
        Assert.Null(plannedSource.AcceptedContentStamp);

        await File.WriteAllBytesAsync(
            sourcePath,
            [0xA5, 0xC3],
            TestContext.Current.CancellationToken);
        GeneralAuthoringSessionPreparation rebound =
            await BootstrapTestHost.Services.GeneralAuthoring
                .PrepareReplaceSessionAsync(
                session,
                "NT51926",
                IcNumberSelectionTokens.SingleChip,
                basePath,
                draft,
                TestContext.Current.CancellationToken);
        Assert.True(
            rebound.Succeeded,
            string.Join(" | ", rebound.Issues.Select(static issue => issue.Message)));
        Assert.Same(
            initialCapability,
            rebound.AcceptedSession!.GetAcceptedCapability(
                AuthoringDerivedResultKind.Validation));
        Assert.Equal(
            FileStamp.FromBytes([0xA5, 0xC3]),
            rebound.AcceptedSession.Slots.Single(slot =>
                StringComparer.Ordinal.Equals(slot.DefinitionId, "general-map-1")).FileStamp);
    }

    /// <summary>
    /// General Replace Preview and Build reuse the immutable bytes accepted by one session.
    /// </summary>
    [Fact]
    public async Task GeneralReplaceBuildReusesAcceptedBytesAfterSelectedPathChanges()
    {
        using var workspace = TempWorkspace.Create("nfc-general-replace-content-lifecycle");
        string basePath = workspace.Write("base.bin", new byte[0x40000]);
        string sourcePath = workspace.Write("source.bin", [0xA5, 0x5A]);
        IReadOnlyDictionary<string, string> slotPaths =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionSlotIds.ReplaceBase] = basePath,
            };
        AuthoringMappingState[] inputs =
        [
            GeneralAuthoringMappingUseCase.CreateGeneralReplaceAuthoringState(
                "mapping-1",
                GeneralMappingSourceKind.FileArtifact,
                sourcePath,
                "0x3E020",
                "0x2"),
        ];
        GeneralMappingDraftState pending = GeneralTestDraftFactory.CreateReplaceDraft(inputs);
        var session = new AuthoringSessionState(
            ExperienceIds.GeneralReplace);
        (ActiveSessionSnapshot acceptedSession, CapabilityActionReadinessSnapshot readiness) =
            await AcceptGeneralReplaceSessionAsync(
                session, "NT51926", "single", basePath, pending);
        ResolvedCapability capability =
            Assert.IsType<ResolvedCapability>(
                acceptedSession.GetAcceptedCapability(AuthoringDerivedResultKind.Validation));
        CompositionRunResult preview =
            await BootstrapTestHost.Services.CompositionExecution.ExecuteAsync(
                new AcceptedCompositionExecutionRequest(
                    acceptedSession,
                     slotPaths,
                     build: false,
                     actionReadiness: readiness),
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken);

        Assert.True(preview.Succeeded, CompositionRunReportJson.Serialize(preview));
        Assert.Same(capability, preview.ResolvedCapability);
        GeneralMappingDraftState acceptedDraft =
            Assert.IsType<GeneralMappingDraftState>(
                preview.AcceptedGeneralMappingDraft);
        GeneralMappingDraftRow acceptedRow = Assert.Single(acceptedDraft.Rows);
        FileStamp acceptedStamp = Assert.IsType<FileStamp>(
            acceptedRow.Source.AcceptedFileStamp);
        await File.WriteAllBytesAsync(
            sourcePath,
            [0xA5, 0xC3],
            TestContext.Current.CancellationToken);
        byte[] changedBase = new byte[0x40000];
        changedBase[0] = 0x77;
        await File.WriteAllBytesAsync(
            basePath,
            changedBase,
            TestContext.Current.CancellationToken);
        string outputPath = workspace.PathFor("output.bin");

        CompositionRunResult build =
            await BootstrapTestHost.Services.CompositionExecution.ExecuteAsync(
                    new AcceptedCompositionExecutionRequest(
                        acceptedSession,
                        slotPaths,
                         build: true,
                         outputPath: outputPath,
                         actionReadiness: readiness),
                    new CompositionRunProgressFeed(),
                    TestContext.Current.CancellationToken);

        Assert.True(build.Succeeded, CompositionRunReportJson.Serialize(build));
        byte[] output = await File.ReadAllBytesAsync(
            outputPath,
            TestContext.Current.CancellationToken);
        Assert.Equal(0xA5, output[0x3E020]);
        Assert.Equal(0x5A, output[0x3E021]);
        Assert.Equal(0x00, output[0]);
        AssertReportInputHash(
            build,
            Path.GetFileName(sourcePath),
            acceptedStamp.Sha256);
    }

    /// <summary>General Merge batch preparation accepts two files under one exact compilation.</summary>
    [Fact]
    public async Task GeneralMergeTwoFilesRetainExactCompilationAndReachReadiness()
    {
        using var workspace = TempWorkspace.Create("nfc-general-merge-two-files");
        string firstPath = workspace.Write("first.bin", [0x11, 0x12]);
        string secondPath = workspace.Write("second.bin", [0x21, 0x22]);
        GeneralMergeDraftState pending = GeneralTestDraftFactory.CreateMergeDraft(
            "0x10",
            [
                GeneralAuthoringMappingUseCase.CreateGeneralMergeAuthoringState(
                    "mapping-1", firstPath, "0x0", "0x0", "0x2"),
                GeneralAuthoringMappingUseCase.CreateGeneralMergeAuthoringState(
                    "mapping-2", secondPath, "0x0", "0x2", "0x2"),
            ]);
        var session = new AuthoringSessionState(
            ExperienceIds.GeneralMerge);

        GeneralAuthoringSessionPreparation prepared =
            await BootstrapTestHost.Services.GeneralAuthoring
                .PrepareMergeSessionAsync(
                session,
                "NT51950",
                pending,
                TestContext.Current.CancellationToken);

        Assert.True(prepared.Succeeded, string.Join(
            " | ",
            prepared.Issues.Select(static issue => $"{issue.Code}: {issue.Message}")));
        Assert.All(prepared.AcceptedSession!.Slots, static slot => Assert.True(
            slot.Lifecycle is AuthoringSlotLifecycle.Verified or AuthoringSlotLifecycle.Warning));
        Assert.True(prepared.Readiness!.Build.IsAvailable);
        Assert.NotNull(prepared.AcceptedSession.GetAcceptedCapability(
            AuthoringDerivedResultKind.Validation));
    }

    /// <summary>General Replace batch preparation accepts two files under one exact compilation.</summary>
    [Fact]
    public async Task GeneralReplaceTwoFilesRetainExactCompilationAndReachReadiness()
    {
        using var workspace = TempWorkspace.Create("nfc-general-replace-two-files");
        string basePath = workspace.Write("base.bin", new byte[0x40000]);
        string firstPath = workspace.Write("first.bin", [0x31, 0x32]);
        string secondPath = workspace.Write("second.bin", [0x41, 0x42]);
        GeneralMappingDraftState pending = GeneralTestDraftFactory.CreateReplaceDraft(
        [
            GeneralAuthoringMappingUseCase.CreateGeneralReplaceAuthoringState(
                "mapping-1", GeneralMappingSourceKind.FileArtifact,
                firstPath, "0x3E020", "0x2"),
            GeneralAuthoringMappingUseCase.CreateGeneralReplaceAuthoringState(
                "mapping-2", GeneralMappingSourceKind.FileArtifact,
                secondPath, "0x3E022", "0x2"),
        ]);
        var session = new AuthoringSessionState(
            ExperienceIds.GeneralReplace);

        GeneralAuthoringSessionPreparation prepared =
            await BootstrapTestHost.Services.GeneralAuthoring
                .PrepareReplaceSessionAsync(
                session,
                "NT51926",
                IcNumberSelectionTokens.SingleChip,
                basePath,
                pending,
                TestContext.Current.CancellationToken);

        Assert.True(prepared.Succeeded, string.Join(
            " | ",
            prepared.Issues.Select(static issue => $"{issue.Code}: {issue.Message}")));
        Assert.All(prepared.AcceptedSession!.Slots, static slot => Assert.True(
            slot.Lifecycle is AuthoringSlotLifecycle.Verified or AuthoringSlotLifecycle.Warning));
        Assert.True(prepared.Readiness!.Build.IsAvailable);
        Assert.NotNull(prepared.AcceptedSession.GetAcceptedCapability(
            AuthoringDerivedResultKind.Validation));
    }

    /// <summary>General Replace readiness publishes the revision that rebound its Base.</summary>
    [Fact]
    public async Task GeneralReplaceReboundReferenceReadinessUsesAcceptedRevision()
    {
        using var workspace = TempWorkspace.Create(
            "nfc-general-replace-reference-readiness-revision");
        string originalBasePath = workspace.Write("base.bin", new byte[0x40000]);
        byte[] reboundBase = new byte[0x40000];
        reboundBase[0] = 0x7E;
        string reboundBasePath = workspace.Write("rebound-base.bin", reboundBase);
        string sourcePath = workspace.Write("source.bin", [0xA5, 0x5A]);
        GeneralMappingDraftState pending = GeneralTestDraftFactory.CreateReplaceDraft(
        [
            GeneralAuthoringMappingUseCase.CreateGeneralReplaceAuthoringState(
                "mapping-1",
                GeneralMappingSourceKind.FileArtifact,
                sourcePath,
                "0x3E020",
                "0x2",
                FileStamp.FromBytes(File.ReadAllBytes(sourcePath))),
        ]);
        var session = new AuthoringSessionState(
            ExperienceIds.GeneralReplace);
        GeneralAuthoringSessionPreparation initial =
            await BootstrapTestHost.Services.GeneralAuthoring
                .PrepareReplaceSessionAsync(
                session,
                "NT51926",
                IcNumberSelectionTokens.SingleChip,
                originalBasePath,
                pending,
                TestContext.Current.CancellationToken);
        Assert.True(initial.Succeeded, string.Join(" | ", initial.Issues));

        GeneralAuthoringSessionPreparation rebound =
            await BootstrapTestHost.Services.GeneralAuthoring
                .PrepareReplaceSessionAsync(
                session,
                "NT51926",
                IcNumberSelectionTokens.SingleChip,
                reboundBasePath,
                pending,
                TestContext.Current.CancellationToken);

        Assert.True(rebound.Succeeded, string.Join(" | ", rebound.Issues));
        Assert.Equal(
            session.CurrentSnapshot!.AuthoringRevision,
            rebound.Readiness!.AuthoringRevision);
        Assert.NotNull(session.CurrentSnapshot.GetAcceptedCapability(
            AuthoringDerivedResultKind.Validation));
    }

    private static async Task<ActiveSessionSnapshot> AcceptGeneralMergeSessionAsync(
        AuthoringSessionState session,
        string icId,
        GeneralMergeDraftState draft)
    {
        GeneralAuthoringSessionPreparation prepared =
            await BootstrapTestHost.Services.GeneralAuthoring
                .PrepareMergeSessionAsync(
                session,
                icId,
                draft,
                TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded, string.Join(" | ", prepared.Issues));
        return prepared.AcceptedSession!;
    }

    private static async Task<(
        ActiveSessionSnapshot Session,
        CapabilityActionReadinessSnapshot Readiness)> AcceptGeneralReplaceSessionAsync(
        AuthoringSessionState session,
        string icId,
        string number,
        string referencePath,
        GeneralMappingDraftState draft)
    {
        GeneralAuthoringSessionPreparation prepared =
            await BootstrapTestHost.Services.GeneralAuthoring
                .PrepareReplaceSessionAsync(
                session,
                icId,
                number,
                referencePath,
                draft,
                TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded, string.Join(" | ", prepared.Issues));
        return (prepared.AcceptedSession!, prepared.Readiness!);
    }

    private static GeneralMergeInitializer ResolveGeneralMergeInitializer(
        string outputLength)
    {
        Assert.True(
            GeneralMergeAuthoringUseCase.TryResolveOutputInitializer(
                outputLength,
                outputFillByte: null,
                out GeneralMergeInitializer? initializer));
        return initializer;
    }

    private static void AssertReportInputHash(
        CompositionRunResult result,
        string originalFileName,
        string expectedSha256)
    {
        using var report = JsonDocument.Parse(CompositionRunReportJson.Serialize(result));
        JsonElement input = Assert.Single(
            report.RootElement.GetProperty("Inputs").EnumerateArray(),
            input => StringComparer.Ordinal.Equals(
                input.GetProperty("OriginalFileName").GetString(),
                originalFileName));
        Assert.Equal(expectedSha256, input.GetProperty("Sha256").GetString());
    }
}
