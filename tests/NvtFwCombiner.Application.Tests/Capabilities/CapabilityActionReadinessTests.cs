using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Metadata;

namespace NvtFwCombiner.Application.Tests.Capabilities;

/// <summary>Tests the shared headless Preview/Build readiness projection.</summary>
public sealed class CapabilityActionReadinessTests
{
    private static readonly ResolutionToken Token = new("catalog:1");

    /// <summary>Evidence and publication remain certification dimensions rather than Build switches.</summary>
    [Fact]
    public void MissingEvidenceDoesNotBlockOtherwiseReadyBuild()
    {
        CapabilityAdmissionSnapshot admission = Admission(
            evidence: CapabilityEvidenceStatus.Missing,
            publication: CapabilityPublicationStatus.Candidate);
        RuntimeDependencyReadinessSnapshot runtime = Runtime(
            RuntimeDependencyEntry.Ready("legacy-combiner", "legacy-combiner-1.13.0"));

        CapabilityActionReadinessSnapshot result = CapabilityActionReadinessResolver.Resolve(
            admission,
            [Child("base", ResolvedChildReadiness.Ready)],
            runtime,
            currentRuntimeDependencyGeneration: 1);

        Assert.True(result.Preview.IsAvailable);
        Assert.True(result.Build.IsAvailable);
        Assert.Empty(result.Build.Blockers);
    }

    /// <summary>Build returns one deterministic highest-priority typed blocker without creating a report.</summary>
    [Fact]
    public void BuildReturnsHighestPriorityTypedBlockerWithoutReportState()
    {
        CapabilityActionReadinessSnapshot result = CapabilityActionReadinessResolver.Resolve(
            Admission(
                authoring: CapabilityAuthoringAvailability.Unavailable,
                executionAdmitted: false),
            [
                Child("base", ResolvedChildReadiness.PendingInput),
                Child("diff", ResolvedChildReadiness.Blocked, "input.invalid"),
            ],
            Runtime(RuntimeDependencyEntry.Blocked(
                "legacy-combiner",
                "legacy-combiner-1.13.0",
                "external-tool.executable.missing",
                "External processor is unavailable.")),
            currentRuntimeDependencyGeneration: 1);

        Assert.False(result.Build.IsAvailable);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.AuthoringUnavailable,
            result.Build.PrimaryBlocker!.Code);
        Assert.Equal(5, result.Build.Blockers.Count);
        Assert.DoesNotContain(
            typeof(CapabilityActionReadinessSnapshot).GetProperties(),
            property => property.Name.Contains("Report", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            typeof(CapabilityActionAvailability).GetProperties(),
            property => property.Name.Contains("Report", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Preview can run a diagnostic while Build remains blocked by execution or runtime dependencies.</summary>
    [Fact]
    public void PreviewCanRunDiagnosticWhenExecutionOrRuntimeIsBlocked()
    {
        CapabilityActionReadinessSnapshot result = CapabilityActionReadinessResolver.Resolve(
            Admission(executionAdmitted: false),
            [Child("base", ResolvedChildReadiness.Ready)],
            Runtime(RuntimeDependencyEntry.Blocked(
                "legacy-combiner",
                "legacy-combiner-1.13.0",
                "external-tool.executable-sha.mismatch",
                "External processor identity is invalid.")),
            currentRuntimeDependencyGeneration: 1);

        Assert.True(result.Preview.IsAvailable);
        Assert.False(result.Build.IsAvailable);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.ExecutionNotAdmitted,
            result.Build.PrimaryBlocker!.Code);
        Assert.Equal(2, result.Build.Blockers.Count);
    }

    /// <summary>Supplied invalid input stays reportable; a genuinely absent required input blocks Preview.</summary>
    [Fact]
    public void PreviewDistinguishesBlockedInputFromPendingInput()
    {
        RuntimeDependencyReadinessSnapshot runtime = Runtime(
            RuntimeDependencyEntry.Ready("legacy-combiner", "legacy-combiner-1.13.0"));

        CapabilityActionReadinessSnapshot blocked = CapabilityActionReadinessResolver.Resolve(
            Admission(),
            [Child("diff-dlm", ResolvedChildReadiness.Blocked, "input.invalid")],
            runtime,
            currentRuntimeDependencyGeneration: 1);
        CapabilityActionReadinessSnapshot pending = CapabilityActionReadinessResolver.Resolve(
            Admission(),
            [Child("diff-dlm", ResolvedChildReadiness.PendingInput)],
            runtime,
            currentRuntimeDependencyGeneration: 1);

        Assert.True(blocked.Preview.IsAvailable);
        Assert.False(blocked.Build.IsAvailable);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.InputBlocked,
            blocked.Build.PrimaryBlocker!.Code);
        Assert.False(pending.Preview.IsAvailable);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.InputPending,
            pending.Preview.PrimaryBlocker!.Code);
    }

