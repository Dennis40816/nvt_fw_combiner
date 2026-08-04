using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// Verifies that desktop-style Preview and Build consume one explicitly
/// accepted General selected-file draft until Reload/Rebind replaces it.
/// </summary>
public sealed class GeneralSelectedFileExecutionLifecycleTests
{
    /// <summary>Strict desktop execution rejects an uninspected draft without touching its path.</summary>
    [Fact]
    public async Task AcceptedSessionRunnerRejectsUnboundSelectedFile()
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

        AuthoringSessionState session = MergeAuthoringSessionSet.CreateEphemeral(
            ExperienceIds.GeneralMerge);
        Assert.NotNull(WorkbenchCompositionService.BeginGeneralMergeSelectedFileInspection(
            session,
            "NT51950",
            WorkbenchCompositionService.CreateGeneralMergeDraft(
                ResolveGeneralMergeInitializer("0x10"),
                unboundDraft),
            "mapping-1",
            observedLength: 1).Lease);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await WorkbenchCompositionService.RunGeneralMergeAcceptedSessionWithProgressAsync(
                "NT51950",
                session.CurrentSnapshot!,
                build: false,
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken));
        Assert.False(File.Exists(@"C:\does-not-exist\source.bin"));
    }

    /// <summary>
    /// General Merge rejects same-size mutation against its Preview snapshot,
    /// then succeeds after explicit Reload without changing the mapping range.
    /// </summary>
    [Fact]
    public async Task GeneralMergePreviewMutationBuildRequiresExplicitReload()
    {
        using var workspace = TempWorkspace.Create("nfc-general-merge-content-lifecycle");
        string sourcePath = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]);
        AuthoringMappingState[] inputs =
        [
            WorkbenchCompositionService.CreateGeneralMergeAuthoringState(
                "mapping-1", sourcePath, "0x0", "0x0", "0x4"),
        ];
        GeneralMergeDraftState pending = GeneralTestDraftFactory.CreateMergeDraft("0x10", inputs);
        AuthoringSessionState session = MergeAuthoringSessionSet.CreateEphemeral(
            ExperienceIds.GeneralMerge);
        ActiveSessionSnapshot acceptedSession = await AcceptGeneralMergeSessionAsync(
            session, "NT51950", pending, "mapping-1", sourcePath, expectedLength: 4);
        GeneralMergeDraftState accepted = Assert.IsType<GeneralMergeDraftState>(
            acceptedSession.DraftState);
        ResolvedCapability capability =
            Assert.IsType<ResolvedCapability>(
                acceptedSession.GetAcceptedCapability(AuthoringDerivedResultKind.Validation));
        WorkbenchRunResult preview =
            await WorkbenchCompositionService.RunGeneralMergeAcceptedSessionWithProgressAsync(
                "NT51950",
                acceptedSession,
                build: false,
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken);

        Assert.True(preview.Succeeded, preview.ReportJson);
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
        string staleOutputPath = workspace.PathFor("stale-output.bin");

        WorkbenchRunResult staleBuild =
            await WorkbenchCompositionService.RunGeneralMergeAcceptedSessionWithProgressAsync(
                "NT51950",
                acceptedSession,
                build: true,
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken,
                staleOutputPath);

        Assert.False(staleBuild.Succeeded);
        Assert.False(File.Exists(staleOutputPath));
        AssertIssue(
            staleBuild,
            CompositionRunIssueCodes.InputArtifactContentSnapshotMismatch);
        Assert.Equal(
            acceptedStamp,
            Assert.Single(staleBuild.AcceptedGeneralMappingDraft!.Rows)
                .Source.AcceptedFileStamp);

        var rebound = new GeneralMergeDraftState(
            accepted.OutputInitializer,
            new GeneralMappingDraftState(acceptedDraft.Rows.Select(static row =>
                row.RebindSelectedFile(row.Source.Reference))));
        ActiveSessionSnapshot reloadedSession = await AcceptGeneralMergeSessionAsync(
            session, "NT51950", rebound, "mapping-1", sourcePath, expectedLength: 4);
        GeneralMappingDraftRow reloadedRow = Assert.Single(
            Assert.IsType<GeneralMergeDraftState>(reloadedSession.DraftState).Mappings.Rows);
        Assert.NotEqual(acceptedStamp, reloadedRow.Source.AcceptedFileStamp);
        Assert.Equal(acceptedRow.SourceRange, reloadedRow.SourceRange);
        Assert.Equal(acceptedRow.TargetRange, reloadedRow.TargetRange);

        string outputPath = workspace.PathFor("output.bin");
        WorkbenchRunResult build =
            await WorkbenchCompositionService.RunGeneralMergeAcceptedSessionWithProgressAsync(
                "NT51950",
                reloadedSession,
                build: true,
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken,
                outputPath);

        Assert.True(build.Succeeded, build.ReportJson);
        Assert.Equal(
            [0x10, 0x11, 0x99, 0x13, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            await File.ReadAllBytesAsync(
                outputPath,
                TestContext.Current.CancellationToken));
        AssertReportInputHash(
            build,
            Path.GetFileName(sourcePath),
            reloadedRow.Source.AcceptedFileStamp!.Value.Sha256);
    }

    /// <summary>
    /// General Replace uses the same accepted-draft lifecycle and never
    /// silently rebinds changed replacement bytes at Build.
    /// </summary>
    [Fact]
    public async Task GeneralReplacePreviewMutationBuildRequiresExplicitReload()
    {
        using var workspace = TempWorkspace.Create("nfc-general-replace-content-lifecycle");
        string basePath = workspace.Write("base.bin", new byte[0x40000]);
        string sourcePath = workspace.Write("source.bin", [0xA5, 0x5A]);
        IReadOnlyDictionary<string, string> slotPaths =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [WorkbenchSlotIds.ReplaceBase] = basePath,
            };
        AuthoringMappingState[] inputs =
        [
            WorkbenchCompositionService.CreateGeneralReplaceAuthoringState(
                "mapping-1",
                GeneralMappingSourceKind.FileArtifact,
                sourcePath,
                "0x3E020",
                "0x2"),
        ];
        GeneralMappingDraftState pending = GeneralTestDraftFactory.CreateReplaceDraft(inputs);
        AuthoringSessionState session = ReplaceAuthoringSessionSet.CreateEphemeral(
            ExperienceIds.GeneralReplace);
        ActiveSessionSnapshot acceptedSession = await AcceptGeneralReplaceSessionAsync(
            session, "NT51926", "single", 0x40000, pending,
            "mapping-1", sourcePath, expectedLength: 2);
        ResolvedCapability capability =
            Assert.IsType<ResolvedCapability>(
                acceptedSession.GetAcceptedCapability(AuthoringDerivedResultKind.Validation));
        WorkbenchRunResult preview =
            await WorkbenchCompositionService.RunGeneralReplaceAcceptedSessionWithProgressAsync(
                "NT51926",
                "single",
                slotPaths,
                acceptedSession,
                build: false,
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken);

        Assert.True(preview.Succeeded, preview.ReportJson);
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
        string staleOutputPath = workspace.PathFor("stale-output.bin");

        WorkbenchRunResult staleBuild =
            await WorkbenchCompositionService
                .RunGeneralReplaceAcceptedSessionWithProgressAsync(
                    "NT51926",
                    "single",
                    slotPaths,
                    acceptedSession,
                    build: true,
                    new CompositionRunProgressFeed(),
                    TestContext.Current.CancellationToken,
                    staleOutputPath);

        Assert.False(staleBuild.Succeeded);
        Assert.False(File.Exists(staleOutputPath));
        AssertIssue(
            staleBuild,
            CompositionRunIssueCodes.InputArtifactContentSnapshotMismatch);

        var rebound = new GeneralMappingDraftState(acceptedDraft.Rows.Select(static row =>
            row.RebindSelectedFile(row.Source.Reference)));
        ActiveSessionSnapshot reloadedSession = await AcceptGeneralReplaceSessionAsync(
            session, "NT51926", "single", 0x40000, rebound,
            "mapping-1", sourcePath, expectedLength: 2);
        GeneralMappingDraftRow reloadedRow = Assert.Single(
            Assert.IsType<GeneralMappingDraftState>(reloadedSession.DraftState).Rows);
        Assert.NotEqual(acceptedStamp, reloadedRow.Source.AcceptedFileStamp);
        Assert.Equal(acceptedRow.SourceRange, reloadedRow.SourceRange);
        Assert.Equal(acceptedRow.TargetRange, reloadedRow.TargetRange);

        string outputPath = workspace.PathFor("output.bin");
        WorkbenchRunResult build =
            await WorkbenchCompositionService
                .RunGeneralReplaceAcceptedSessionWithProgressAsync(
                    "NT51926",
                    "single",
                    slotPaths,
                    reloadedSession,
                    build: true,
                    new CompositionRunProgressFeed(),
                    TestContext.Current.CancellationToken,
                    outputPath);

        Assert.True(build.Succeeded, build.ReportJson);
        byte[] output = await File.ReadAllBytesAsync(
            outputPath,
            TestContext.Current.CancellationToken);
        Assert.Equal(0xA5, output[0x3E020]);
        Assert.Equal(0xC3, output[0x3E021]);
        AssertReportInputHash(
            build,
            Path.GetFileName(sourcePath),
            reloadedRow.Source.AcceptedFileStamp!.Value.Sha256);
    }

    /// <summary>Sequential General Merge file inspection retains one exact compilation.</summary>
    [Fact]
    public async Task GeneralMergeTwoFilesRetainExactCompilationAndReachReadiness()
    {
        using var workspace = TempWorkspace.Create("nfc-general-merge-two-files");
        string firstPath = workspace.Write("first.bin", [0x11, 0x12]);
        string secondPath = workspace.Write("second.bin", [0x21, 0x22]);
        GeneralMergeDraftState pending = GeneralTestDraftFactory.CreateMergeDraft(
            "0x10",
            [
                WorkbenchCompositionService.CreateGeneralMergeAuthoringState(
                    "mapping-1", firstPath, "0x0", "0x0", "0x2"),
                WorkbenchCompositionService.CreateGeneralMergeAuthoringState(
                    "mapping-2", secondPath, "0x0", "0x2", "0x2"),
            ]);
        AuthoringSessionState session = MergeAuthoringSessionSet.CreateEphemeral(
            ExperienceIds.GeneralMerge);

        _ = await AcceptGeneralMergeSessionAsync(
            session, "NT51950", pending, "mapping-1", firstPath, 2,
            expectReady: false);
        GeneralMergeDraftState afterFirst = Assert.IsType<GeneralMergeDraftState>(
            session.CurrentSnapshot!.DraftState);
        ActiveSessionSnapshot second = await AcceptGeneralMergeSessionAsync(
            session, "NT51950", afterFirst, "mapping-2", secondPath, 2,
            expectReady: false);
        ResolvedCapability exact = Assert.IsType<ResolvedCapability>(second.ExactCapability);
        GeneralMergeDraftState afterSecond = Assert.IsType<GeneralMergeDraftState>(
            second.DraftState);
        AcceptCachedGeneralMergeInspection(
            session, "NT51950", afterSecond, "mapping-1", firstPath);
        CapabilityActionReadinessSnapshot? readiness =
            WorkbenchCompositionService.GetGeneralMergeActionReadiness(
                session,
                "NT51950",
                Assert.IsType<GeneralMergeDraftState>(session.CurrentSnapshot!.DraftState));

        Assert.Same(exact, session.CurrentSnapshot!.ExactCapability);
        Assert.All(session.CurrentSnapshot.Slots, static slot => Assert.True(
            slot.Lifecycle is AuthoringSlotLifecycle.Verified or AuthoringSlotLifecycle.Warning));
        Assert.NotNull(readiness);
        Assert.NotNull(session.CurrentSnapshot.GetAcceptedCapability(
            AuthoringDerivedResultKind.Validation));
    }

    /// <summary>Sequential General Replace file inspection retains one exact compilation.</summary>
    [Fact]
    public async Task GeneralReplaceTwoFilesRetainExactCompilationAndReachReadiness()
    {
        using var workspace = TempWorkspace.Create("nfc-general-replace-two-files");
        string firstPath = workspace.Write("first.bin", [0x31, 0x32]);
        string secondPath = workspace.Write("second.bin", [0x41, 0x42]);
        GeneralMappingDraftState pending = GeneralTestDraftFactory.CreateReplaceDraft(
        [
            WorkbenchCompositionService.CreateGeneralReplaceAuthoringState(
                "mapping-1", GeneralMappingSourceKind.FileArtifact,
                firstPath, "0x3E020", "0x2"),
            WorkbenchCompositionService.CreateGeneralReplaceAuthoringState(
                "mapping-2", GeneralMappingSourceKind.FileArtifact,
                secondPath, "0x3E022", "0x2"),
        ]);
        AuthoringSessionState session = ReplaceAuthoringSessionSet.CreateEphemeral(
            ExperienceIds.GeneralReplace);

        _ = await AcceptGeneralReplaceSessionAsync(
            session, "NT51926", "single", 0x40000, pending,
            "mapping-1", firstPath, 2, expectReady: false);
        GeneralMappingDraftState afterFirst =
            Assert.IsType<GeneralMappingDraftState>(session.CurrentSnapshot!.DraftState);
        ActiveSessionSnapshot second = await AcceptGeneralReplaceSessionAsync(
            session, "NT51926", "single", 0x40000, afterFirst,
            "mapping-2", secondPath, 2, expectReady: false);
        ResolvedCapability exact = Assert.IsType<ResolvedCapability>(second.ExactCapability);
        GeneralMappingDraftState afterSecond = Assert.IsType<GeneralMappingDraftState>(
            second.DraftState);
        AcceptCachedGeneralReplaceInspection(
            session,
            "NT51926",
            "single",
            0x40000,
            afterSecond,
            "mapping-1",
            firstPath);
        CapabilityActionReadinessSnapshot? readiness = await WorkbenchCompositionService
            .GetGeneralReplaceActionReadinessAsync(
                session,
                "NT51926",
                "single",
                0x40000,
                Assert.IsType<GeneralMappingDraftState>(session.CurrentSnapshot!.DraftState),
                baseFirmware: null,
                TestContext.Current.CancellationToken);

        Assert.Same(exact, session.CurrentSnapshot!.ExactCapability);
        Assert.All(session.CurrentSnapshot.Slots, static slot => Assert.True(
            slot.Lifecycle is AuthoringSlotLifecycle.Verified or AuthoringSlotLifecycle.Warning));
        Assert.NotNull(readiness);
        Assert.NotNull(session.CurrentSnapshot.GetAcceptedCapability(
            AuthoringDerivedResultKind.Validation));
    }

    private static async Task<ActiveSessionSnapshot> AcceptGeneralMergeSessionAsync(
        AuthoringSessionState session,
        string icId,
        GeneralMergeDraftState draft,
        string mappingId,
        string sourcePath,
        long expectedLength,
        bool expectReady = true)
    {
        AuthoringSlotInspectionStartResult started =
            WorkbenchCompositionService.BeginGeneralMergeSelectedFileInspection(
                session, icId, draft, mappingId, expectedLength);
        if (!started.Succeeded && !expectReady)
        {
            Assert.True(WorkbenchCompositionService.PrepareGeneralMergeSelectionSession(
                session,
                icId,
                draft.Mappings.Rows.Select(static row => row.MappingId)));
            Assert.True(session.SetDraft(draft).Succeeded);
            Assert.True(session.SetSlotFile(mappingId, sourcePath, fileStamp: null).Succeeded);
            AuthoringPublicationLease lease = session.CapturePublicationLease(
                AuthoringDerivedResultKind.Inspection);
            GeneralSelectedFileInspectionResult cached =
                await WorkbenchCompositionService.InspectGeneralSelectedFileAsync(
                    mappingId,
                    sourcePath,
                    lease.AuthoringRevision,
                    expectedLength,
                    TestContext.Current.CancellationToken);
            Assert.True(session.TryCacheGeneralSelectedFileInspection(
                lease, cached.Inspection!).Succeeded);
            return session.CurrentSnapshot!;
        }
        Assert.True(started.Succeeded, started.Issue?.Message + DescribeGeneralSession(
            session, draft.Mappings, mappingId));
        GeneralSelectedFileInspectionResult inspection =
            await WorkbenchCompositionService.InspectGeneralSelectedFileAsync(
                mappingId,
                sourcePath,
                started.Snapshot!.AuthoringRevision,
                expectedLength,
                TestContext.Current.CancellationToken);
        Assert.True(session.TryAcceptSlotFileInspection(
            started.Lease!, inspection.Inspection!).Succeeded);
        GeneralMergeDraftState accepted = Assert.IsType<GeneralMergeDraftState>(
            session.CurrentSnapshot!.DraftState);
        CapabilityActionReadinessSnapshot? readiness =
            WorkbenchCompositionService.GetGeneralMergeActionReadiness(
                session, icId, accepted);
        Assert.Equal(expectReady, readiness is not null);
        return session.CurrentSnapshot!;
    }

    private static async Task<ActiveSessionSnapshot> AcceptGeneralReplaceSessionAsync(
        AuthoringSessionState session,
        string icId,
        string number,
        long capacity,
        GeneralMappingDraftState draft,
        string mappingId,
        string sourcePath,
        long expectedLength,
        bool expectReady = true)
    {
        AuthoringSlotInspectionStartResult started =
            WorkbenchCompositionService.BeginGeneralReplaceSelectedFileInspection(
                session, icId, number, capacity, draft, mappingId, expectedLength);
        if (!started.Succeeded && !expectReady)
        {
            Assert.True(WorkbenchCompositionService.PrepareGeneralReplaceSelectionSession(
                session,
                icId,
                capacity,
                draft.Rows.Select(static row => row.MappingId)));
            Assert.True(session.SetDraft(draft).Succeeded);
            Assert.True(session.SetSlotFile(mappingId, sourcePath, fileStamp: null).Succeeded);
            AuthoringPublicationLease lease = session.CapturePublicationLease(
                AuthoringDerivedResultKind.Inspection);
            GeneralSelectedFileInspectionResult cached =
                await WorkbenchCompositionService.InspectGeneralSelectedFileAsync(
                    mappingId,
                    sourcePath,
                    lease.AuthoringRevision,
                    expectedLength,
                    TestContext.Current.CancellationToken);
            Assert.True(session.TryCacheGeneralSelectedFileInspection(
                lease, cached.Inspection!).Succeeded);
            return session.CurrentSnapshot!;
        }
        Assert.True(started.Succeeded, started.Issue?.Message + DescribeGeneralSession(
            session, draft, mappingId));
        GeneralSelectedFileInspectionResult inspection =
            await WorkbenchCompositionService.InspectGeneralSelectedFileAsync(
                mappingId,
                sourcePath,
                started.Snapshot!.AuthoringRevision,
                expectedLength,
                TestContext.Current.CancellationToken);
        Assert.True(session.TryAcceptSlotFileInspection(
            started.Lease!, inspection.Inspection!).Succeeded);
        GeneralMappingDraftState accepted = Assert.IsType<GeneralMappingDraftState>(
            session.CurrentSnapshot!.DraftState);
        CapabilityActionReadinessSnapshot? readiness = await WorkbenchCompositionService
            .GetGeneralReplaceActionReadinessAsync(
            session,
            icId,
            number,
            capacity,
            accepted,
            baseFirmware: null,
            TestContext.Current.CancellationToken);
        Assert.Equal(expectReady, readiness is not null);
        return session.CurrentSnapshot!;
    }

    private static void AcceptCachedGeneralMergeInspection(
        AuthoringSessionState session,
        string icId,
        GeneralMergeDraftState draft,
        string mappingId,
        string sourcePath)
    {
        Assert.True(session.TryGetCachedGeneralSelectedFileInspection(
            mappingId, sourcePath, out GeneralSelectedFileInspection? cached));
        AuthoringSlotInspectionStartResult started =
            WorkbenchCompositionService.BeginGeneralMergeSelectedFileInspection(
                session,
                icId,
                draft,
                mappingId,
                cached.FileStamp.AcceptedLength);
        Assert.True(started.Succeeded, started.Issue?.Message + DescribeGeneralSession(
            session, draft.Mappings, mappingId));
        Assert.True(session.TryAcceptSlotFileInspection(
            started.Lease!,
            WithRevision(cached, started.Snapshot!.AuthoringRevision)).Succeeded);
    }

    private static void AcceptCachedGeneralReplaceInspection(
        AuthoringSessionState session,
        string icId,
        string number,
        long capacity,
        GeneralMappingDraftState draft,
        string mappingId,
        string sourcePath)
    {
        Assert.True(session.TryGetCachedGeneralSelectedFileInspection(
            mappingId, sourcePath, out GeneralSelectedFileInspection? cached));
        AuthoringSlotInspectionStartResult started =
            WorkbenchCompositionService.BeginGeneralReplaceSelectedFileInspection(
                session,
                icId,
                number,
                capacity,
                draft,
                mappingId,
                cached.FileStamp.AcceptedLength);
        Assert.True(started.Succeeded, started.Issue?.Message + DescribeGeneralSession(
            session, draft, mappingId));
        Assert.True(session.TryAcceptSlotFileInspection(
            started.Lease!,
            WithRevision(cached, started.Snapshot!.AuthoringRevision)).Succeeded);
    }

    private static GeneralSelectedFileInspection WithRevision(
        GeneralSelectedFileInspection inspection,
        AuthoringRevision revision)
    {
        return new GeneralSelectedFileInspection(
            inspection.DefinitionId,
            revision,
            inspection.SelectedPathHint,
            inspection.FileStamp,
            inspection.DisplayNameHint,
            inspection.LastWriteTimeUtcHint);
    }

    private static string DescribeGeneralSession(
        AuthoringSessionState session,
        GeneralMappingDraftState draft,
        string mappingId)
    {
        ActiveSessionSnapshot? snapshot = session.CurrentSnapshot;
        GeneralMappingDraftState? current = snapshot?.DraftState switch
        {
            GeneralMergeDraftState merge => merge.Mappings,
            GeneralMappingDraftState replace => replace,
            _ => null,
        };
        string bindings = snapshot?.ExactCapability is null
            ? "none"
            : string.Join(",", snapshot.ExactCapability.CompiledComposition.V2Details
                .InputContract.SpaceBindings.Select(binding =>
                    $"{binding.SlotId}->{binding.AddressSpaceId}"));
        string spaces = snapshot?.ExactCapability is null
            ? "none"
            : string.Join(",", snapshot.ExactCapability.CompiledComposition.Plan
                .AddressSpaces.Select(space => $"{space.AddressSpaceId}:{space.Length}"));
        return $" exact={snapshot?.ExactCapability is not null}; same={current?.HasSameCompilationInputs(draft)}; mapping={mappingId}; bindings={bindings}; spaces={spaces}";
    }

    private static void AssertIssue(WorkbenchRunResult result, string issueCode)
    {
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => StringComparer.Ordinal.Equals(
                issue.GetProperty("Code").GetString(),
                issueCode));
    }

    private static WorkbenchGeneralMergeInitializer ResolveGeneralMergeInitializer(
        string outputLength)
    {
        Assert.True(
            WorkbenchCompositionService.TryResolveGeneralMergeOutputInitializer(
                outputLength,
                outputFillByte: null,
                out WorkbenchGeneralMergeInitializer? initializer));
        return initializer;
    }

    private static void AssertReportInputHash(
        WorkbenchRunResult result,
        string originalFileName,
        string expectedSha256)
    {
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement input = Assert.Single(
            report.RootElement.GetProperty("Inputs").EnumerateArray(),
            input => StringComparer.Ordinal.Equals(
                input.GetProperty("OriginalFileName").GetString(),
                originalFileName));
        Assert.Equal(expectedSha256, input.GetProperty("Sha256").GetString());
    }
}
