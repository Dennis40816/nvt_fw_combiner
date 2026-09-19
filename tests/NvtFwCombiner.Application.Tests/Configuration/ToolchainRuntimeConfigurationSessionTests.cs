using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.Configuration;

namespace NvtFwCombiner.Application.Tests.Configuration;

/// <summary>Selection admission, failure recovery and serialized publication without executing a runtime.</summary>
public sealed class ToolchainRuntimeConfigurationSessionTests
{
    private const string CandidatePath = @"C:\tools\runtime.exe";
    private static readonly string Hash = new('a', 64);
    private static readonly ToolchainRuntimeSelection User = new(ToolchainRuntimeSource.User, CandidatePath, Hash);

    /// <summary>A missing document explicitly selects bundled without probing user executables.</summary>
    [Fact]
    public async Task MissingConfigurationAdmitsBundledWithoutInspection()
    {
        var storage = new Storage();
        var inspector = new Inspector();
        using var session = new ToolchainRuntimeConfigurationSession(storage, inspector);
        Assert.Equal(ToolchainRuntimeConfigurationStatus.NotLoaded, session.Current.Status);

        ToolchainRuntimeConfigurationOperationResult result = await session.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Same(result.Snapshot, session.Current);
        Assert.Equal(1, result.Snapshot.Generation);
        Assert.Equal(new(ToolchainRuntimeSource.Bundled), result.Snapshot.Selection);
        Assert.Equal(0, inspector.Inspections);
        Assert.Equal(0, storage.Writes);
    }