    /// <summary>A dependency result from another publication cannot enable the current Build.</summary>
    [Fact]
    public void StaleRuntimeDependencySnapshotBlocksBuild()
    {
        CapabilityAdmissionSnapshot admission = Admission();
        var stale = new RuntimeDependencyReadinessSnapshot(
            admission.RouteId,
            admission.CapabilityFingerprint,
            admission.CompilationFingerprint,
            new ResolutionToken("catalog:stale"),
            admission.AuthoringRevision,
            generation: 2,
            DateTimeOffset.UnixEpoch,
            [RuntimeDependencyEntry.Ready("legacy-combiner", "legacy-combiner-1.13.0")]);

        CapabilityActionReadinessSnapshot result = CapabilityActionReadinessResolver.Resolve(
            admission,
            [Child("base", ResolvedChildReadiness.Ready)],
            stale,
            currentRuntimeDependencyGeneration: 2);

        Assert.True(result.Preview.IsAvailable);
        Assert.False(result.Build.IsAvailable);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale,
            result.Build.PrimaryBlocker!.Code);
    }

    /// <summary>A result for an earlier authoring edit cannot enable the current Build.</summary>
    [Fact]
    public void EarlierAuthoringRevisionBlocksBuildEvenWhenRouteAndPublicationMatch()
    {
        CapabilityAdmissionSnapshot admission = Admission(authoringRevision: 2);
        var stale = new RuntimeDependencyReadinessSnapshot(
            admission.RouteId,
            admission.CapabilityFingerprint,
            admission.CompilationFingerprint,
            admission.ResolutionToken,
            new AuthoringRevision(1),
            generation: 3,
            DateTimeOffset.UnixEpoch,
            [RuntimeDependencyEntry.Ready("legacy-combiner", "legacy-combiner-1.13.0")]);

        CapabilityActionReadinessSnapshot result = CapabilityActionReadinessResolver.Resolve(
            admission,
            [Child("base", ResolvedChildReadiness.Ready)],
            stale,
            currentRuntimeDependencyGeneration: 3);

        Assert.False(result.Build.IsAvailable);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale,
            result.Build.PrimaryBlocker!.Code);
        Assert.Equal(new AuthoringRevision(2), result.AuthoringRevision);
    }

    /// <summary>A completed refresh from an older environment generation cannot win after replacement.</summary>
    [Fact]
    public void EarlierRuntimeGenerationBlocksBuildEvenWhenCapabilityIdentityMatches()
    {
        CapabilityAdmissionSnapshot admission = Admission(authoringRevision: 4);
        var stale = new RuntimeDependencyReadinessSnapshot(
            admission.RouteId,
            admission.CapabilityFingerprint,
            admission.CompilationFingerprint,
            admission.ResolutionToken,
            admission.AuthoringRevision,
            generation: 7,
            DateTimeOffset.UnixEpoch,
            [RuntimeDependencyEntry.Ready("legacy-combiner", "legacy-combiner-1.13.0")]);

        CapabilityActionReadinessSnapshot result = CapabilityActionReadinessResolver.Resolve(
            admission,
            [Child("base", ResolvedChildReadiness.Ready)],
            stale,
            currentRuntimeDependencyGeneration: 8);

        Assert.False(result.Build.IsAvailable);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale,
            result.Build.PrimaryBlocker!.Code);
        Assert.Equal(8, result.RuntimeDependencyGeneration);
    }

    /// <summary>A refresh for another compilation cannot enable Build under the same capability.</summary>
    [Fact]
    public void DifferentCompilationFingerprintBlocksBuildWithinSameCapability()
    {
        CapabilityAdmissionSnapshot admission = Admission();
        var stale = new RuntimeDependencyReadinessSnapshot(
            admission.RouteId,
            admission.CapabilityFingerprint,
            new string('c', 64),
            admission.ResolutionToken,
            admission.AuthoringRevision,
            generation: 1,
            DateTimeOffset.UnixEpoch,
            []);

        CapabilityActionReadinessSnapshot result =
            CapabilityActionReadinessResolver.Resolve(
                admission,
                [],
                stale,
                currentRuntimeDependencyGeneration: 1);

        Assert.False(result.Build.IsAvailable);
        Assert.Equal(
            CapabilityActionReadinessIssueCodes.RuntimeSnapshotStale,
            result.Build.PrimaryBlocker!.Code);
        Assert.Equal(admission.CompilationFingerprint, result.CompilationFingerprint);
    }

    /// <summary>Exact execution blockers survive projection while non-applicable work stays ignored.</summary>
    [Fact]
    public void ExactExecutionBlockerAndClosedNonApplicableStatesRemainDeterministic()
    {
        var exactBlocker = new CapabilityActionBlocker(
            "profile.execution.blocked",
            CapabilityReadinessDimension.Execution,
            "route-ctrlram",
            "The exact profile is not executable.",
            CapabilityReadinessNextAction.ReviewCompilation);
        var admission = new CapabilityAdmissionSnapshot(
            "route-ctrlram",
            new string('a', 64),
            new string('b', 64),
            Token,
            new AuthoringRevision(0),
            CapabilityAuthoringAvailability.Available,
            executionAdmitted: false,
            CapabilityEvidenceStatus.DirectGolden,
            CapabilityPublicationStatus.Candidate,
            exactBlocker);
        RuntimeDependencyReadinessSnapshot runtime = Runtime(
            new RuntimeDependencyEntry(
                "pending-processor",
                "pending-tool",
                ResolvedChildReadiness.PendingInput),
            new RuntimeDependencyEntry(
                "ignored-processor",
                "ignored-tool",
                ResolvedChildReadiness.NotApplicable));

        CapabilityActionReadinessSnapshot result =
            CapabilityActionReadinessResolver.Resolve(
                admission,
                [Child("optional", ResolvedChildReadiness.NotApplicable)],
                runtime,
                currentRuntimeDependencyGeneration: 1);

        Assert.Same(exactBlocker, result.Build.PrimaryBlocker);
        CapabilityActionBlocker runtimeBlocker = Assert.Single(
            result.Build.Blockers,
            blocker => blocker.Dimension ==
                CapabilityReadinessDimension.RuntimeDependency);
        Assert.Equal(
            "Refresh the required runtime dependency.",
            runtimeBlocker.Message);
    }

    /// <summary>An exact route with no child or runtime work remains immediately actionable.</summary>
    [Fact]
    public void EmptyInputAndRuntimeSetsRemainAvailable()
    {
        CapabilityActionReadinessSnapshot result =
            CapabilityActionReadinessResolver.Resolve(
                Admission(),
                [],
                Runtime(),
                currentRuntimeDependencyGeneration: 1);

        Assert.True(result.Preview.IsAvailable);
        Assert.True(result.Build.IsAvailable);
    }

    /// <summary>Only ready and not-applicable dependency entries make the exact snapshot ready.</summary>
    [Theory]
    [InlineData(ResolvedChildReadiness.Ready, true)]
    [InlineData(ResolvedChildReadiness.NotApplicable, true)]
    [InlineData(ResolvedChildReadiness.PendingInput, false)]
    [InlineData(ResolvedChildReadiness.Blocked, false)]
    public void RuntimeSnapshotReadinessUsesClosedDependencyStates(
        ResolvedChildReadiness readiness,
        bool expected)
    {
        RuntimeDependencyEntry entry = readiness switch
        {
            ResolvedChildReadiness.Ready =>
                RuntimeDependencyEntry.Ready("processor", "tool"),
            ResolvedChildReadiness.NotApplicable =>
                new RuntimeDependencyEntry("processor", "tool", readiness),
            ResolvedChildReadiness.PendingInput =>
                new RuntimeDependencyEntry("processor", "tool", readiness),
            ResolvedChildReadiness.Blocked =>
                RuntimeDependencyEntry.Blocked(
                    "processor",
                    "tool",
                    "runtime.blocked",
                    "The runtime dependency is blocked."),
            _ => throw new ArgumentOutOfRangeException(nameof(readiness)),
        };

        Assert.Equal(expected, Runtime(entry).IsReady);
    }

    /// <summary>Only the four accepted child readiness values are part of the public contract.</summary>
    [Fact]
    public void ChildReadinessVocabularyRemainsClosed()
    {
        Assert.Equal(
            [
                ResolvedChildReadiness.NotApplicable,
                ResolvedChildReadiness.PendingInput,
                ResolvedChildReadiness.Blocked,
                ResolvedChildReadiness.Ready,
            ],
            Enum.GetValues<ResolvedChildReadiness>());
    }

    /// <summary>Invalid admission identities and child states fail before action projection.</summary>
    [Fact]
    public void AdmissionAndChildContractsRejectInvalidState()
    {
        _ = Assert.Throws<ArgumentException>(() => new CapabilityAdmissionSnapshot(
            "route-ctrlram",
            "not-a-sha256",
            new string('b', 64),
            Token,
            new AuthoringRevision(0),
            CapabilityAuthoringAvailability.Available,
            executionAdmitted: true,
            CapabilityEvidenceStatus.DirectGolden,
            CapabilityPublicationStatus.Supported));
        _ = Assert.Throws<ArgumentException>(() => new CapabilityAdmissionSnapshot(
            "route-ctrlram",
            new string('a', 64),
            "not-a-sha256",
            Token,
            new AuthoringRevision(0),
            CapabilityAuthoringAvailability.Available,
            executionAdmitted: true,
            CapabilityEvidenceStatus.DirectGolden,
            CapabilityPublicationStatus.Supported));
        _ = Assert.Throws<ArgumentException>(() => new CapabilityAdmissionSnapshot(
            "route-ctrlram",
            new string('a', 64),
            new string('b', 64),
            default,
            new AuthoringRevision(0),
            CapabilityAuthoringAvailability.Available,
            executionAdmitted: true,
            CapabilityEvidenceStatus.DirectGolden,
            CapabilityPublicationStatus.Supported));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CapabilityAdmissionSnapshot(
            "route-ctrlram",
            new string('a', 64),
            new string('b', 64),
            Token,
            new AuthoringRevision(0),
            (CapabilityAuthoringAvailability)int.MaxValue,
            executionAdmitted: true,
            CapabilityEvidenceStatus.DirectGolden,
            CapabilityPublicationStatus.Supported));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CapabilityChildReadiness(
                "base",
                (ResolvedChildReadiness)int.MaxValue));
        _ = Assert.Throws<ArgumentException>(() =>
            new CapabilityChildReadiness(
                "base",
                ResolvedChildReadiness.Blocked));

        CapabilityAdmissionSnapshot admission = Admission();
        RuntimeDependencyReadinessSnapshot runtime = Runtime();
        CapabilityChildReadiness duplicate =
            Child("base", ResolvedChildReadiness.Ready);
        _ = Assert.Throws<ArgumentException>(() =>
            CapabilityActionReadinessResolver.Resolve(
                admission,
                [duplicate, duplicate],
                runtime,
                currentRuntimeDependencyGeneration: 1));
        _ = Assert.Throws<ArgumentException>(() =>
            CapabilityActionReadinessResolver.Resolve(
                admission,
                [null!],
                runtime,
                currentRuntimeDependencyGeneration: 1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CapabilityActionReadinessResolver.Resolve(
                admission,
                [],
                runtime,
                currentRuntimeDependencyGeneration: 0));
    }

    /// <summary>Runtime dependency requests and snapshots reject stale or malformed identities and entries.</summary>
    [Fact]
    public void RuntimeDependencyContractsRejectInvalidState()
    {
        _ = Assert.Throws<ArgumentException>(() => new RuntimeDependencyReadinessRequest(
            "route-ctrlram",
            "not-a-sha256",
            new string('b', 64),
            Token,
            new AuthoringRevision(0),
            []));
        _ = Assert.Throws<ArgumentException>(() => new RuntimeDependencyReadinessRequest(
            "route-ctrlram",
            new string('a', 64),
            "not-a-sha256",
            Token,
            new AuthoringRevision(0),
            []));
        _ = Assert.Throws<ArgumentException>(() => new RuntimeDependencyReadinessRequest(
            "route-ctrlram",
            new string('a', 64),
            new string('b', 64),
            default,
            new AuthoringRevision(0),
            []));
        _ = Assert.Throws<ArgumentException>(() => new RuntimeDependencyReadinessRequest(
            "route-ctrlram",
            new string('a', 64),
            new string('b', 64),
            Token,
            new AuthoringRevision(0),
            [null!]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RuntimeDependencyEntry(
                "processor",
                "tool",
                (ResolvedChildReadiness)int.MaxValue));
        _ = Assert.Throws<ArgumentException>(() =>
            new RuntimeDependencyEntry(
                "processor",
                "tool",
                ResolvedChildReadiness.PendingInput,
                "runtime.pending",
                "Pending dependencies cannot carry an issue."));

        var entry =
            RuntimeDependencyEntry.Ready("processor", "tool");
        _ = Assert.Throws<ArgumentException>(() =>
            new RuntimeDependencyReadinessSnapshot(
                "route-ctrlram",
                "not-a-sha256",
                new string('b', 64),
                Token,
                new AuthoringRevision(0),
                generation: 1,
                DateTimeOffset.UnixEpoch,
                [entry]));
        _ = Assert.Throws<ArgumentException>(() =>
            new RuntimeDependencyReadinessSnapshot(
                "route-ctrlram",
                new string('a', 64),
                "not-a-sha256",
                Token,
                new AuthoringRevision(0),
                generation: 1,
                DateTimeOffset.UnixEpoch,
                [entry]));
        _ = Assert.Throws<ArgumentException>(() =>
            new RuntimeDependencyReadinessSnapshot(
                "route-ctrlram",
                new string('a', 64),
                new string('b', 64),
                Token,
                new AuthoringRevision(0),
                generation: 1,
                new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.FromHours(8)),
                [entry]));
        _ = Assert.Throws<ArgumentException>(() =>
            new RuntimeDependencyReadinessSnapshot(
                "route-ctrlram",
                new string('a', 64),
                new string('b', 64),
                default,
                new AuthoringRevision(0),
                generation: 1,
                DateTimeOffset.UnixEpoch,
                [entry]));
        _ = Assert.Throws<ArgumentException>(() =>
            new RuntimeDependencyReadinessSnapshot(
                "route-ctrlram",
                new string('a', 64),
                new string('b', 64),
                Token,
                new AuthoringRevision(0),
                generation: 1,
                DateTimeOffset.UnixEpoch,
                [null!]));
        _ = Assert.Throws<ArgumentException>(() =>
            new RuntimeDependencyReadinessSnapshot(
                "route-ctrlram",
                new string('a', 64),
                new string('b', 64),
                Token,
                new AuthoringRevision(0),
                generation: 1,
                DateTimeOffset.UnixEpoch,
                [entry, entry]));
    }

    private static CapabilityAdmissionSnapshot Admission(
        CapabilityAuthoringAvailability authoring = CapabilityAuthoringAvailability.Available,
        bool executionAdmitted = true,
        CapabilityEvidenceStatus evidence = CapabilityEvidenceStatus.DirectGolden,
        CapabilityPublicationStatus publication = CapabilityPublicationStatus.Supported,
        long authoringRevision = 0)
    {
        return new CapabilityAdmissionSnapshot(
            "route-ctrlram",
            new string('a', 64),
            new string('b', 64),
            Token,
            new AuthoringRevision(authoringRevision),
            authoring,
            executionAdmitted,
            evidence,
            publication);
    }

    private static CapabilityChildReadiness Child(
        string childId,
        ResolvedChildReadiness readiness,
        string? issueCode = null)
    {
        return new CapabilityChildReadiness(
            childId,
            readiness,
            issueCode,
            issueCode is null ? null : "The supplied input is invalid.");
    }

    private static RuntimeDependencyReadinessSnapshot Runtime(
        params RuntimeDependencyEntry[] entries)
    {
        return new RuntimeDependencyReadinessSnapshot(
            "route-ctrlram",
            new string('a', 64),
            new string('b', 64),
            Token,
            new AuthoringRevision(0),
            generation: 1,
            DateTimeOffset.UnixEpoch,
            entries);
    }
}
