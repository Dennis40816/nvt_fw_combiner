using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class BuildOutcomeTests
{
    /// <summary>Matching workflow, IC and authoring revision do not identify a page instance.</summary>
    [Fact]
    public async Task WorkflowRunOwnersRemainIndependentAcrossIdenticalPageInstances()
    {
        MainWindowViewModel first = PresentationTestHost.CreateViewModel();
        MainWindowViewModel second = PresentationTestHost.CreateViewModel();
        foreach (MainWindowViewModel shell in new[] { first, second })
        {
            shell.ShowMergeCommand.Execute(null);
            shell.WorkflowSession.SelectedIc = "NT51926";
        }
        CompositionRunContext a = first.Merge.CaptureRunContext(ExperienceIds.StandardMerge);
        CompositionRunContext b = second.Merge.CaptureRunContext(ExperienceIds.StandardMerge);
        Assert.NotNull(a.AcceptedSession);
        Assert.NotNull(b.AcceptedSession);
        Assert.Equal(a.Mode, b.Mode);
        Assert.Equal(a.Ic, b.Ic);
        Assert.Equal(a.AcceptedSession.AuthoringRevision, b.AcceptedSession.AuthoringRevision);
        Assert.NotSame(a.Owner, b.Owner);
        UiRunResultViewModel untouched = b.Owner.LastRunResult;

        _ = await first.RunSession.RunCompositionAsync(a, false,
            (_, _) => ValueTask.FromResult(CreateRunResult(true, null)), (_, _) => { });
        UiRunResultViewModel completed = a.Owner.LastRunResult;
        Assert.True(completed.Succeeded);
        Assert.Same(untouched, b.Owner.LastRunResult);

        _ = await first.RunSession.RunCompositionAsync(b, false,
            (_, _) => throw new InvalidOperationException("second instance failed"), (_, _) => { });

        Assert.Same(completed, a.Owner.LastRunResult);
        Assert.Equal("second instance failed", b.Owner.LastRunResult.Detail);
        Assert.Same(completed, first.RunSession.LastRunResult);
        Assert.Same(b.Owner.LastRunResult, second.RunSession.LastRunResult);
    }

    /// <summary>Every terminal branch publishes to the captured owner after page navigation.</summary>
    [Theory]
    [InlineData("success", "Build succeeded")]
    [InlineData("partial", "Build partially delivered")]
    [InlineData("blocked", "Build blocked")]
    [InlineData("exception", "Build failed")]
    [InlineData("refusal", "Build blocked")]
    public async Task WorkflowRunTerminalResultFollowsCapturedOwner(string outcome, string title)
    {
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        shell.ShowMergeCommand.Execute(null);
        shell.WorkflowSession.SelectedIc = "NT51926";
        CompositionRunContext context = shell.Merge.CaptureRunContext(ExperienceIds.StandardMerge, build: true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task run = shell.RunSession.RunCompositionAsync(context, true, async (_, cancellationToken) =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(cancellationToken);
            return outcome switch
            {
                "exception" => throw new IOException("captured failure"),
                "refusal" => throw new CompositionPreRunRefusalException([new CompositionIssue("refused", "captured refusal")]),
                "partial" => WithDelivery(CreateRunResult(true, "primary.bin"), [], false, "second delivery failed"),
                "blocked" => CreateRunResult(false, null, [new CompositionIssue("blocked", "captured blocker")]),
                _ => CreateRunResult(true, "primary.bin"),
            };
        }, (action, message) => shell.Reports.LoadRunErrorReport(
            action, "test-profile", context.Ic, context.Number, message, new Dictionary<string, string>()));
        try
        {
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            shell.ShowReplaceCommand.Execute(null);
            var visible = new UiRunResultViewModel("Other page", "retained", "No output", false);
            shell.RunSession.PublishRunResult(shell.Replace.RunState, visible);
            release.SetResult();
            await run;

            Assert.Equal(ShellPage.Replace, shell.SelectedPage);
            Assert.Same(visible, shell.RunSession.LastRunResult);
            Assert.Equal(title, context.Owner.LastRunResult.Title);
            Assert.Same(context, context.Owner.CompletedContext);
            Assert.Equal(outcome == "success", context.Owner.LastRunResult.Succeeded);
            Assert.Equal(outcome != "refusal", shell.Reports.HasLoadedReport);
            Assert.Equal(outcome == "success", shell.BuildResult.IsOpen);
            Assert.False(shell.RunSession.IsRunInProgress);
            shell.BuildResult.Close();
            shell.Reports.CloseReportCommand.Execute(null);
            shell.ShowMergeCommand.Execute(null);
            Assert.Same(context.Owner.LastRunResult, shell.RunSession.LastRunResult);
            UiRunResultViewModel completed = context.Owner.LastRunResult;
            shell.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
            Assert.NotSame(completed, shell.RunSession.LastRunResult);
            shell.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
            Assert.Same(completed, shell.RunSession.LastRunResult);
        }
        finally
        {
            _ = release.TrySetResult();
            shell.RunSession.CancelActiveRun();
            await run;
        }
    }

    /// <summary>One gate rejects a second request without replacing progress; cancellation requires owner and attempt.</summary>
    [Fact]
    public async Task WorkflowRunGateAndCancellationRejectOtherOwnersAndOldAttempts()
    {
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        CompositionRunContext first = shell.Merge.CaptureRunContext(ExperienceIds.StandardMerge);
        CompositionRunContext other = shell.Merge.CaptureRunContext(ExperienceIds.GeneralMerge);
        Guid? previousAttempt = null;
        for (int index = 0; index < 2; index++)
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task run = shell.RunSession.RunCompositionAsync(first, false, async (_, token) =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("unreachable");
            }, (_, _) => { });
            try
            {
                await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
                Guid attempt = Assert.IsType<Guid>(first.Owner.ActiveAttemptId);
                UiRunResultViewModel retained = first.Owner.LastRunResult;
                UiRunResultViewModel otherRetained = other.Owner.LastRunResult;
                int rejectedWork = 0;
                UiRunResultViewModel? rejected = await shell.RunSession.RunCompositionAsync(other, true,
                    (_, _) => { rejectedWork++; return ValueTask.FromResult(CreateRunResult(true, null)); }, (_, _) => { });
                Assert.Null(rejected);
                Assert.Equal(0, rejectedWork);
                Assert.Same(retained, first.Owner.LastRunResult);
                Assert.Same(otherRetained, other.Owner.LastRunResult);
                Assert.Equal(attempt, first.Owner.ActiveAttemptId);
                Assert.Equal(first.Mode, shell.RunSession.ActiveRunMode);
                Assert.False(shell.RunSession.CancelRun(other.Owner, attempt));
                if (previousAttempt is { } stale)
                {
                    Assert.NotEqual(stale, attempt);
                    Assert.False(shell.RunSession.CancelRun(first.Owner, stale));
                }
                Assert.False(run.IsCompleted);
                Assert.True(shell.RunSession.CancelRun(first.Owner, attempt));
                await run;
                Assert.Null(first.Owner.ActiveAttemptId);
                Assert.False(shell.RunSession.IsRunInProgress);
                previousAttempt = attempt;
            }
            finally
            {
                shell.RunSession.CancelActiveRun();
                await run;
            }
        }
    }

    /// <summary>A failing start observer cannot strand the serialized gate or its page attempt.</summary>
    [Fact]
    public async Task WorkflowRunStartObserverFailureReleasesTheOwner()
    {
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        CompositionRunContext context = shell.Merge.CaptureRunContext(ExperienceIds.StandardMerge);
        static void Fail(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(CompositionRunPresentationViewModel.RunProgressAccessibleLabel))
            {
                throw new InvalidOperationException("start observer failed");
            }
        }
        shell.RunSession.PropertyChanged += Fail;
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => shell.RunSession.RunCompositionAsync(
            context, false, (_, _) => ValueTask.FromResult(CreateRunResult(true, null)), (_, _) => { }));
        Assert.False(shell.RunSession.IsRunInProgress);
        Assert.Null(context.Owner.ActiveAttemptId);
        shell.RunSession.PropertyChanged -= Fail;
        UiRunResultViewModel? next = await shell.RunSession.RunCompositionAsync(
            context, false, (_, _) => ValueTask.FromResult(CreateRunResult(true, null)), (_, _) => { });
        Assert.True(next?.Succeeded);
    }

    /// <summary>Clearing a hidden page resets all of its modes and preserves the visible page.</summary>
    [Fact]
    public void WorkflowRunHiddenPageResetUsesExplicitPageAndModes()
    {
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        shell.ShowReplaceCommand.Execute(null);
        var visible = new UiRunResultViewModel("Visible result", "retained", "output.bin", true);
        shell.RunSession.PublishRunResult(shell.Replace.RunState, visible);
        foreach (WorkflowRunState owner in shell.Merge.RunStates)
        {
            shell.RunSession.PublishRunResult(owner, new UiRunResultViewModel("Old merge", "old", "old.bin", true));
        }

        shell.WorkflowSession.ClearSelectedInputs(ShellPage.Merge);

        Assert.All(shell.Merge.RunStates, owner => Assert.Equal("Context changed", owner.LastRunResult.Title));
        Assert.Same(visible, shell.RunSession.LastRunResult);
        CompositionRunContext captured = shell.Merge.CaptureRunContext(ExperienceIds.AbMerge) with { Ic = "captured IC", Number = "captured count" };
        shell.RunSession.ResetRunResultForContextChange(captured);
        Assert.StartsWith("captured IC / captured count:", captured.Owner.LastRunResult.Detail, StringComparison.Ordinal);
        Assert.Same(visible, shell.RunSession.LastRunResult);
    }

    /// <summary>Language changes initialize all independent owners, including modes never shown.</summary>
    [Fact]
    public void WorkflowRunInitialLocalizationReachesEveryOwner()
    {
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        shell.SelectedLanguage = "Traditional Chinese";
        Assert.All(shell.Merge.RunStates.Concat(shell.Replace.RunStates), owner =>
            Assert.Equal(shell.Text.InitialRunTitle, owner.LastRunResult.Title));
        shell.SelectedLanguage = "English";
        Assert.All(shell.Merge.RunStates.Concat(shell.Replace.RunStates), owner =>
            Assert.Equal(shell.Text.InitialRunTitle, owner.LastRunResult.Title));
    }

    /// <summary>Readiness and asynchronous diagnostic previews retain an explicit hidden-mode owner.</summary>
    [Fact]
    public async Task WorkflowRunReadinessAndDiagnosticUseCapturedOwner()
    {
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        shell.ShowMergeCommand.Execute(null);
        var visible = new UiRunResultViewModel("Visible merge", "retained", "old.bin", true);
        shell.RunSession.PublishRunResult(shell.Merge.RunState, visible);
        CompositionRunContext context = shell.Replace.CaptureRunContext(ExperienceIds.GeneralReplace);
        var admission = new CapabilityAdmissionSnapshot("route", new string('a', 64), new string('b', 64),
            new ResolutionToken("catalog:1"), new AuthoringRevision(0), CapabilityAuthoringAvailability.Unavailable,
            false, CapabilityEvidenceStatus.Missing, CapabilityPublicationStatus.Candidate);
        var runtime = new RuntimeDependencyReadinessSnapshot("route", admission.CapabilityFingerprint,
            admission.CompilationFingerprint, admission.ResolutionToken, admission.AuthoringRevision,
            1, DateTimeOffset.UnixEpoch, []);
        CapabilityActionReadinessSnapshot readiness = CapabilityActionReadinessResolver.Resolve(admission, [], runtime, 1);
        shell.RunSession.ShowActionReadiness(context, readiness, true);
        Assert.Equal("Build blocked", context.Owner.LastRunResult.Title);
        Assert.Same(visible, shell.RunSession.LastRunResult);
        Assert.False(shell.Reports.HasLoadedReport);

        CompositionRunReport source = CreateRunResult(false, null).Report;
        var diagnostic = new GeneralReplaceDiagnosticPreviewSummary(null, readiness.Build.PrimaryBlocker!, []);
        var report = new CompositionRunReport(source.RunId, source.ProfileId, source.ProfileVersion,
            source.IcId, ExperienceIds.GeneralReplace, ExperienceIds.GeneralReplace, CompositionKind.Replace,
            source.StartedAtUtc, source.CompletedAtUtc, [], [], [], [], source.Output, diagnosticPreview: diagnostic);
        Task projection = shell.RunSession.ShowDiagnosticPreviewAsync(context, report);
        shell.RunSession.ResetRunResultForContextChange(context);
        await projection;
        Assert.Equal("Preview blocked", context.Owner.LastRunResult.Title);
        Assert.Equal(diagnostic.Message, context.Owner.LastRunResult.Detail);
        Assert.Same(context, context.Owner.CompletedContext);
        Assert.Same(visible, shell.RunSession.LastRunResult);
        Assert.True(shell.Reports.HasLoadedReport);
        _ = Assert.Single(shell.Reports.ReportHistoryEntries);
    }
}

