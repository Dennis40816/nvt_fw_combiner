using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

/// <summary>Verifies General preparation cannot overwrite a newer accepted session.</summary>
public sealed class GeneralPreparationFreshnessTests
{
    private const string IcId = "NT-HEADLESS";
    private const string MappingId = "mapping-1";
    private const string SourcePath = @"C:\firmware\source.bin";
    private static readonly byte[] SourceBytes = [0x11, 0x12, 0x13];

    /// <summary>The bug's late progress window cannot publish 0x5 over accepted 0x4.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparationHeldAfterLastProgressCannotOverwriteNewerAcceptance(bool cancelOld)
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        GeneralAuthoringExperience experience = Experience(new Planner(), new Inspector());
        using var cancellation = new CancellationTokenSource();
        var gate = new ProgressGate(completedWork: 1, cancellation.Token);
        Task<GeneralAuthoringSessionPreparation> old = Task.Run(async () =>
            await experience.PrepareMergeSessionAsync(session, IcId, Draft(0x5), cancellation.Token, gate));
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            GeneralAuthoringSessionPreparation newest = await experience.PrepareMergeSessionAsync(
                session, IcId, Draft(0x4), CancellationToken.None);
            AssertAccepted(newest, session, 0x4);
            if (cancelOld)
            {
                cancellation.Cancel();
            }
            gate.Release.SetResult();
            if (cancelOld)
            {
                _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await old);
            }
            else
            {
                AssertSuperseded(await old);
            }
            Assert.Same(newest.AcceptedSession, session.CurrentSnapshot);
        }
        finally
        {
            _ = gate.Release.TrySetResult();
        }
    }

    /// <summary>Cancellation after the last check alone also leaves the accepted state intact.</summary>
    [Fact]
    public async Task LatestPreparationCancelledAfterLastProgressLeavesAcceptedSessionUnchanged()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        GeneralAuthoringExperience experience = Experience(new Planner(), new Inspector());
        GeneralAuthoringSessionPreparation accepted = await experience.PrepareMergeSessionAsync(
            session, IcId, Draft(0x4), CancellationToken.None);
        AssertAccepted(accepted, session, 0x4);
        using var cancellation = new CancellationTokenSource();
        var gate = new ProgressGate(completedWork: 1, cancellation.Token);
        Task<GeneralAuthoringSessionPreparation> pending = Task.Run(async () =>
            await experience.PrepareMergeSessionAsync(session, IcId, Draft(0x5), cancellation.Token, gate));
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.Same(accepted.AcceptedSession, session.CurrentSnapshot);
            cancellation.Cancel();
            gate.Release.SetResult();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
            Assert.Same(accepted.AcceptedSession, session.CurrentSnapshot);
        }
        finally
        {
            _ = gate.Release.TrySetResult();
        }
    }

    /// <summary>A direct accepted-state transition also invalidates the preparation's starting snapshot.</summary>
    [Fact]
    public async Task PreparationCannotOverwriteAcceptedDraftChangedDuringCapture()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        GeneralAuthoringExperience experience = Experience(new Planner(), new Inspector());
        AssertAccepted(await experience.PrepareMergeSessionAsync(session, IcId, Draft(0x3), CancellationToken.None),
            session, 0x3);
        var gate = new ProgressGate(completedWork: 1, CancellationToken.None);
        Task<GeneralAuthoringSessionPreparation> pending = Task.Run(async () =>
            await experience.PrepareMergeSessionAsync(session, IcId, Draft(0x5), CancellationToken.None, gate));
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            AuthoringSessionTransitionResult edited = session.SetDraft(Draft(0x4));
            Assert.True(edited.Succeeded);
            gate.Release.SetResult();
            AssertSuperseded(await pending);
            Assert.Same(edited.Snapshot, session.CurrentSnapshot);
        }
        finally
        {
            _ = gate.Release.TrySetResult();
        }
    }

    /// <summary>A queued request revokes the predecessor even before the successor can finish.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyNewestRequestAdoptsRegardlessOfCompletionOrder(bool newestFinishesFirst)
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        GeneralAuthoringExperience experience = Experience(new Planner(), new Inspector());
        GeneralAuthoringSessionPreparation accepted = await experience.PrepareMergeSessionAsync(
            session, IcId, Draft(0x3), CancellationToken.None);
        AssertAccepted(accepted, session, 0x3);
        var oldGate = new ProgressGate(completedWork: 1, CancellationToken.None);
        var newGate = new ProgressGate(completedWork: 1, CancellationToken.None);
        Task<GeneralAuthoringSessionPreparation> old = Task.Run(async () =>
            await experience.PrepareMergeSessionAsync(session, IcId, Draft(0x5), CancellationToken.None, oldGate));
        try
        {
            await oldGate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Task<GeneralAuthoringSessionPreparation> newest = Task.Run(async () =>
                await experience.PrepareMergeSessionAsync(session, IcId, Draft(0x4), CancellationToken.None, newGate));
            await newGate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.Same(accepted.AcceptedSession, session.CurrentSnapshot);
            if (newestFinishesFirst)
            {
                newGate.Release.SetResult();
                GeneralAuthoringSessionPreparation result = await newest;
                AssertAccepted(result, session, 0x4);
                oldGate.Release.SetResult();
                AssertSuperseded(await old);
                Assert.Same(result.AcceptedSession, session.CurrentSnapshot);
            }
            else
            {
                oldGate.Release.SetResult();
                AssertSuperseded(await old);
                Assert.Same(accepted.AcceptedSession, session.CurrentSnapshot);
                newGate.Release.SetResult();
                AssertAccepted(await newest, session, 0x4);
            }
        }
        finally
        {
            _ = oldGate.Release.TrySetResult();
            _ = newGate.Release.TrySetResult();
        }
    }

    /// <summary>Supersession before the first read rejects work without reading or adopting it.</summary>
    [Fact]
    public async Task PreparationSupersededBeforeReadingDoesNotReadOrAdopt()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        var inspector = new Inspector();
        GeneralAuthoringExperience experience = Experience(new Planner(), inspector);
        var gate = new ProgressGate(completedWork: 0, CancellationToken.None);
        Task<GeneralAuthoringSessionPreparation> old = Task.Run(async () =>
            await experience.PrepareMergeSessionAsync(session, IcId, Draft(0x5), CancellationToken.None, gate));
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.Equal(0, inspector.ReadCount);
            GeneralAuthoringSessionPreparation newest = await experience.PrepareMergeSessionAsync(
                session, IcId, Draft(0x4), CancellationToken.None);
            AssertAccepted(newest, session, 0x4);
            gate.Release.SetResult();
            AssertSuperseded(await old);
            Assert.Equal(1, inspector.ReadCount);
            Assert.Same(newest.AcceptedSession, session.CurrentSnapshot);
        }
        finally
        {
            _ = gate.Release.TrySetResult();
        }
    }

    /// <summary>Even after all pre-adoption checks pass, cancellation or supersession rejects the candidate.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparationRevokedDuringCandidateConstructionCannotAdopt(bool cancelOld)
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        var resources = new ResourceGate();
        GeneralAuthoringExperience oldExperience = Experience(new Planner(resources), new Inspector());
        GeneralAuthoringExperience newestExperience = Experience(new Planner(), new Inspector());
        using var cancellation = new CancellationTokenSource();
        Task<GeneralAuthoringSessionPreparation> old = Task.Run(async () =>
            await oldExperience.PrepareMergeSessionAsync(session, IcId, Draft(0x5), cancellation.Token));
        try
        {
            await resources.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            GeneralAuthoringSessionPreparation newest = await newestExperience.PrepareMergeSessionAsync(
                session, IcId, Draft(0x4), CancellationToken.None);
            AssertAccepted(newest, session, 0x4);
            if (cancelOld)
            {
                cancellation.Cancel();
            }
            resources.Release.SetResult();
            if (cancelOld)
            {
                _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await old);
            }
            else
            {
                AssertSuperseded(await old);
            }
            Assert.Same(newest.AcceptedSession, session.CurrentSnapshot);
        }
        finally
        {
            _ = resources.Release.TrySetResult();
        }
    }

    /// <summary>Invalidation revokes a candidate even when the accepted snapshot stays null.</summary>
    [Fact]
    public async Task PreparationStartedInactiveCannotAdoptAfterCanonicalInvalidation()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        var resources = new ResourceGate();
        GeneralAuthoringExperience experience = Experience(new Planner(resources), new Inspector());
        Task<GeneralAuthoringSessionPreparation> pending = Task.Run(async () =>
            await experience.PrepareMergeSessionAsync(session, IcId, Draft(0x5), CancellationToken.None));
        try
        {
            await resources.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.Null(session.CurrentSnapshot);
            session.InvalidateCanonicalPublication();
            Assert.Null(session.CurrentSnapshot);
            resources.Release.SetResult();
            AssertSuperseded(await pending);
            Assert.Null(session.CurrentSnapshot);
        }
        finally
        {
            _ = resources.Release.TrySetResult();
        }
    }

    /// <summary>A cancelled request does not prevent the next request from adopting.</summary>
    [Fact]
    public async Task NextPreparationAdoptsAfterCancelledPreparation()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        GeneralAuthoringExperience experience = Experience(new Planner(), new Inspector());
        GeneralAuthoringSessionPreparation accepted = await experience.PrepareMergeSessionAsync(
            session, IcId, Draft(0x3), CancellationToken.None);
        AssertAccepted(accepted, session, 0x3);
        using var cancellation = new CancellationTokenSource();
        var gate = new ProgressGate(completedWork: 1, CancellationToken.None);
        Task<GeneralAuthoringSessionPreparation> pending = Task.Run(async () =>
            await experience.PrepareMergeSessionAsync(session, IcId, Draft(0x5), cancellation.Token, gate));
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            cancellation.Cancel();
            gate.Release.SetResult();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
            Assert.Same(accepted.AcceptedSession, session.CurrentSnapshot);
            GeneralAuthoringSessionPreparation next = await experience.PrepareMergeSessionAsync(
                session, IcId, Draft(0x4), CancellationToken.None);
            AssertAccepted(next, session, 0x4);
        }
        finally
        {
            _ = gate.Release.TrySetResult();
        }
    }

    /// <summary>A failed capture preserves accepted state and permits the next request to adopt.</summary>
    [Fact]
    public async Task NextPreparationAdoptsAfterCaptureFailure()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        GeneralAuthoringExperience experience = Experience(new Planner(), new Inspector());
        GeneralAuthoringSessionPreparation accepted = await experience.PrepareMergeSessionAsync(
            session, IcId, Draft(0x3), CancellationToken.None);
        AssertAccepted(accepted, session, 0x3);
        GeneralAuthoringExperience failingExperience = Experience(new Planner(), new Inspector(failCapture: true));
        GeneralAuthoringSessionPreparation failed = await failingExperience.PrepareMergeSessionAsync(
            session, IcId, Draft(0x5), CancellationToken.None);
        Assert.False(failed.Succeeded);
        Assert.Null(failed.AcceptedSession);
        Assert.Null(failed.Readiness);
        Assert.Equal(GeneralSelectedFileInspectionIssueCodes.InspectionFailed, Assert.Single(failed.Issues).Code);
        Assert.Same(accepted.AcceptedSession, session.CurrentSnapshot);
        GeneralAuthoringSessionPreparation next = await experience.PrepareMergeSessionAsync(
            session, IcId, Draft(0x4), CancellationToken.None);
        AssertAccepted(next, session, 0x4);
    }

    /// <summary>A failure after candidate transitions preserves the entire accepted snapshot and permits retry.</summary>
    [Fact]
    public async Task LateMappingCompilationFailureLeavesAcceptedSessionUnchangedAndAllowsRetry()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        GeneralAuthoringExperience experience = Experience(new Planner(), new Inspector());
        GeneralAuthoringSessionPreparation accepted = await experience.PrepareMergeSessionAsync(
            session, IcId, Draft(0x3), CancellationToken.None);
        AssertAccepted(accepted, session, 0x3);
        GeneralAuthoringExperience failing = Experience(new Planner(mismatchMapping: true), new Inspector());

        GeneralAuthoringSessionPreparation result = await failing.PrepareMergeSessionAsync(
            session, IcId, Draft(0x5), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.AcceptedSession);
        Assert.Null(result.Readiness);
        Assert.Equal("The selected General Merge mapping compilation became stale.", Assert.Single(result.Issues).Message);
        Assert.Same(accepted.AcceptedSession, session.CurrentSnapshot);
        AssertAccepted(await experience.PrepareMergeSessionAsync(session, IcId, Draft(0x4), CancellationToken.None),
            session, 0x4);
    }

    /// <summary>One adoption consumes the lease; candidate publications cannot address the accepted session.</summary>
    [Fact]
    public async Task CandidatePublicationIsIsolatedAndAdoptionConsumesRequestLease()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        GeneralAuthoringExperience experience = Experience(new Planner(), new Inspector());
        GeneralAuthoringSessionPreparation accepted = await experience.PrepareMergeSessionAsync(
            session, IcId, Draft(0x4), CancellationToken.None);
        AssertAccepted(accepted, session, 0x4);
        (AuthoringSessionState.GeneralPreparationLease lease, AuthoringSessionState candidate) = session.BeginGeneralPreparation();
        AuthoringPublicationLease publication = candidate.CapturePublicationLease(
            AuthoringDerivedResultKind.Validation, candidate.CurrentSnapshot!.CompilationFingerprint);
        var value = new AuthoringDerivedPublication(AuthoringDerivedResultKind.Validation,
            "candidate-validation", candidate.CurrentSnapshot.CompilationFingerprint);

        AuthoringPublicationResult refused = session.TryPublish(publication, value);
        Assert.False(refused.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.StalePublication, refused.Issue!.Code);
        Assert.Same(accepted.AcceptedSession, session.CurrentSnapshot);
        Assert.True(candidate.TryPublish(publication, value).Succeeded);
        AuthoringSessionTransitionResult adopted = session.TryAdoptGeneralPreparation(lease, candidate, CancellationToken.None);
        Assert.True(adopted.Succeeded);
        Assert.Same(candidate.CurrentSnapshot, session.CurrentSnapshot);
        AuthoringSessionTransitionResult repeated = session.TryAdoptGeneralPreparation(lease, candidate, CancellationToken.None);
        Assert.False(repeated.Succeeded);
        Assert.Equal(AuthoringSessionIssueCodes.StaleInspection, repeated.Issue!.Code);
        Assert.Same(adopted.Snapshot, session.CurrentSnapshot);
    }

    /// <summary>With three requests entering A-B-C, only C adopts in either completion order.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyThirdPreparationAdoptsInForwardOrReverseCompletionOrder(bool newestFinishesFirst)
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        GeneralAuthoringExperience experience = Experience(new Planner(), new Inspector());
        ProgressGate[] gates =
        [
            new(completedWork: 1, CancellationToken.None),
            new(completedWork: 1, CancellationToken.None),
            new(completedWork: 1, CancellationToken.None),
        ];
        List<Task<GeneralAuthoringSessionPreparation>> preparations = [];
        try
        {
            for (int index = 0; index < gates.Length; index++)
            {
                ProgressGate gate = gates[index];
                long target = 0x3 + index;
                preparations.Add(Task.Run(async () =>
                    await experience.PrepareMergeSessionAsync(session, IcId, Draft(target), CancellationToken.None, gate)));
                await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
                Assert.Null(session.CurrentSnapshot);
            }

            int[] completionOrder = newestFinishesFirst ? [2, 1, 0] : [0, 1, 2];
            ActiveSessionSnapshot? accepted = null;
            foreach (int index in completionOrder)
            {
                gates[index].Release.SetResult();
                GeneralAuthoringSessionPreparation result = await preparations[index];
                if (index == 2)
                {
                    AssertAccepted(result, session, 0x5);
                    accepted = result.AcceptedSession;
                }
                else
                {
                    AssertSuperseded(result);
                }
                Assert.Same(accepted, session.CurrentSnapshot);
            }
        }
        finally
        {
            foreach (ProgressGate gate in gates)
            {
                _ = gate.Release.TrySetResult();
            }
        }
    }

    /// <summary>A null lease cannot match the cleared ownership field after adoption.</summary>
    [Fact]
    public async Task NullPreparationLeaseIsRejectedAfterAdoption()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        GeneralAuthoringExperience experience = Experience(new Planner(), new Inspector());
        GeneralAuthoringSessionPreparation accepted = await experience.PrepareMergeSessionAsync(
            session, IcId, Draft(0x4), CancellationToken.None);
        AssertAccepted(accepted, session, 0x4);
        _ = Assert.Throws<ArgumentNullException>("lease", () =>
            session.CheckGeneralPreparation(null!, CancellationToken.None));
        _ = Assert.Throws<ArgumentNullException>("lease", () =>
            session.TryAdoptGeneralPreparation(null!, session, CancellationToken.None));
        Assert.Same(accepted.AcceptedSession, session.CurrentSnapshot);
    }

    /// <summary>Entry cancellation is enforced even without any selected file rows.</summary>
    [Fact]
    public async Task AlreadyCancelledPreparationWithoutFileRowsThrows()
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        var planner = new Planner();
        var inspector = new Inspector();
        GeneralAuthoringExperience experience = Experience(planner, inspector);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var draft = new GeneralMergeDraftState(new GeneralMergeOutputInitializer(16, 0xA5), new GeneralMappingDraftState([]));
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await experience.PrepareMergeSessionAsync(session, IcId, draft, cancellation.Token));
        Assert.Null(session.CurrentSnapshot);
        Assert.Null(planner.LastPlan);
        Assert.Equal(0, inspector.ReadCount);
    }

    /// <summary>Cancellation or supersession takes precedence over a completed failed capture.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevokedPreparationDoesNotReportCaptureFailure(bool cancelOld)
    {
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        GeneralAuthoringExperience failingExperience = Experience(new Planner(), new Inspector(failCapture: true));
        using var cancellation = new CancellationTokenSource();
        var gate = new ProgressGate(completedWork: 1, CancellationToken.None);
        Task<GeneralAuthoringSessionPreparation> pending = Task.Run(async () =>
            await failingExperience.PrepareMergeSessionAsync(session, IcId, Draft(0x5), cancellation.Token, gate));
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            ActiveSessionSnapshot? accepted = null;
            if (cancelOld)
            {
                cancellation.Cancel();
            }
            else
            {
                GeneralAuthoringExperience experience = Experience(new Planner(), new Inspector());
                GeneralAuthoringSessionPreparation newest = await experience.PrepareMergeSessionAsync(
                    session, IcId, Draft(0x4), CancellationToken.None);
                AssertAccepted(newest, session, 0x4);
                accepted = newest.AcceptedSession;
            }
            gate.Release.SetResult();
            if (cancelOld)
            {
                _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
            }
            else
            {
                AssertSuperseded(await pending);
            }
            Assert.Same(accepted, session.CurrentSnapshot);
        }
        finally
        {
            _ = gate.Release.TrySetResult();
        }
    }

    /// <summary>Newest preparation preserves the original transitions, revisions and readiness.</summary>
    [Fact]
    public async Task NewestPreparationPreservesAcceptedDraftAndReadiness()
    {
        var planner = new Planner();
        GeneralAuthoringExperience experience = Experience(planner, new Inspector());
        var session = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        var expected = new AuthoringSessionState(ExperienceIds.GeneralMerge);
        long[] targets = [0x4, 0x4, 0x5];
        foreach (long target in targets)
        {
            GeneralMergeDraftState draft = Draft(target);
            GeneralAuthoringSessionPreparation result = await experience.PrepareMergeSessionAsync(
                session, IcId, draft, CancellationToken.None);
            AssertAccepted(result, session, target);
            GeneralMergeAuthoringPlan plan = planner.LastPlan!;
            Assert.True(expected.Activate(AuthoringCapabilityCatalogSnapshot.FromResolvedCapability(
                plan.Capability, new Dictionary<string, long> { [MappingId] = SourceBytes.Length })).Succeeded);
            Assert.True(expected.SetDraft(draft).Succeeded);
            AuthoringSlotInspectionStartResult started = expected.BeginSlotFileInspection(MappingId, SourcePath);
            Assert.True(expected.TryAcceptSlotFileInspection(started.Lease!, new GeneralSelectedFileInspection(
                MappingId, started.Snapshot!.AuthoringRevision, SourcePath, FileStamp.FromBytes(SourceBytes),
                acceptedBytes: SourceBytes)).Succeeded);
            ActiveSessionSnapshot current = expected.CurrentSnapshot!;
            AuthoringPublicationLease publication = expected.CapturePublicationLease(
                AuthoringDerivedResultKind.Validation, current.CompilationFingerprint);
            Assert.True(expected.TryPublish(publication, new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Validation, $"general-merge-readiness:{current.AuthoringRevision.Value}",
                current.CompilationFingerprint)).Succeeded);
            AssertSnapshotsEqual(expected.CurrentSnapshot!, result.AcceptedSession!);
            CapabilityAdmissionSnapshot admission = CapabilityAdmissionSnapshot.FromResolvedCapability(
                plan.Capability, current.AuthoringRevision);
            var runtime = new RuntimeDependencyReadinessSnapshot(admission.RouteId, admission.CapabilityFingerprint,
                admission.CompilationFingerprint, admission.ResolutionToken, admission.AuthoringRevision,
                1, DateTimeOffset.UnixEpoch, []);
            CapabilityActionReadinessSnapshot readiness = CapabilityActionReadinessResolver.Resolve(admission,
                [new CapabilityChildReadiness(MappingId, ResolvedChildReadiness.Ready)], runtime, 1);
            Assert.Equivalent(readiness, result.Readiness, strict: true);
            Assert.Same(plan.Capability.GeneralExecutionPlan!.Admission, result.Admission);
            Assert.Null(result.DiagnosticPreviewReport);
        }
    }

    private static void AssertSnapshotsEqual(ActiveSessionSnapshot expected, ActiveSessionSnapshot actual)
    {
        Assert.Equal(expected.WorkflowId, actual.WorkflowId);
        Assert.Equal(expected.ResolutionToken, actual.ResolutionToken);
        Assert.Equal(expected.AuthoringRevision, actual.AuthoringRevision);
        Assert.Equal(expected.SelectedRouteId, actual.SelectedRouteId);
        Assert.Equal(expected.CapabilityFingerprint, actual.CapabilityFingerprint);
        Assert.Equal(expected.CompilationFingerprint, actual.CompilationFingerprint);
        Assert.Same(expected.ExactCapability, actual.ExactCapability);
        Assert.Equal(expected.ExecutionAdmitted, actual.ExecutionAdmitted);
        Assert.Equal(expected.SelectedIc, actual.SelectedIc);
        Assert.Equal(expected.SelectedIcCount, actual.SelectedIcCount);
        Assert.Equal(expected.SelectedMapVariant, actual.SelectedMapVariant);
        Assert.Equal(expected.IcChoices, actual.IcChoices);
        Assert.Equal(expected.IcCountChoices, actual.IcCountChoices);
        Assert.True(expected.DraftState!.HasSameValue(actual.DraftState!));
        Assert.Equal(expected.DraftCapabilityFingerprint, actual.DraftCapabilityFingerprint);
        Assert.Equal(expected.DerivedPublications, actual.DerivedPublications);
        Assert.Equal(expected.InputSlotStatuses, actual.InputSlotStatuses);
        Assert.Equal(expected.InputSelectionReadiness, actual.InputSelectionReadiness);
        Assert.Equal(expected.MetadataInspection, actual.MetadataInspection);
        Assert.Equal(expected.HasCurrentInputInspection, actual.HasCurrentInputInspection);
        AuthoringSlotState left = Assert.Single(expected.Slots);
        AuthoringSlotState right = Assert.Single(actual.Slots);
        Assert.Equal(left.DefinitionId, right.DefinitionId);
        Assert.Equal(left.SelectedPath, right.SelectedPath);
        Assert.Equal(left.FileStamp, right.FileStamp);
        Assert.Equal(left.Lifecycle, right.Lifecycle);
        Assert.Equal(left.BlockingIssue, right.BlockingIssue);
        Assert.Equal(left.AcceptedBytes!.Value.ToArray(), right.AcceptedBytes!.Value.ToArray());
    }

    private static void AssertAccepted(GeneralAuthoringSessionPreparation result, AuthoringSessionState session, long target)
    {
        Assert.True(result.Succeeded, string.Join("; ", result.Issues.Select(static issue => issue.Message)));
        Assert.Same(result.AcceptedSession, session.CurrentSnapshot);
        GeneralMappingDraftRow row = Assert.Single(Assert.IsType<GeneralMergeDraftState>(result.AcceptedSession!.DraftState).Mappings.Rows);
        Assert.Equal(target, row.TargetRange.Start);
        Assert.Equal(target, Assert.Single(result.AcceptedSession.ExactCapability!.CompiledComposition.Plan.OrderedOperations).TargetRange.Start);
        Assert.Equal(FileStamp.FromBytes(SourceBytes), row.Source.AcceptedFileStamp);
        Assert.Equal(SourceBytes, Assert.Single(result.AcceptedSession.Slots).AcceptedBytes!.Value.ToArray());
        Assert.True(result.Readiness!.Build.IsAvailable);
        Assert.True(result.Readiness.Preview.IsAvailable);
        Assert.Equal(result.AcceptedSession.AuthoringRevision, result.Readiness.AuthoringRevision);
    }

    private static void AssertSuperseded(GeneralAuthoringSessionPreparation result)
    {
        Assert.False(result.Succeeded);
        Assert.Null(result.AcceptedSession);
        Assert.Null(result.Readiness);
        Assert.Equal(AuthoringSessionIssueCodes.StaleInspection, Assert.Single(result.Issues).Code);
    }

    private static GeneralMergeDraftState Draft(long target)
    {
        return new(new GeneralMergeOutputInitializer(16, 0xA5), new GeneralMappingDraftState(
        [
            new GeneralMappingDraftRow(MappingId, ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File(SourcePath), new ByteRange(0, SourceBytes.Length),
                CompositionAddressSpaceIds.OutputImage, new ByteRange(target, SourceBytes.Length),
                OverlapPolicy.Reject, 1, "Synthetic General merge."),
        ]));
    }

    private static GeneralAuthoringExperience Experience(Planner planner, Inspector inspector)
    {
        return new(planner, inspector, new UnusedRuntimeLeases(), new Clock());
    }

    private sealed class ProgressGate(int completedWork, CancellationToken cancellationToken)
        : IProgress<AuthoringInspectionProgress>
    {
        internal TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Report(AuthoringInspectionProgress value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.CompletedWork == completedWork)
            {
                Reached.SetResult();
                Release.Task.GetAwaiter().GetResult();
            }
        }
    }

    private sealed class Inspector(bool failCapture = false) : ISelectedFileContentInspector
    {
        private int _readCount;
        internal int ReadCount => Volatile.Read(ref _readCount);

        public ValueTask<SelectedFileContentInspection> InspectAsync(string selectedPath, long maximumBytes,
            CancellationToken cancellationToken,
            SelectedFileContentInspectionMode mode = SelectedFileContentInspectionMode.CaptureBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(SelectedFileContentInspectionMode.CaptureBytes, mode);
            _ = Interlocked.Increment(ref _readCount);
            return failCapture
                ? throw new IOException("Synthetic capture failure.")
                : ValueTask.FromResult(new SelectedFileContentInspection(FileStamp.FromBytes(SourceBytes),
                    acceptedBytes: SourceBytes));
        }
    }

    private sealed class ResourceGate : IReadOnlyList<GeneralInputResource>
    {
        internal TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Count => 1;
        public GeneralInputResource this[int index] => new(MappingId, SourceBytes.Length);

        public IEnumerator<GeneralInputResource> GetEnumerator()
        {
            Reached.SetResult();
            Release.Task.GetAwaiter().GetResult();
            yield return this[0];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private sealed class Planner(ResourceGate? resources = null, bool mismatchMapping = false) : IGeneralAuthoringPlanner
    {
        internal GeneralMergeAuthoringPlan? LastPlan { get; private set; }

        public GeneralMergeAuthoringPlanResult PlanGeneralMerge(string icId, GeneralMergeDraftState draft,
            IReadOnlyDictionary<string, long> observedFileLengths, ResolvedCapability? retainedCapability)
        {
            GeneralMappingDraftRow row = Assert.Single(draft.Mappings.Rows);
            var admission = new GeneralAuthoringAdmissionResult(draft.Mappings, "synthetic-parent", null,
                new GeneralResourceLimits(1, 16, 16, 16), [new GeneralInputResource(MappingId, observedFileLengths[MappingId])],
                [new GeneralOccupancySegment(MappingId, row.Source.Kind, row.TargetAddressSpaceId, row.TargetRange)], []);
            ResolvedCapability capability = retainedCapability ?? Capability(draft, admission);
            GeneralMappingDraftState mappingDraft = mismatchMapping ? Draft(row.TargetRange.Start + 1).Mappings : draft.Mappings;
            var plan = new GeneralMergeAuthoringPlan(mappingDraft, capability, resources ?? admission.InputResources);
            LastPlan = plan;
            return new(plan, capability.GeneralExecutionPlan!.Admission, []);
        }

        private static ResolvedCapability Capability(GeneralMergeDraftState draft, GeneralAuthoringAdmissionResult admission)
        {
            var identity = new CapabilityRouteIdentity(IcId, ExperienceIds.GeneralMerge, "none", "general-test-map");
            GeneralMappingDraftRow row = Assert.Single(draft.Mappings.Rows);
            var plan = new CompositionPlan(ImageInitialization.Blank(CompositionAddressSpaceIds.OutputImage, 16, 0xA5),
                [new AddressSpace(MappingId, SourceBytes.Length, AddressSpaceMutability.Immutable),
                 new AddressSpace(CompositionAddressSpaceIds.OutputImage, 16, AddressSpaceMutability.Mutable)],
                [CompositionOperation.CopyRange(MappingId, 100, MappingId, row.SourceRange, row.TargetAddressSpaceId,
                    row.TargetRange, OverlapPolicy.Reject, row.Reason)]);
            CompiledComposition compiled = CompiledCompositionTestFactory.Create(plan,
                new TestCompiledCompositionIdentity("synthetic-general", "1.0.0", IcId, ExperienceIds.GeneralMerge,
                    ExperienceIds.GeneralMerge, CompositionKind.Merge), "general.bin", mapId: identity.MapVariant);
            string fingerprint = new('a', 64);
            var token = new ResolutionToken("synthetic-general-publication");
            return new ResolvedCapability(identity, fingerprint, compiled,
                Decision(CapabilityAuthoringAvailability.Available), Decision(CapabilityPublicationStatus.TestOnly),
                Decision(CapabilityEvidenceStatus.SyntheticOracle), MetadataPlanDefinition.Empty.Resolve(token), token)
                .BindGeneralExecutionPlan(new AcceptedGeneralExecutionPlan(admission,
                    [new InputArtifactBinding(MappingId, MappingId, SourcePath)]));

            PinnedCapabilityDecision<T> Decision<T>(T value) where T : struct, Enum
            {
                return new(typeof(T).Name, identity.RouteId, fingerprint, value, "synthetic-general");
            }
        }

        public bool CanPlanGeneralReplace(string icId)
        {
            return false;
        }

        public GeneralAuthoringAdmissionResult GetGeneralMergeAdmission(string icId, GeneralMergeDraftState draft)
        {
            throw new NotSupportedException();
        }

        public GeneralAuthoringAdmissionResult? GetGeneralReplaceAdmission(string icId, long referenceCapacity,
            GeneralMappingDraftState mappingDraft)
        {
            throw new NotSupportedException();
        }

        public GeneralMergeOutputInitializer GetGeneralMergeDefaultOutputInitializer(string icId)
        {
            throw new NotSupportedException();
        }

        public GeneralReplaceAuthoringPlanResult PlanGeneralReplace(string icId, string number, GeneralMappingDraftState draft,
            ReadOnlyMemory<byte> referenceBytes, IReadOnlyDictionary<string, long> observedFileLengths,
            ResolvedCapability? retainedCapability)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class UnusedRuntimeLeases : IRuntimeDependencyReadinessLeaseProvider
    {
        public RuntimeDependencyReadinessLease AcquireCurrent()
        {
            throw new NotSupportedException();
        }
    }

    private sealed class Clock : ISystemClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    }
}