    /// <summary>Successful saves persist the exact pin and publish a new immutable generation.</summary>
    [Fact]
    public async Task VerifiedUserSavePersistsExactSelection()
    {
        var storage = new Storage();
        using var session = new ToolchainRuntimeConfigurationSession(storage, new Inspector());
        ToolchainRuntimeConfigurationSnapshot before = session.Current;

        ToolchainRuntimeConfigurationOperationResult result = await session.SaveAsync(User, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(User, result.Snapshot.Selection);
        Assert.Equal(User, result.Snapshot.RequestedSelection);
        Assert.Equal(new(1, "user", CandidatePath, Hash), storage.Document);
        Assert.Equal(0, before.Generation);
        Assert.Null(before.Selection);
        Assert.Equal(1, result.Snapshot.Generation);
    }

    /// <summary>Startup retains the persisted User request even when its bytes changed before the first reload.</summary>
    [Fact]
    public async Task FreshReloadRetainsRejectedUserAndMissingFileExplicitlySelectsBundled()
    {
        var storage = new Storage { Document = new(1, "user", CandidatePath, Hash) };
        var inspector = new Inspector { Result = FailedEvidence("hash") };
        using var session = new ToolchainRuntimeConfigurationSession(storage, inspector);

        Assert.False((await session.ReloadAsync(TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal(User, session.Current.RequestedSelection);
        Assert.Null(session.Current.Selection);
        storage.Document = null;

        Assert.True((await session.ReloadAsync(TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal(ToolchainRuntimeSource.Bundled, session.Current.Selection!.Source);
        Assert.Equal(1, inspector.Inspections);
    }

    /// <summary>Unknown verification, changed bytes/path and contradictory evidence never publish or persist.</summary>
    [Theory]
    [InlineData("unknown")]
    [InlineData("rejected")]
    [InlineData("missing-identity")]
    [InlineData("hash")]
    [InlineData("path")]
    [InlineData("issues")]
    public async Task SaveRequiresCompleteVerifiedExactEvidence(string failure)
    {
        var storage = new Storage();
        var inspector = new Inspector();
        using var session = new ToolchainRuntimeConfigurationSession(storage, inspector);
        _ = await session.ReloadAsync(TestContext.Current.CancellationToken);
        ToolchainRuntimeConfigurationSnapshot before = session.Current;
        inspector.Result = FailedEvidence(failure);

        ToolchainRuntimeConfigurationOperationResult result = await session.SaveAsync(User, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Issues);
        Assert.Same(before, session.Current);
        Assert.Null(storage.Document);
        Assert.Equal(0, storage.Writes);
    }

    /// <summary>Reload of invalid or unreadable configuration blocks rather than reactivating bundled or prior user bytes.</summary>
    [Theory]
    [InlineData("hash")]
    [InlineData("unknown")]
    [InlineData("invalid-document")]
    [InlineData("read-io")]
    [InlineData("schema")]
    [InlineData("invalid-user")]
    public async Task InvalidReloadRetainsRequestedUserButClearsAdmission(string failure)
    {
        var storage = new Storage();
        var inspector = new Inspector();
        using var session = new ToolchainRuntimeConfigurationSession(storage, inspector);
        Assert.True((await session.SaveAsync(User, TestContext.Current.CancellationToken)).Succeeded);
        ToolchainRuntimeConfigurationSnapshot before = session.Current;
        if (failure == "invalid-document")
        {
            storage.ReadError = new ToolchainRuntimeConfigurationFormatException();
        }
        else if (failure == "read-io")
        {
            storage.ReadError = new IOException("read failure");
        }
        else if (failure == "schema")
        {
            storage.Document = storage.Document! with { SchemaVersion = 2 };
        }
        else if (failure == "invalid-user")
        {
            storage.Document = storage.Document! with { Sha256 = null };
        }
        else
        {
            inspector.Result = FailedEvidence(failure);
        }

        ToolchainRuntimeConfigurationOperationResult result = await session.ReloadAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ToolchainRuntimeConfigurationStatus.Blocked, session.Current.Status);
        Assert.Null(session.Current.Selection);
        Assert.Equal(ToolchainRuntimeSource.User, session.Current.RequestedSelection!.Source);
        Assert.Equal(CandidatePath, session.Current.RequestedSelection.Path);
        Assert.Equal(before.Generation + 1, session.Current.Generation);
        Assert.Equal(User, before.Selection);
    }

    /// <summary>Malformed selections are rejected before candidate inspection or writes.</summary>
    [Theory]
    [InlineData("source")]
    [InlineData("bundled-path")]
    [InlineData("user-path")]
    [InlineData("user-hash")]
    public async Task InvalidDraftShapePreservesCurrent(string failure)
    {
        var storage = new Storage();
        var inspector = new Inspector();
        using var session = new ToolchainRuntimeConfigurationSession(storage, inspector);
        ToolchainRuntimeSelection invalid = failure switch
        {
            "source" => new((ToolchainRuntimeSource)99),
            "bundled-path" => new(ToolchainRuntimeSource.Bundled, CandidatePath),
            "user-path" => User with { Path = " " },
            _ => User with { Sha256 = "abc" },
        };
        ToolchainRuntimeConfigurationSnapshot before = session.Current;

        Assert.False((await session.SaveAsync(invalid, TestContext.Current.CancellationToken)).Succeeded);

        Assert.Same(before, session.Current);
        Assert.Equal(0, inspector.Inspections);
        Assert.Equal(0, storage.Writes);
    }

    /// <summary>Failed storage writes leave the persisted and published selections intact.</summary>
    [Fact]
    public async Task FailedSavePreservesCurrent()
    {
        var storage = new Storage();
        using var session = new ToolchainRuntimeConfigurationSession(storage, new Inspector());
        _ = await session.SaveAsync(User, TestContext.Current.CancellationToken);
        ToolchainRuntimeConfigurationSnapshot before = session.Current;
        storage.WriteError = new IOException("write failure");

        Assert.False((await session.SaveAsync(new(ToolchainRuntimeSource.Bundled), TestContext.Current.CancellationToken)).Succeeded);

        Assert.Same(before, session.Current);
        Assert.Equal("user", storage.Document!.Source);
    }

    /// <summary>Cancellation after the atomic adapter returns cannot hide a completed save.</summary>
    [Fact]
    public async Task SuccessfulPersistencePublishesDespiteLateCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var storage = new Storage { AfterCommit = cancellation.Cancel };
        using var session = new ToolchainRuntimeConfigurationSession(storage, new Inspector());

        ToolchainRuntimeConfigurationOperationResult result = await session.SaveAsync(User, cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(result.Succeeded);
        Assert.Equal(User, session.Current.Selection);
        Assert.Equal(1, session.Current.Generation);
    }

    /// <summary>Cancellation before storage commit cannot publish or replace persisted intent.</summary>
    [Fact]
    public async Task CancellationBeforeCommitPreservesCurrent()
    {
        using var cancellation = new CancellationTokenSource();
        var storage = new Storage { BeforeCommit = () => { cancellation.Cancel(); return Task.CompletedTask; } };
        using var session = new ToolchainRuntimeConfigurationSession(storage, new Inspector());
        ToolchainRuntimeConfigurationSnapshot before = session.Current;

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.SaveAsync(User, cancellation.Token).AsTask());

        Assert.Same(before, session.Current);
        Assert.Null(storage.Document);
        Assert.Equal(0, storage.Writes);
    }

    /// <summary>A queued save cannot overtake a pending persistence or publish early.</summary>
    [Fact]
    public async Task ConcurrentSavesPublishInPersistenceOrder()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storage = new Storage { BeforeCommit = async () => { _ = entered.TrySetResult(); await release.Task; } };
        using var session = new ToolchainRuntimeConfigurationSession(storage, new Inspector());
        Task<ToolchainRuntimeConfigurationOperationResult> first = session.SaveAsync(User, TestContext.Current.CancellationToken).AsTask();
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        Task<ToolchainRuntimeConfigurationOperationResult> second = session.SaveAsync(new(ToolchainRuntimeSource.Bundled), TestContext.Current.CancellationToken).AsTask();
        Assert.False(second.IsCompleted);
        Assert.Equal(0, session.Current.Generation);
        release.SetResult();

        Assert.Equal(1, (await first).Snapshot.Generation);
        Assert.Equal(2, (await second).Snapshot.Generation);
        Assert.Equal(ToolchainRuntimeSource.Bundled, session.Current.Selection!.Source);
        Assert.Equal("bundled", storage.Document!.Source);
    }

    /// <summary>Candidate inspection and detection cannot change selection or persist discovery.</summary>
    [Fact]
    public async Task InspectAndDetectDoNotPublish()
    {
        var storage = new Storage();
        using var session = new ToolchainRuntimeConfigurationSession(storage, new Inspector());
        ToolchainRuntimeConfigurationSnapshot before = session.Current;
        Assert.Equal(ToolchainRuntimeCandidateVerification.Verified,
            (await session.InspectAsync(CandidatePath, TestContext.Current.CancellationToken)).Verification);
        _ = Assert.Single(await session.DetectAsync(TestContext.Current.CancellationToken));
        Assert.Equal(ToolchainRuntimeCandidateVerification.Unknown,
            (await session.InspectBundledAsync(TestContext.Current.CancellationToken)).Verification);
        Assert.Same(before, session.Current);
        Assert.Equal(0, storage.Writes);
    }

    private static ToolchainRuntimeCandidateInspection FailedEvidence(string failure)
    {
        return new(
            failure == "missing-identity" ? null : new(failure == "path" ? CandidatePath + ".other" : CandidatePath,
                failure == "hash" ? new('b', 64) : Hash, "1.0", "x64"),
            failure == "unknown" ? ToolchainRuntimeCandidateVerification.Unknown : failure == "rejected"
                ? ToolchainRuntimeCandidateVerification.Rejected : ToolchainRuntimeCandidateVerification.Verified,
            failure == "issues" ? [new("test-rejected", "Candidate rejected.")] : []);
    }

    private sealed class Inspector : IToolchainRuntimeCandidateInspector
    {
        internal int Inspections { get; private set; }
        internal ToolchainRuntimeCandidateInspection Result { get; set; } = FailedEvidence("none");
        public ValueTask<ToolchainRuntimeCandidateInspection> InspectAsync(string path, CancellationToken cancellationToken)
        {
            Inspections++;
            return ValueTask.FromResult(Result);
        }
        public ValueTask<IReadOnlyList<ToolchainRuntimeCandidateInspection>> DetectAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<IReadOnlyList<ToolchainRuntimeCandidateInspection>>(Array.AsReadOnly([Result]));
        }

        public ValueTask<ToolchainRuntimeCandidateInspection> InspectBundledAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(new ToolchainRuntimeCandidateInspection(Result.Identity,
                ToolchainRuntimeCandidateVerification.Unknown, []));
        }
    }

    private sealed class Storage : IToolchainRuntimeConfigurationStorage
    {
        internal ToolchainRuntimeConfigurationDocument? Document { get; set; }
        internal Exception? ReadError { get; set; }
        internal Exception? WriteError { get; set; }
        internal Action? AfterCommit { get; init; }
        internal Func<Task>? BeforeCommit { get; init; }
        internal int Writes { get; private set; }
        public ValueTask<ToolchainRuntimeConfigurationDocument?> ReadAsync(CancellationToken cancellationToken)
        {
            return ReadError is null ? ValueTask.FromResult(Document) : ValueTask.FromException<ToolchainRuntimeConfigurationDocument?>(ReadError);
        }

        public async ValueTask WriteAsync(ToolchainRuntimeConfigurationDocument document, CancellationToken cancellationToken)
        {
            if (WriteError is not null)
            {
                throw WriteError;
            }

            if (BeforeCommit is not null)
            {
                await BeforeCommit();
            }

            cancellationToken.ThrowIfCancellationRequested();
            Document = document;
            Writes++;
            AfterCommit?.Invoke();
        }
    }
}