public sealed partial class ReportProjectionConcurrencyTests
{
    /// <summary>A delayed direct projection can update its old owner and shared history without changing the displayed owner.</summary>
    [Fact]
    public async Task WorkflowRunDelayedProjectionRetainsCapturedOwner()
    {
        CompositionRunResult source = await CreateDpReplaceInspectionResultAsync(TestHost);
        CompositionRunResult result = WithReport(source, CreateLargeDifferenceReport(
            source.Report, count: 10_000, sectionCount: 40, runId: "captured-owner-projection"));
        MainWindowViewModel shell = PresentationTestHost.CreateViewModel();
        shell.ShowReplaceCommand.Execute(null);
        CompositionRunContext context = shell.Replace.CaptureRunContext(shell.Replace.SelectedReplaceMode);
        Task projecting = shell.RunSession.ProjectAndApplyRunResultAsync(
            context, result, false, TestContext.Current.CancellationToken);
        shell.ShowMergeCommand.Execute(null);
        var visible = new UiRunResultViewModel("Visible merge", "retained", "old.bin", true);
        shell.RunSession.PublishRunResult(shell.Merge.RunState, visible);
        await projecting;

        Assert.True(context.Owner.LastRunResult.Succeeded);
        Assert.Same(context, context.Owner.CompletedContext);
        Assert.Same(visible, shell.RunSession.LastRunResult);
        Assert.Contains("captured-owner-projection", shell.Reports.LoadedReportJson, StringComparison.Ordinal);
        _ = Assert.Single(shell.Reports.ReportHistoryEntries);
    }
}
